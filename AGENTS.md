# AP-Scaffold — AI 协作交接文档

> 本文档面向 Kimi / Cursor / Copilot 等 AI 编码助手。当你在一个新 Session 中接手本项目时，请先完整阅读本文件，再继续处理用户请求。
> 本文档以**实际代码**为准整理（已核对全部 36 个项目）。
>
> **新 Session 阅读顺序（避免重复探索）**：① 本文件（现状与约定）→ ② `docs/EVOLUTION_PLAN.md`（架构演进史、任务铁律、停车场）→ ③ `docs/conventions/`（四份规范：ERROR_HANDLING / LOGGING / LAYERING / DEPENDENCIES）。读完这三处即具备完整上下文，无需再扫描全仓库。

---

## 1. 项目速览

**AP-Scaffold** 是一个工业自动化上位机平台脚手架，采用 .NET 8 WPF + Prism + 插件化架构。设计目标：**可快速复用、安全可靠、统一视觉**。

- **工作目录**: `D:\workspace\AP-Scaffold`
- **解决方案**: `AP-Automation.Platform.slnx`
- **启动项目**: `platform/hosts/AP.Host.Desktop/AP.Host.Desktop.csproj`
- **目标框架**: .NET 8（构建 SDK 为 .NET 10.0.102，见 `global.json`）
- **输出目录**: `bin/Release/`（插件输出到 `bin/Release/plugins/{PluginName}/`，由 `Directory.Build.props` 自动处理，无需构建后事件）

---

## 2. 当前上下文

### 2.1 分支与提交

- **当前分支**: `main`（已与远程同步）
- **演进状态**: **`docs/EVOLUTION_PLAN.md` 全部 33 个任务（阶段 0~6）+ 停车场第一项已完成**。阶段：规范先行 → 连接监督收敛（四套看门狗归一为 `ConnectionSupervisor`）→ 地址对象化 → Device 抽象 → Tag 系统（点表/读写/采集/Dashboard 真实数据）→ 业务防线与依赖清理（LAYERING 规范、契约层死引用删除）。附加交付：带类型批量契约 `IPlcTypedBatchRead` + 采集引擎合并读；首个真实报表（操作审计日报 AuditDaily）。关键热修复：启动时序、优雅关闭死锁（`Dispatcher.Invoke`→`BeginInvoke`）
- **验证状态**: 西门子仿真环境真机验证通过（连接/掉线/重连/混合类型批量/Dashboard 联动/优雅关闭）；三菱/欧姆龙待真机
- **工作区状态**: 干净（开始新任务前请再次 `git status` 确认）

### 2.2 已完成功能

- 插件化核心框架（加载、隔离上下文、生命周期、14 态状态机、MediatR 事件总线）
- **声明式导航贡献者模式**（`INavigationContributor`，见 5.1）
- **设置贡献者模式**（`ISettingsContributor`，见 5.2）
- 三菱 / 西门子 / 欧姆龙 PLC 驱动与统一切换机制（`IPlcDriverFactory` + `ActivePlcService`）
- 串口扫码枪硬件驱动（`Plugins:Configuration:AP.Plugin.Scanner:Enabled=false` 可整体禁用：不注册服务/设备、不连接，系统配置页有勾选；2026-07-31 起）
- gRPC Server/Client 分布式通信（proto 契约 + StreamBroadcaster 广播）❄ 已封存（代码保留不维护，文件头有标注）
- 安全模块：本地用户/角色/权限（12 个种子权限、3 个默认角色）、登录、强制改密、审计日志
- 用户管理、角色管理、审计日志可视化、配方管理骨架、报表中心骨架
- 全浅色统一视觉主题（`Industrial.Teal.MD3.xaml`，文件名保留但内容已是浅色）
- 系统托盘、启动画面（Splash）、全局异常崩溃日志
- **Device Runtime Model（设备运行时模型，见 5.12）**：协议无关连接状态机 + 统一连接监督器（全部 4 个硬件驱动共享）、`IDevice`/`IDeviceRegistry` 设备抽象、统一状态事件 `DeviceStateChangedEvent`
- **Tag 系统（见 5.13）**：点表（`Configuration/tags.json`）、`ITagService` 按点名读写（质量戳结果）、采集引擎 + 最新值表 + 变化事件订阅、**带类型批量读**（`IPlcTypedBatchRead`，在线每周期一次往返+三级降级）
- **横切规范文档（四份，新代码必须遵守）**：`docs/conventions/ERROR_HANDLING.md`、`LOGGING.md`、`LAYERING.md`（设备访问防线）、`DEPENDENCIES.md`（依赖方向）
- **首个真实报表**：操作审计日报（`AuditDailyReportProvider`，数据源=审计日志）
- **点表可视化编辑（2026-07-31，`AP.Plugin.TagConfiguration`）**：菜单"点表配置"（Order=1100，权限 `device.config`），tags.json 列表增删改 + 默认/按点采集周期编辑；保存前经 `ITagTableValidator` 全量校验（与启动加载同一规则，非法不落盘），原子写入含头部注释，保存写审计，**保存后热重载即时生效**（`ITagTableReloader`：换表→引擎 Restart→值表 PruneExcept，校验失败保留旧表，见 5.13）
- **仪表板框架通用化（2026-08-06 重构）**：首页只展示系统健康度、设备状态与运行概况，不依赖任何业务 Tag——欢迎区（问候+软件名+健康结论+实时时钟）、六张统计卡（在线设备/当前告警/采集点/通讯成功率/系统资源/运行时间，全部真实数据）、设备状态总览、系统服务状态（数据库探测/采集引擎/审计/资源监控，探测逻辑抽为 Layout 插件 `DatabaseStatusService` 与状态栏共用）、最近事件（仅设备连接状态变化/扫码/系统启动，文案面向操作员如"自动重连成功"；**不含 Tag 实时值变化**——那是数据不是事件）、快捷入口（复用导航贡献者，排除首页自身，≤6 个）。实时趋势/工艺曲线归业务页面，LiveCharts2 能力保留给业务插件（宿主共享库模式不变，见 5.11）；`ITagAcquisitionStatus` 新增 `TotalReads`/`FailedReads` 读次统计支撑通讯成功率卡。历史演进：2026-08-03 曾落地首页实时趋势卡（LiveCharts2），本次按"框架级首页不放业务数据"定位移除

### 2.3 活跃问题 / 待办

> **范围决策（2026-07-21）**：当前仅聚焦 **Standalone 单机模式 + SQLite 数据库**。Server/Client（gRPC 技术栈）与 PostgreSQL/SQL Server 支持**冻结**——代码保留但不维护、不验证、不投入改进；`AppRole.Server`/`AppRole.Client` 保留但视为"未支持"。除非用户明确要求解冻，不要在这些方向做改动。详见 `docs/IMPROVEMENT_PLAN.md` 1.3 节与第八章末「冻结事项清单」。
>
> **演进节奏决策（2026-07-28，用户）**：演进计划已全部收官，**当前不接新框架功能**，等真实外包项目驱动需求。`EVOLUTION_PLAN.md` 任务铁律仍然有效（一次一个 Task、单独提交、可回滚、不改公开 API 除非必须）。

- [x] 点表热重载（2026-08-07，停车场议题落地）：点表编辑器保存后免重启——`ITagTableReloader` 编排（`TagTable.Reload` 原子替换快照→采集引擎 `Restart` 重建分组→`LatestTagValueStore.PruneExcept` 清理删点残留），校验失败保留旧表继续运行；仅编辑页保存触发，手工改 tags.json 仍需重启
- [ ] 配方管理完善（**等真实项目配方需求再动**：参数校验/版本历史/`SwitchAsync` 事件 TODO，避免返工）
- [ ] Tag 值持久化（停车场议题，设备运行日报/历史报表前置；等客户提历史数据需求再选型）
- [x] 报表中心首个真实数据提供者（2026-07-28，操作审计日报 AuditDaily）
- [ ] 三菱/欧姆龙真机验证（西门子仿真已验证，其余品牌随项目进场验证）
- [ ] `RequiresCapabilitiesAttribute` 目前仅有声明，无运行时强制检查
- [ ] 停车场（`EVOLUTION_PLAN.md`）：SocketException(995) 未观察异常噪音；设备掉线/恢复是否进审计
- [ ] OpenTelemetry（**已判定单机外包场景不做**，仅保留记录）

---

## 3. 目录结构速查

```
AP-Scaffold/
├── platform/
│   ├── core/AP.Core                      # 插件框架、生命周期、状态机、事件总线
│   ├── contracts/                        # 接口与事件契约
│   │   ├── AP.Contracts.Core             # OperationResult、ErrorCode、AppInitializedEvent（Prism PubSubEvent）
│   │   ├── AP.Contracts.Hardware         # IPlcService、IPlcDriverFactory、PlcOptions、设备事件；DeviceRuntime/（IDevice/IDeviceRegistry/ITagService/ITagTable/TagValue/TagDefinition/统一事件）
│   │   ├── AP.Contracts.Communication    # 仅 proto 文件（automation_gate.proto / common.proto）
│   │   ├── AP.Contracts.System           # ILoginService、ISettingsDialogService、ISystemMonitorService
│   │   ├── AP.Contracts.Security         # IIdentityService、IAuditService、用户/角色/权限模型
│   │   ├── AP.Contracts.Recipe           # IRecipeManager（注意：不是 IRecipeService）
│   │   └── AP.Contracts.Report           # IReportCenterService、ReportArchiveDto
│   ├── infra/                            # 基础设施实现
│   │   ├── AP.Infra.Database             # FreeSql (SQLite/PostgreSQL)，UseAutoSyncStructure(false)
│   │   ├── AP.Infra.Grpc                 # gRPC Server/Client、StreamBroadcaster、LoggingInterceptor
│   │   ├── AP.Infra.Hardware             # PlcDriverRegistry、ActivePlcService（统一 IPlcService 代理）；DeviceRuntime/（状态机/ConnectionSupervisor/DeviceRegistry/TagTable/TagService/采集引擎/最新值表）
│   │   ├── AP.Infra.Logging              # Serilog、LogCleanupHelper
│   │   ├── AP.Infra.Resilience           # Polly 策略工厂（Database-Retry / PLC-Retry / Grpc-CircuitBreaker）
│   │   ├── AP.Infra.Security             # 安全/权限/审计实现、SecurityDbInitializer
│   │   ├── AP.Infra.Recipe               # RecipeManager、RecipeDbInitializer
│   │   └── AP.Infra.Report               # 报表框架（IReportDataProvider/ReportData 在契约层 AP.Contracts.Report）
│   ├── shared/                           # 共享库
│   │   ├── AP.Shared.PluginSDK           # PluginBase、INavigationContributor、ISettingsContributor
│   │   ├── AP.Shared.UI                  # LoadingSpinner、对话框服务、PermissionBehavior、浅色主题
│   │   └── AP.Shared.Utilities           # ConfigurationHelper、SerializationHelper、常量
│   ├── plugins/                          # 插件（13 个）
│   │   ├── hardware/
│   │   │   ├── AP.Plugin.Plc.Mitsubishi  # Priority=20，Server|Standalone
│   │   │   ├── AP.Plugin.Plc.Siemens     # Priority=21，Server|Standalone
│   │   │   ├── AP.Plugin.Plc.Omron       # Priority=22，Server|Standalone
│   │   │   └── AP.Plugin.Scanner         # Priority=20，Client|Standalone
│   │   ├── business/
│   │   │   ├── AP.Plugin.DeviceConfiguration  # Priority=100，ISettingsContributor（扫码枪配置）
│   │   │   └── AP.Plugin.TagConfiguration     # Priority=100，INavigationContributor（点表编辑，Order=1100，device.config）
│   │   └── system/
│   │       ├── AP.Plugin.Layout          # Priority=10，布局/Sidebar/仪表盘
│   │       ├── AP.Plugin.Login           # Priority=1，登录窗口（ILoginService）
│   │       ├── AP.Plugin.SystemSettings  # Priority=5，系统配置中心
│   │       ├── AP.Plugin.UserManagement  # Priority=5
│   │       ├── AP.Plugin.RoleManagement  # Priority=6
│   │       ├── AP.Plugin.AuditLog        # Priority=7
│   │       ├── AP.Plugin.RecipeManagement# Priority=8
│   │       └── AP.Plugin.ReportCenter    # Priority=9
│   ├── hosts/AP.Host.Desktop             # WPF 启动宿主（Bootstrapper）
│   └── tests/                            # xUnit 测试（45 个测试文件 / 475 个测试）
│       ├── AP.Core.Tests                 # 9 文件 / 130 测试
│       ├── AP.Shared.Tests               # 4 文件 / 48 测试
│       └── AP.Infra.Tests                # 32 文件 / 293 测试（含 DeviceRuntime/驱动地址对象/带类型批量全套）
├── docs/                                 # 架构/使用/测试/状态文档
├── installer/setup.iss                   # Inno Setup 6 安装包脚本
├── AGENTS.md                             # 本文件
└── AP-Automation.Platform.slnx
```

---

## 4. 关键命令

```bash
# 构建整个解决方案
dotnet build AP-Automation.Platform.slnx -c Release

# 运行所有测试（先构建）
dotnet test platform/tests/AP.Core.Tests/AP.Core.Tests.csproj -c Release -v quiet
dotnet test platform/tests/AP.Shared.Tests/AP.Shared.Tests.csproj -c Release -v quiet
dotnet test platform/tests/AP.Infra.Tests/AP.Infra.Tests.csproj -c Release -v quiet

# 运行桌面宿主（需要 Windows + 人工登录）
bin/Release/AP.Host.Desktop.exe

# 一键构建打包安装包（清理 bin → 构建 → 发布 → Inno 编译，产物在 installer/Output/）
installer/build-installer.bat
```

安装包手动分步流程与发布形态说明见 `installer/README.md`。

---

## 5. 核心约定与常见坑点

### 5.1 导航贡献者模式（新插件加菜单的正确方式）

- 接口定义在 `AP.Shared.PluginSDK/Navigation/`：`INavigationContributor.GetMenuItems()` 返回 `NavigationMenuItem`（`Label` / `IconKind` / `NavigationTarget` / `Order` / `Permission?` / `Category?` / `IsDefault`）。
- 插件主类实现该接口即可出现在 Sidebar，**无需**手动操作 SidebarViewModel；但仍需在 `InitializeAsync` 中把视图 `RegisterViewWithRegion("ContentRegion", ...)`（按权限条件）。
- Host 在 `Bootstrapper` 桥接 DryIoc 时，对实现了 `INavigationContributor` 的插件实例额外 `RegisterInstance(typeof(INavigationContributor), instance)`。
- `SidebarViewModel` 注入 `IEnumerable<INavigationContributor>`，用 `NavigationMenuItemBuilder.Build(contributors, identityService.HasPermission, defaultTarget, visibilityFilter)` 构建菜单：按 `NavigationTarget` 去重（取最小 Order）→ 排序 → 权限过滤 → 白名单过滤 → 无默认项时取第一个可见项。
- 默认导航延迟到 `Dispatcher.BeginInvoke(Background)` 执行（确保 UI 就绪后再选中）；同处订阅 `ContentRegion` 的 `NavigationService.Navigated` 事件——Sidebar 之外发起的导航（如首页快捷入口 `RequestNavigate`）会回写左侧选中态（`_syncingSelection` 标志防止回写再次触发导航）。
- 相关配置键：
  - `AppConfiguration:DefaultNavigationTarget`（当前 `"DashboardView"`）
  - `AppConfiguration:NavigationWhenSecurityDisabled`（字符串数组白名单；`Security:Enabled=false` 时只显示这些 Target，当前为 `DashboardView / SettingsShellView / RecipeListView / ReportListView`）
- 当前菜单 Order：仪表板 100 → 系统配置 1000 → 点表配置 1100 → 配方管理 2000 → 报表中心 3000 → 用户管理 4000 → 角色管理 4100 → 审计日志 4200。

### 5.2 设置贡献者模式（往系统配置中心加一页）

- 接口定义在 `AP.Shared.PluginSDK/Configuration/`：`ISettingsContributor`（`Category` / `Title` / `IconKind` / `Order` / `ConfigurationSection` + `CreateViewModel(IServiceProvider)`），编辑器 VM 实现 `ISettingsEditorViewModel`（`LoadFromConfiguration` / `Validate` / `GetConfigurationValue` / `RequiresRestart`）。
- 在插件 `ConfigureServices` 中 `AddSingleton<ISettingsContributor, YourContributor>()`，并在 `InitializeAsync` 中把 `编辑器VM→编辑器View` 的 DataTemplate 注册到 `Application.Current.Resources`。
- `SettingsShellViewModel` 自动收集所有贡献者按 Category 分组显示；保存时统一 Validate → 备份 appsettings → `ConfigurationHelper.UpdateAppSetting` 写回（临时文件+替换原子写入，失败如实抛错并提示）；保存成功/失败均由 `SettingsService` 写审计日志（`Update`/"修改系统配置"，含变更配置节，不记录具体值）。
- 参考实现：`AP.Plugin.SystemSettings`（应用基础信息、PLC 配置）、`AP.Plugin.DeviceConfiguration`（扫码枪配置）。

### 5.3 插件开发约定

- 插件主类继承 `AP.Shared.PluginSDK.Base.PluginBase`，标注 `[PluginMetadata]`（`Id` 必须与目录名/DLL 名一致；`SupportedRoles` 默认 `AppRole.All`；`Priority` 默认 100，越小越先加载；`Required` 默认 true）。
- **Required/Dependencies/重复 ID 语义已落地**（2026-07-21，`PluginGraphValidator`）：重复 ID 全部拒绝加载并中止启动；`Dependencies` 缺失时拒绝加载该插件并级联（Required 插件依赖缺失中止启动）；Required 插件配置/实例化/初始化/启动失败时中止启动并弹窗说明。当前仅 `Login`、`Layout` 为 Required，其余 10 个插件均 `Required = false`——新插件请按"没有它系统是否还可用"判断，功能/硬件类默认应设 `Required = false`。
- `ConfigureServices` 注册 View/ViewModel；`InitializeAsync` 中按权限条件注册 Region 视图。
- **View 的 DataContext 注入模式**: 统一采用**构造函数注入 ViewModel**（`public UserListView(UserListViewModel vm)`）。全仓库无 `AutoWireViewModel`；部分插件保留 `ViewModelLocationProvider.Register` 作双保险。Login 窗口由 `LoginService` 手动赋 DataContext；设置编辑器 View 通过 DataTemplate 关联。
- 插件之间不允许互相引用，通信通过 `Contracts` 事件 + MediatR。

### 5.4 数据库建表

- `AddPlatformDatabase` 设置了 `.UseAutoSyncStructure(false)`，**不会自动建表**。
- 新增实体必须在对应模块的初始化器中调用 `_freeSql.CodeFirst.SyncStructure<TEntity>()`。
- 当前手动调用的初始化器（均由 `Bootstrapper` 显式解析调用）：
  - `SecurityDbInitializer`（`ISecurityDbInitializer`，登录前同步一次 + `OnInitialized` 一次，幂等）
  - `RecipeDbInitializer`（`IRecipeDbInitializer`）
  - `ReportDatabaseInitializer`（`OnInitialized` 中直接解析并调 `StartAsync`）
- 表清单：`sys_users`、`sys_roles`、`sys_permissions`、`sys_user_roles`、`sys_role_permissions`、`sys_audit_logs`、`recipes`、`report_archives`。

### 5.5 PLC 品牌切换

- 通过统一的 `Plc` 配置节切换（不是各插件独立配置节）：
  `Plc:DriverType`（`Mitsubishi` / `Siemens` / `Omron`）、`Plc:IpAddress`、`Plc:Port`、`Plc:Timeout`、`Plc:Model`（三菱 `Qna_3E`；西门子 `S7_1200` 等；欧姆龙 `FinsTcp`，不解析）、`Plc:HeartbeatAddress`；可选连接参数（2026-07-25 起）：`Plc:HeartbeatIntervalSeconds`（默认 2）、`Plc:ReconnectBackoffSeconds`（默认 5）、`Plc:SupervisorRestartDelaySeconds`（默认 5）。
- 各 PLC 插件注册 `IPlcDriverFactory`，`AP.Infra.Hardware.ActivePlcService`（懒加载代理）按 `DriverType` 从 `PlcDriverRegistry` 取工厂创建真实驱动并转发 `IPlcService` / `IPlcBatchReadWrite` 调用。
- **PLC 写操作审计**：DI 中 `IPlcService` 实际解析为 `AuditingPlcServiceDecorator`（包装 `ActivePlcService`），`WriteAsync`/`WriteBatchAsync` 自动写审计日志（`ManualControl`，含地址/值/结果；操作人取 `IIdentityService.CurrentUser`，未登录记 `system`，Security 禁用恒 `anonymous`）；审计/身份服务未注册时降级不审计，审计失败不影响写入。读操作与连接管理不审计。
- **连接监督（2026-07-25 重构）**：三个 PLC 驱动与扫码枪**共享同一个 `ConnectionSupervisor`**（`AP.Infra.Hardware/DeviceRuntime/`）——心跳探测 + 断线重连 + 监督者自愈；连接状态由 `DeviceConnectionStateMachine`（六态：Disconnected/Connecting/Connected/Reconnecting/Faulted/Disabled）作为**唯一事实来源**，各驱动不再有私有 `bool` 状态或自有看门狗；事件经驱动的 `TransitionEventBridge` 声明式映射发布（桥接属于驱动，Supervisor 不识 MediatR）；日志由 `ConnectionSupervisorLogger` 统一消费（状态迁移记录法，重连尝试 Debug）。新增品牌**不再复制看门狗代码**，只需提供 connect/probe 两个委托。
- **地址对象（2026-07-25 起）**：三个驱动各自内置 internal 地址领域对象（三菱 `McAddress`、西门子 `S7Address`、欧姆龙 `FinsAddress`，解析/规范化/结构化错误/值相等），读写前预检+规范化，非法地址抛带错误码的 `ArgumentException` 子类；`IAddressValidator` 契约供点表加载时跨驱动校验（Address Object 缓存于 `ResolvedTag`）。**协议地址语法只允许存在于驱动内部**，上层一律用逻辑点名（见 5.13）。
- **欧姆龙驱动差异**（IoTClient `OmronFinsClient` 限制，代码已注释）：字符串读写未实现（调用抛 `NotSupportedException`）；批量写入退化为逐条写入；批量读为真批量（`BatchRead` 第二参数无实际效果，传 0）；字节序默认 CDAB；`Model` 不解析（保留 "FinsTcp"）。
- `PlcDriverRegistry` 为单例，**首次解析时从 DI 收集所有 `IPlcDriverFactory`**（`AddPlcHardware` 中的工厂委托完成，2026-07-22 修复了注册表恒为空的缺陷）；不要在插件里手动 `new PlcDriverRegistry()` 或调 `Register`。
- 新增品牌只需实现 `IPlcDriverFactory` 并注册到 DI，业务代码无需修改（欧姆龙即按此方式接入）；插件的 `StartAsync`/`StopAsync` 需按 `IsActiveDriver()` 门控（仅激活品牌发起/断开连接，参考三菱/西门子/欧姆龙插件），避免多品牌插件重复连接同一 `ActivePlcService` 代理。
- **带类型批量读（2026-07-28 起）**：`IPlcTypedBatchRead.ReadBatchAsync(IReadOnlyList<BatchReadItem>)`——每个地址携带 `TagDataType`（西门子/欧姆龙真批量 11 型全映射，三菱循环同契约）；采集引擎在线时每周期一次批量往返（三级降级：不支持永久逐点/整批失败本轮逐点/断连直接逐点）。旧 `IPlcBatchReadWrite` 保留为内部通道，不新增调用点。
- 西门子地址格式与三菱不同，业务插件中的地址应通过**点表 `tags.json`** 配置（见 5.13），避免硬编码。
- 系统设置中已有 PLC 配置编辑页（`PlcConfigurationContributor`，DriverType 切换自动填默认值，含连接参数三项，RequiresRestart）。

### 5.6 IHostedService 不自动启动

- `AP.Host.Desktop` 使用 `PrismBootstrapper` 手动构建容器，不启动 `IHost`，因此 `AddHostedService<T>()` 注册的服务**不会自动运行**。
- 若需要在启动时执行，必须在 `Bootstrapper.OnInitialized` 中显式解析并调用（参考 `ReportDatabaseInitializer`、`GrpcClientWorker`）。
- `ReportScheduler` / `ReportCleanupService` 已修复（2026-07-21）：注册改为"单例 + `AddHostedService` 转发"，并由 `Bootstrapper.OnInitialized` 在报表初始化段显式 `StartAsync`。新增同类后台服务请沿用同一模式。

### 5.7 契约程序集必须被 Host 直接引用

- 插件隔离加载，但共享契约 DLL 必须出现在 Host 输出目录。
- 若 MediatR 扫描时报告 `ReflectionTypeLoadException: Could not load file or assembly 'AP.Contracts.Xxx'`，请在 `AP.Host.Desktop.csproj` 中直接引用该项目。
- Host 当前直接引用：`AP.Contracts.Hardware`、`AP.Contracts.Report`、`AP.Contracts.System`（其余契约经传递引用获得）、全部 8 个 Infra、`AP.Core`、`AP.Shared.PluginSDK`、`AP.Shared.UI`。

### 5.8 权限字符串（12 个种子权限）

`SecurityDbInitializer` 种入的完整权限清单（新增功能时请保持统一风格 `资源.操作`）：

| 权限 | 用途 |
|------|------|
| `system.view` | 系统查看（基础） |
| `system.settings` | 系统配置中心 |
| `user.manage` | 用户管理 |
| `role.manage` | 角色管理 |
| `audit.view` | 审计日志查看 |
| `recipe.view` | 配方查看 |
| `recipe.edit` | 配方编辑 |
| `recipe.switch` | 配方切换 |
| `report.view` | 报表查看 |
| `report.export` | 报表导出 |
| `device.config` | 设备配置 |
| `test.start` | 启动测试 |

默认角色：`Administrator`（全部权限）、`Operator`（system.view / recipe.view / recipe.switch / report.view / report.export / test.start）、`Technician`（system.view / system.settings / recipe.* / report.view / report.export / device.config）。默认账号 `admin / admin123`，首次登录强制改密（仅 `Security:Enabled=true` 时涉及；当前默认配置为 false，见 5.9）。

视图级权限控制还可用 `AP.Shared.UI` 的 `PermissionBehavior` 附加属性：`ui:PermissionBehavior.Permission="user.manage"`，`HideWhenUnauthorized=false` 时禁用而非隐藏。

### 5.9 Security:Enabled=false 的行为

> **当前默认配置即为 `Security:Enabled=false`**（2026-07-22 起，面向免登录单机场景）；需要登录与权限时改为 `true`。

- DI 层：不注册用户/角色/权限 Repository 与初始化器；`IIdentityService` 替换为 `AnonymousIdentityService`（anonymous / Roles=["Administrator"] / Permissions=["*"]，HasPermission 恒 true）；审计按 `Security:Audit:Enabled` 独立判断（缺省回退到 `Security:Enabled`；当前配置显式 `true`，免登录下审计保留），关闭时用 `NullAuditService`；审计表由 `AuditService` 构造函数幂等自建（`SyncStructure<AuditLog>`），不再依赖仅 Security 启用时注册的初始化器。
- 宿主：跳过登录窗口与安全库初始化。
- 菜单：Sidebar 只显示 `AppConfiguration:NavigationWhenSecurityDisabled` 白名单内的 Target；UserManagement/RoleManagement/AuditLog 插件在 `InitializeAsync` 中检测禁用后直接 return 不注册视图（注意：RecipeManagement/ReportCenter 目前只按各自权限门控，未检查 `Security:Enabled`）。

### 5.10 统一视觉主题

- 主题文件：`AP.Shared.UI/Themes/Industrial.Teal.MD3.xaml`（文件名保留，**内容已是全浅色主题**）。
- 主色 `Color.Primary #1E3A5F`（深蓝），强调色 `Color.Accent #0891B2`（青蓝），语义色 Success/Warning/Error/Info 齐全；表面色全浅色（`Surface #FFFFFF`、`SurfaceDark #E8EDF2` 用于侧边栏）。
- 引用链：`App.xaml` 合并 `MaterialDesign3.Defaults.xaml` + `BundledTheme(BaseTheme=Light, Primary=BlueGrey, Secondary=Cyan)` + `AP.Shared.UI/Resources/ResourceDictionary.xaml`（内部合并主题与转换器）。
- 新 UI 请使用主题资源键（`Brush.*`、`TextStyle.*`、`Layout.Spacing.*` 等），不要硬编码颜色。
- **加载遮罩**：统一用 `AP.Shared.UI.Controls.LoadingSpinner`（浅色遮罩 `Brush.Surface` Opacity=0.85 + `TextStyle.Body`；`IsLoading`/`LoadingText` DP 通常绑 `IsBusy`/`BusyText`，两者 `ViewModelBase` 自带）。不要再手写遮罩 Grid；长操作在 VM 里先设 `BusyText` 再设 `IsBusy`。
- **弹窗双轨**：确认/提示/错误走 `ICustomDialogService`（DialogHost 弹层）；新增/编辑走插件自带模态 Window——`ShowDialog()` 前必须 `window.Owner = Application.Current.MainWindow`，XAML 用 `WindowStartupLocation="CenterOwner"`。
- **DataGrid**：不要显式 `Style="{StaticResource MaterialDesignDataGrid}"`——`App.xaml` 隐式样式已 `BasedOn` 它，并全局并入虚拟化 4 项 + `AutoGenerateColumns=False`/`GridLinesVisibility=Horizontal`/`BorderThickness=0`/背景/前景；页面只写差异属性（`ItemsSource`/`IsReadOnly`/`BorderThickness` 等）。**单元格模板已全局覆写**（MD 原模板的 ContentPresenter 不消费 `VerticalContentAlignment`，44px 行高下文本贴顶；覆写后 ContentPresenter 垂直居中、水平保持 Stretch 供编辑控件填满），文本/按钮列自动垂直居中，新页面无需处理。
- **主题按钮**：Raised 按钮统一用 `App.xaml` 的 `RaisedButton.Primary`（BasedOn `MaterialDesignRaisedButton`，`Brush.Primary` 深蓝底 + `Brush.OnPrimary` 白字；OnPrimary 语义键在主题文件定义为 `#FFFFFF`）。不要直接用 MD3 原生 Raised 键（浅色主题下灰白底、白字看不清）；**不要**用同名键 BasedOn 覆写 MD 样式——自引用会被静默置空丢模板，必须另起新键名。
- **Header 用户区**：右上角用户标识+退出按钮整体按 `CanLogout`（即 `Security:Enabled`）显隐，免登录时改显公司名（`CompanyName` 淡白字）；深色 Header 上的图标/文字用白色前景，用户标识底衬为 `PrimaryHueDarkBrush` 圆角 Border。**Sidebar 底部用户卡**同样按 `Security:Enabled` 显隐（`SidebarViewModel.IsUserCardVisible`），免登录不再露出 `anonymous`。
- **底部状态栏**（2026-07-29 起，2026-08-03 扩展）：`StatusBarView` 挂在两个布局的 HeaderView 之外（Standard/SinglePage 第三行），DataContext 继承布局的 `LayoutViewModel`（与 HeaderView 同模式，无独立 VM、无需 DI 注册）；左=设备在线 X/Y + 状态点（`DeviceStatusLevel`：ok/warn/error/none → Success/Warning/Error/Inactive）+ "CPU x% · 内存 xMB"（`ISystemMonitorService`，Layout 插件内实现，PerformanceCounter 整机 CPU，每 2s）+ 数据库连通（`select 1` 探测，每 30s），中=公司名，右=版本号（入口程序集 `InformationalVersion`）+ 当前时间。头部不再放时钟。
- **深色例外**（刻意不走浅色主题）：Splash 启动页（深色硬编码品牌页，仅显示中文软件名）；Login/ChangePassword 的蓝色 `ColorZone PrimaryMid` 横幅页头。其余窗口一律浅色主题键。

### 5.11 其他坑点

- **配置写回**：`ConfigurationHelper.UpdateAppSetting` 写入的是 `{BaseDirectory}/Configuration/appsettings.json`；临时文件+替换原子写入，IO/JSON/权限错误会**抛出异常**（配置文件不存在则静默返回），调用方需处理失败。**分层写回（2026-07-31 修复）**：配置按 `appsettings.json` + `appsettings.{Role}.json` 分层加载（角色文件优先），写回必须经 `ConfigurationHelper.ResolveTargetFileName` 按节选目标文件（角色文件含该节 → 角色文件；宿主经 `AppRuntime:RoleConfigFile` 暴露活动角色文件名），否则只存在于角色文件的节会被遮蔽、保存不生效。
- **SQLite**：启动前自动备份 `.db → .db.bak`（连同 -wal/-shm，失败仅警告）；已启用 WAL 等 PRAGMA 优化。
- **数据库配置键**：`Database:Provider`（SQLite/PostgreSQL）、`Database:SQLite:ConnectionString`、`Database:PostgreSQL:ConnectionString`（嵌套结构，不是扁平键）。
- **gRPC 配置键**：服务端 `Grpc:ServerPort`（默认 5000）；客户端 `Grpc:ServerUrl`、`Grpc:ClientId`、`Grpc:ClientName`。
- **Resilience 配置键**（扁平结构）：`Resilience:DatabaseRetryCount`、`Resilience:PlcRetryCount`、`Resilience:GrpcCircuitBreakerThreshold`、`Resilience:CircuitBreakerDurationSeconds`。管道 Key 常量：`ResiliencePipelineFactory.Keys.Database="Database-Retry"` / `Plc="PLC-Retry"` / `Grpc="Grpc-CircuitBreaker"`。三条管道在 `AddPlatformResilience` 中直接登记到 Registry（注册表自描述，不依赖工厂解析时机），不再有 `ResiliencePipeline.Empty` 瞬态注册；`FreeSqlRepository` 五个方法已套 `Database-Retry` 管道（构造参数 `ResiliencePipelineProvider<string>?` 可空，未注册韧性服务时退化为 Empty）。
- **文件名大小写**：`appsettings.server.json` 是小写（Linux 上与 `appsettings.Server.json` 不匹配，Windows 无碍）。
- **驱动 internal 类型的测试模式**：三个 PLC 插件通过 csproj 的 `InternalsVisibleTo("AP.Infra.Tests")` 开放 internal（地址对象/验证器）供测试；`AP.Infra.Tests` 的 TFM 为 `net8.0-windows`（引用 WPF 插件的需要）并直接引用三个 PLC 插件项目。新驱动照此办理。
- **崩溃日志**：全局异常写入 `logs/crash-yyyyMMdd.log`，致命异常 `Environment.Exit(1)`，不弹窗。
- **单实例**：`App.OnStartup` 用命名互斥体 `AP.SCAFFOLD.PLATFORM.RUNNING` 检查（非首实例弹提示退出；该互斥体同时供 Inno `AppMutex` 检测）；托盘重启给新进程传 `--restart`，新进程等待旧实例释放互斥体（最长 60s，含 `AbandonedMutexException` 处理）后再启动。
- **关闭流程**：`App.OnExit` 把优雅关闭放到线程池执行 + 15s 硬上限（禁止 UI 线程 sync-over-async，会卡死关闭）；`PluginLifecycleManager.StopPluginsAsync` 单插件停止有 5s 独立超时（超时/失败记错后继续其余插件）。注意：构建不会自动删除 `bin/Release/plugins` 里已从源码移除的插件目录，残留 DLL 仍会被加载——删除插件源码后需手动清理该目录。
- **报表 IHostedService**：见 5.6。
- **扫码枪断线重连**：已迁入统一 `ConnectionSupervisor`（2026-07-25，T3.4）：probe=端口枚举+句柄检查、connect=关残留句柄重开（5s/0s 参数）；`OpenAsync` 首开失败仍同步抛出（A 方案，有注释说明的例外）；数据通道与消费者只建一次，重连只涉及串口句柄、不重建通道。
- **LiveCharts2 走宿主共享库模式（2026-08-03 起）**：插件 XAML 引用第三方程序集时，BAML 引用经 Default ALC 解析，程序集必须在**宿主输出根目录**（同 MaterialDesign 既有模式）——宿主 `AP.Host.Desktop` 直接引用 `LiveChartsCore.SkiaSharpView.WPF`，`PluginLoadContext.SharedPrefixes` 含 `LiveChartsCore`/`SkiaSharp`/`OpenTK`，`CleanDuplicateLibs` 删除插件目录私有副本（含 libSkiaSharp 等原生库）。教训：rc6.1 强名引用曾在插件 ALC 内自洽，2.0.4 正式版取消强名（BAML 部分名引用）即报"找不到 LiveChartsCore.SkiaSharpView.WPF"——插件 XAML 新增第三方库时一律按共享库模式接入，勿依赖插件 ALC 私载。`SkiaSharp.Views.WPF` 仅 net462 资产（NU1701 警告，官方文档明确不影响 .NET 8 运行；如需消除可把 TFM 升为 `net8.0-windows10.0.19041`，暂不需要）。**两个必坑（2026-08-06 复现实验确证）**：① 必须在宿主启动早期调用一次 `LiveCharts.Configure(c => c.UseDefaults())`（`App.OnStartup`，注册 SkiaSharp 渲染后端+默认映射器+主题，2.0.4 不隐式初始化）；② **`Axis` 禁止设 `MinStep`**——数据范围小于 MinStep 时整个图表静默空白（无线/无轴/无图例无任何报错），时间轴用 `UnitWidth`+`Labeler` 即可。当前首页已移除趋势图（2026-08-06 框架通用化重构），上述基建为业务插件接入图表保留。
- **插件禁止注入/引用 Infra 具体类型（2026-08-06 实战教训）**：插件在独立 ALC 加载，Infra DLL 不在插件目录时会经 `PluginLoadContext` 根目录兜底**二次装载进插件 ALC**，与宿主注册类型不恒等 → DryIoc 自动瞬态化 → 拿到从未 Start 的新实例（实例：DashboardViewModel 注入 `TagAcquisitionEngine`/`LatestTagValueStore` 具体类型 → 采集徽标恒"全部停止"、趋势图空白）。需要 Infra 运行时状态时走契约层只读视图接口（`ILatestTagValueStore`/`ITagAcquisitionStatus`，见 5.13）。防护：`SharedPrefixes` 含 `AP.Infra`、`CleanDuplicateLibs` 删插件输出 `AP.Infra.*`。连带坑点：`SharedPrefixes` 新增前缀时，其公开签名里的第三方类型前缀也必须已共享（AP.Infra.Resilience 返回 `Polly.ResiliencePipeline`，故 `Polly` 同步共享，否则跨 ALC 签名不匹配抛 `MissingMethodException`）。

### 5.12 Device Runtime Model（设备运行时模型）

> 2026-07-25 起落地（阶段 1~3）。定位：全部设备（PLC/扫码枪/未来相机/MQTT）的统一运行时，协议无关。

- **组件**（`AP.Infra.Hardware/DeviceRuntime/`，契约在 `AP.Contracts.Hardware/DeviceRuntime/`）：
  - `DeviceConnectionState` 六态枚举（契约层）+ `DeviceConnectionStateMachine`：连接状态**唯一事实来源**（锁内迁移、锁外发 `Transitioned` 事件；全项目不允许再有第二份"当前连接状态"缓存）
  - `ConnectionSupervisor`：心跳+重连+监督自愈，纯事件源不识日志（`ConnectionSupervisorLogger` 为可拆消费者）不识 MediatR（`TransitionEventBridge` 声明式映射，桥接属于各驱动）
  - `IDevice`（Info/State/Transitioned/Connect/Disconnect，**无 IsConnected**、不含读写能力）+ `DeviceInfo`（预留 Group/Description）+ `DeviceType` 粗粒度三值（Plc/Scanner/Other，细分归 DriverType 字符串）
  - `IDeviceRegistry`：设备统一登记（DeviceId 大小写不敏感；`plc.main`、`scanner.{端口}`）；`DeviceStateChangedEvent` 统一状态事件（旧四个 PLC 连接事件并行保留，退役评估留阶段 5）
- **接入新设备类型**：实现 `IDevice`（或经适配器）+ DI 注册 `IDevice`，Bootstrapper 循环自动登记；连接管理一律复用 `ConnectionSupervisor`，**不允许再写第四套看门狗**。
- **启动时序约束（2026-07-26 踩坑修复）**：设备注册与点表加载**必须先于插件初始化**（Bootstrapper 1.0/1.1 段）——视图在插件初始化/Region 创建时即可能解析 `ITagTable`，而点表校验依赖注册表已填充。

### 5.13 Tag 系统（点名读写与采集）

> 2026-07-25 起落地（阶段 4）。定位：业务层**永远按逻辑点名**访问设备数据，不允许直接传协议地址（协议语法只存在于驱动内部）。

- **点表**：`bin/Release/Configuration/tags.json`（随宿主 `Configuration/tags.json` 复制）：`{ "Acquisition": {...}, "Tags": [...] }`；启动时全量校验（点名唯一/设备已注册/地址经 `IAddressValidator` 解析），任一非法即**中止启动**（快速失败，`DeviceConfigurationException`）；`Acquisition` 节为可选采集配置（`DefaultIntervalMs` 默认 1000 + `Overrides` 按点名覆盖），采集策略不属于 `TagDefinition`。
- **点表编辑（2026-07-31 起；热重载 2026-08-07 起）**：运行期改点表走"点表配置"菜单页（`AP.Plugin.TagConfiguration`），不要手改 JSON——保存前经 `ITagTableValidator`（与启动加载共用 `TagTableValidation`，规则/错误文案一致）全量校验，非法不落盘；保存为原子写入并写审计。**保存即热重载生效**：`ITagTableReloader`（契约层 `AP.Contracts.Hardware/DeviceRuntime/`）编排三步——`TagTable.Reload()`（重读文件+全量校验，失败保留旧表返回错误；成功锁内原子替换 `_tags`+`Acquisition` 快照）→ 采集引擎 `Restart()`（Stop+Start，`Acquisition` 配置改为 `Start` 时从 `ITagTable` 现取，引擎构造不再接收快照）→ `LatestTagValueStore.PruneExcept(当前点名集)`（清理已删点的残留值/版本）。热重载校验失败时保留旧表继续运行并弹窗提示（重启后生效）。注意：仅编辑页保存触发热重载，**手工改 tags.json 文件仍需重启**（无文件监视）。
- **读写**：注入 `ITagService` → `ReadAsync(name)` / `WriteAsync(name, value)`，返回 `TagValue(Value, Quality, Timestamp:DateTimeOffset, Version, Error)`；**通信失败返回 `Quality=Bad` 不抛异常**（设备未连接快速失败、类型不支持 Bad）；仅编程错误抛异常（点名不存在 `ArgumentException`、读写方向违规 `InvalidOperationException`、写入类型不匹配 `ArgumentException`）；地址解析在点表加载时完成（`ResolvedTag` 缓存 Address Object），读写零解析开销。
- **采集**：`TagAcquisitionEngine`（按生效间隔分组轮询、跳过只写点）→ `LatestTagValueStore`（Version 按点递增；订阅者读最新值不打设备）→ 变化检测（值或质量戳变化）→ `TagValueChangedEvent`（MediatR）/ `PrismTagValueChangedEvent`（UI）；设备状态走 `DeviceStateChangedEvent` / `PrismDeviceStateChangedEvent`。**UI 读采集状态/最新值走契约只读视图**（2026-08-06 起）：`ITagAcquisitionStatus.IsRunning`、`ILatestTagValueStore.Snapshot()`（均在 `AP.Contracts.Hardware/DeviceRuntime/`，DI 转发宿主单例）——禁止注入 Infra 具体类型（见 5.11 坑点）。
- **错误处理与日志规范**：新代码必须遵守 `docs/conventions/ERROR_HANDLING.md` 与 `LOGGING.md`（禁止裸 `Exception`、禁止 emoji、状态迁移记录法、通信字段结构化）。

### 5.14 打包与发布（框架依赖）

> 2026-07-29 起。面向外包现场，发布形态为 **win-x64 框架依赖**（体积小；现场初次装机装一次运行时，后续只发应用）。

- **两步流程**（顺序不可颠倒，publish 只复制插件目录不构建插件）：先 `dotnet build AP-Automation.Platform.slnx -c Release`，再 `dotnet publish platform/hosts/AP.Host.Desktop/AP.Host.Desktop.csproj -c Release -r win-x64 --self-contained false -p:AppendRuntimeIdentifierToOutputPath=true`（输出 `bin/Release/publish/`，约 26MB/227 文件；安装包约 7.7MB）。详见 `installer/README.md`。
- **现场需装两个 .NET 8 运行时**：桌面运行时 + ASP.NET Core 运行时（宿主编译期 `FrameworkReference Microsoft.AspNetCore.App`，缺失启动即失败）；`setup.iss` 安装时按注册表主版本精确检测并列出缺失项（仅装 9/10 拦截）。
- 宿主 csproj 仅固定 `PublishDir=$(BuildRoot)publish\`；**RID 与 self-contained 只走命令行**，且必须带 `-p:AppendRuntimeIdentifierToOutputPath=true`（否则 RID 构建产物混入 `bin/Release` 根，2026-07-29 实测导致开发版 exe 无法启动）。
- **测试产物隔离**：`Directory.Build.props` 中 `.Tests` 结尾的项目输出到 `bin/Tests/{项目名}/`，`bin/Release` 根目录不再有 TestPlatform/xunit/语言目录污染。
- `installer/Languages/ChineseSimplified.isl` 随仓库携带（官方发行版不含中文，要求 Inno ≥ 6.5）；`installer/Output/` 已 gitignore。
- **禁项（均实测踩坑）**：勿开 `PublishReadyToRun`（R2R 与 WPF 混合程序集 DirectWriteForwarder 冲突，启动即 TypeLoadException）；**暂勿自包含发布**（`PluginLoadContext` 可回收上下文装载 DirectWriteForwarder 崩溃，框架依赖因该文件不在应用目录而规避；如需自包含须先让框架程序集回退 `AssemblyLoadContext.Default`）；勿 Trim/NativeAOT；单文件发布无收益。

---

## 6. 文档索引

| 文档 | 用途 |
|------|------|
| `README.md` | 项目概览、快速开始、技术栈 |
| `docs/ARCHITECTURE.md` | 分层架构、设计模式、数据流 |
| `docs/EVOLUTION_PLAN.md` | **架构演进主控文档**（现状诊断、任务执行铁律、全部任务完成记录、问题停车场） |
| `docs/conventions/ERROR_HANDLING.md` | 错误处理与 Result 使用规范（分层规则、ErrorCode 规划、反模式清单） |
| `docs/conventions/LOGGING.md` | 日志使用规范（级别约定、消息模板、防刷屏、应用日志 vs 审计） |
| `docs/conventions/LAYERING.md` | 设备访问分层防线（UI/业务只许 ITagService+IDeviceRegistry+事件；地址纪律） |
| `docs/conventions/DEPENDENCIES.md` | 项目依赖方向规范（目标规则、现状例外登记、评审清单） |
| `docs/GETTING_STARTED.md` | 环境准备、配置、插件开发示例 |
| `docs/TESTING.md` | 测试规范与运行方式 |
| `docs/PROJECT_STATUS.md` | 项目状态与工作计划 |
| `docs/IMPROVEMENT_PLAN.md` | 五维度差距分析与改进计划（稳定/复用/安全/可持续/通用） |
| `installer/README.md` | 安装包构建说明（框架依赖两步流程、现场运行时要求、体积数据、禁项清单） |
| `CHANGELOG.md` | 版本变更日志 |

---

## 7. 如何继续工作

1. 先 `dotnet build AP-Automation.Platform.slnx -c Release` 确认当前代码可编译。
2. 运行三个测试项目确认 475 个测试通过。
3. 查看 `docs/PROJECT_STATUS.md` 了解当前进度和待办；**架构演进任务以 `docs/EVOLUTION_PLAN.md` 为准**（含任务执行铁律：一次一个 Task、单独提交可回滚、不改公开 API 除非必须、提交后回填状态）。
4. 处理用户请求前，先通过 `git status` 确认当前工作区状态。
5. 涉及多文件修改时，优先使用最小改动，保持与现有代码风格一致。

---

**最后更新**: 2026-08-07
