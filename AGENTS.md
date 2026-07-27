# AP-Scaffold — AI 协作交接文档

> 本文档面向 Kimi / Cursor / Copilot 等 AI 编码助手。当你在一个新 Session 中接手本项目时，请先完整阅读本文件，再继续处理用户请求。
> 本文档以**实际代码**为准整理（已核对全部 36 个项目）。

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

- **当前分支**: `main`
- **最近提交主题**: 架构演进阶段 0~4 全部完成（`docs/EVOLUTION_PLAN.md` 主控）：规范先行（错误处理/日志规范、三驱动日志清理、gRPC 封存标注）→ 连接监督收敛（`DeviceConnectionStateMachine`+`ConnectionSupervisor`+`TransitionEventBridge`，三套复制看门狗消除）→ 地址对象化（`McAddress`/`S7Address`/`FinsAddress`+`IAddressValidator`）→ Device 抽象（`IDevice`/`IDeviceRegistry`/PLC+扫码枪接入/统一状态事件）→ Tag 系统（点表 `tags.json`/`ITagService`/采集引擎/最新值表/变化事件/Dashboard 真实数据）；启动时序修复（设备注册必须先于插件初始化）
- **工作区状态**: 干净（开始新任务前请再次 `git status` 确认）

### 2.2 已完成功能

- 插件化核心框架（加载、隔离上下文、生命周期、14 态状态机、MediatR 事件总线）
- **声明式导航贡献者模式**（`INavigationContributor`，见 5.1）
- **设置贡献者模式**（`ISettingsContributor`，见 5.2）
- 三菱 / 西门子 / 欧姆龙 PLC 驱动与统一切换机制（`IPlcDriverFactory` + `ActivePlcService`）
- 串口扫码枪硬件驱动
- gRPC Server/Client 分布式通信（proto 契约 + StreamBroadcaster 广播）❄ 已封存（代码保留不维护，文件头有标注）
- 安全模块：本地用户/角色/权限（12 个种子权限、3 个默认角色）、登录、强制改密、审计日志
- 用户管理、角色管理、审计日志可视化、配方管理骨架、报表中心骨架
- 全浅色统一视觉主题（`Industrial.Teal.MD3.xaml`，文件名保留但内容已是浅色）
- 系统托盘、启动画面（Splash）、全局异常崩溃日志
- **Device Runtime Model（设备运行时模型，见 5.12）**：协议无关连接状态机 + 统一连接监督器（全部 4 个硬件驱动共享）、`IDevice`/`IDeviceRegistry` 设备抽象、统一状态事件 `DeviceStateChangedEvent`
- **Tag 系统（见 5.13）**：点表（`Configuration/tags.json`）、`ITagService` 按点名读写（质量戳结果）、采集引擎 + 最新值表 + 变化事件订阅
- **横切规范文档**：`docs/conventions/ERROR_HANDLING.md`（Result/异常分层规则）、`docs/conventions/LOGGING.md`（日志级别/模板/防刷屏）

### 2.3 活跃问题 / 待办

> **范围决策（2026-07-21）**：当前仅聚焦 **Standalone 单机模式 + SQLite 数据库**。Server/Client（gRPC 技术栈）与 PostgreSQL/SQL Server 支持**冻结**——代码保留但不维护、不验证、不投入改进；`AppRole.Server`/`AppRole.Client` 保留但视为"未支持"。除非用户明确要求解冻，不要在这些方向做改动。详见 `docs/IMPROVEMENT_PLAN.md` 1.3 节与第八章末「冻结事项清单」。
>
> **架构演进主控文档**：`docs/EVOLUTION_PLAN.md`——阶段 0~4（规范/连接监督/地址对象/Device 抽象/Tag 系统）已完成，阶段 5（业务迁移与防线）待启动。所有架构级改动必须遵守其中的"任务执行铁律"（一次一个 Task、单独提交、可回滚、不改公开 API 除非必须）。

- [ ] 报表中心接入更多真实业务数据提供者（2026-07-28 已落地首个：**操作审计日报 AuditDaily**，数据源=审计日志；Tag 历史报表待 Tag 值持久化议题）
- [ ] 配方管理完善校验与版本历史；`IRecipeManager.SwitchAsync` 的事件发布仍是 TODO
- [ ] 审计日志导出（PLC 写操作与配置修改审计已于 2026-07-22 接入）
- [x] 西门子 PLC 协议支持
- [x] 欧姆龙 PLC 协议支持（2026-07-24，`AP.Plugin.Plc.Omron`，FINS/TCP）
- [ ] OpenTelemetry 可观测性
- [x] Dashboard 接入真实数据（2026-07-26，T4.6：设备状态/采集点/Tag 变化/真实事件流）
- [ ] `RequiresCapabilitiesAttribute` 目前仅有声明，无运行时强制检查
- [ ] 停车场（`EVOLUTION_PLAN.md`）：`IPlcBatchReadWrite` 带类型批量契约统一（采集引擎批量合并的前置）；设备掉线/恢复是否进审计

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
│   ├── plugins/                          # 插件（12 个）
│   │   ├── hardware/
│   │   │   ├── AP.Plugin.Plc.Mitsubishi  # Priority=20，Server|Standalone
│   │   │   ├── AP.Plugin.Plc.Siemens     # Priority=21，Server|Standalone
│   │   │   ├── AP.Plugin.Plc.Omron       # Priority=22，Server|Standalone
│   │   │   └── AP.Plugin.Scanner         # Priority=20，Client|Standalone
│   │   ├── business/
│   │   │   └── AP.Plugin.DeviceConfiguration  # Priority=100，ISettingsContributor（扫码枪配置）
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
│   └── tests/                            # xUnit 测试（39 个测试文件 / 425 个测试）
│       ├── AP.Core.Tests                 # 9 文件 / 130 测试
│       ├── AP.Shared.Tests               # 4 文件 / 48 测试
│       └── AP.Infra.Tests                # 26 文件 / 247 测试（含 DeviceRuntime/驱动地址对象全套）
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
```

---

## 5. 核心约定与常见坑点

### 5.1 导航贡献者模式（新插件加菜单的正确方式）

- 接口定义在 `AP.Shared.PluginSDK/Navigation/`：`INavigationContributor.GetMenuItems()` 返回 `NavigationMenuItem`（`Label` / `IconKind` / `NavigationTarget` / `Order` / `Permission?` / `Category?` / `IsDefault`）。
- 插件主类实现该接口即可出现在 Sidebar，**无需**手动操作 SidebarViewModel；但仍需在 `InitializeAsync` 中把视图 `RegisterViewWithRegion("ContentRegion", ...)`（按权限条件）。
- Host 在 `Bootstrapper` 桥接 DryIoc 时，对实现了 `INavigationContributor` 的插件实例额外 `RegisterInstance(typeof(INavigationContributor), instance)`。
- `SidebarViewModel` 注入 `IEnumerable<INavigationContributor>`，用 `NavigationMenuItemBuilder.Build(contributors, identityService.HasPermission, defaultTarget, visibilityFilter)` 构建菜单：按 `NavigationTarget` 去重（取最小 Order）→ 排序 → 权限过滤 → 白名单过滤 → 无默认项时取第一个可见项。
- 默认导航延迟到 `Dispatcher.BeginInvoke(Background)` 执行（确保 UI 就绪后再选中）。
- 相关配置键：
  - `AppConfiguration:DefaultNavigationTarget`（当前 `"DashboardView"`）
  - `AppConfiguration:NavigationWhenSecurityDisabled`（字符串数组白名单；`Security:Enabled=false` 时只显示这些 Target，当前为 `DashboardView / SettingsShellView / RecipeListView / ReportListView`）
- 当前菜单 Order：仪表板 100 → 系统配置 1000 → 配方管理 2000 → 报表中心 3000 → 用户管理 4000 → 角色管理 4100 → 审计日志 4200。

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
- **Header 用户区**：右上角用户标识+退出按钮整体按 `CanLogout`（即 `Security:Enabled`）显隐，免登录场景不显示；深色 Header 上的图标/文字用白色前景，用户标识底衬为 `PrimaryHueDarkBrush` 圆角 Border。
- **深色例外**（刻意不走浅色主题）：Splash 启动页（深色硬编码品牌页，仅显示中文软件名）；Login/ChangePassword 的蓝色 `ColorZone PrimaryMid` 横幅页头。其余窗口一律浅色主题键。

### 5.11 其他坑点

- **配置写回**：`ConfigurationHelper.UpdateAppSetting` 写入的是 `{BaseDirectory}/Configuration/appsettings.json`；临时文件+替换原子写入，IO/JSON/权限错误会**抛出异常**（配置文件不存在则静默返回），调用方需处理失败。
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
- **扫码枪断线重连**：已迁入统一 `ConnectionSupervisor`（2026-07-26，T3.4）：probe=端口枚举+句柄检查、connect=关残留句柄重开（5s/0s 参数）；`OpenAsync` 首开失败仍同步抛出（A 方案，有注释说明的例外）；数据通道与消费者只建一次，重连只涉及串口句柄、不重建通道。

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
- **读写**：注入 `ITagService` → `ReadAsync(name)` / `WriteAsync(name, value)`，返回 `TagValue(Value, Quality, Timestamp:DateTimeOffset, Version, Error)`；**通信失败返回 `Quality=Bad` 不抛异常**（设备未连接快速失败、类型不支持 Bad）；仅编程错误抛异常（点名不存在 `ArgumentException`、读写方向违规 `InvalidOperationException`、写入类型不匹配 `ArgumentException`）；地址解析在点表加载时完成（`ResolvedTag` 缓存 Address Object），读写零解析开销。
- **采集**：`TagAcquisitionEngine`（按生效间隔分组轮询、跳过只写点）→ `LatestTagValueStore`（Version 按点递增；订阅者读最新值不打设备）→ 变化检测（值或质量戳变化）→ `TagValueChangedEvent`（MediatR）/ `PrismTagValueChangedEvent`（UI）；设备状态走 `DeviceStateChangedEvent` / `PrismDeviceStateChangedEvent`。
- **错误处理与日志规范**：新代码必须遵守 `docs/conventions/ERROR_HANDLING.md` 与 `LOGGING.md`（禁止裸 `Exception`、禁止 emoji、状态迁移记录法、通信字段结构化）。

---

## 6. 文档索引

| 文档 | 用途 |
|------|------|
| `README.md` | 项目概览、快速开始、技术栈 |
| `docs/ARCHITECTURE.md` | 分层架构、设计模式、数据流 |
| `docs/EVOLUTION_PLAN.md` | **架构演进主控文档**（现状诊断、任务执行铁律、7 阶段任务清单与完成记录、问题停车场） |
| `docs/conventions/ERROR_HANDLING.md` | 错误处理与 Result 使用规范（分层规则、ErrorCode 规划、反模式清单） |
| `docs/conventions/LOGGING.md` | 日志使用规范（级别约定、消息模板、防刷屏、应用日志 vs 审计） |
| `docs/GETTING_STARTED.md` | 环境准备、配置、插件开发示例 |
| `docs/TESTING.md` | 测试规范与运行方式 |
| `docs/PROJECT_STATUS.md` | 项目状态与工作计划 |
| `docs/IMPROVEMENT_PLAN.md` | 五维度差距分析与改进计划（稳定/复用/安全/可持续/通用） |
| `CHANGELOG.md` | 版本变更日志 |

---

## 7. 如何继续工作

1. 先 `dotnet build AP-Automation.Platform.slnx -c Release` 确认当前代码可编译。
2. 运行三个测试项目确认 425 个测试通过。
3. 查看 `docs/PROJECT_STATUS.md` 了解当前进度和待办；**架构演进任务以 `docs/EVOLUTION_PLAN.md` 为准**（含任务执行铁律：一次一个 Task、单独提交可回滚、不改公开 API 除非必须、提交后回填状态）。
4. 处理用户请求前，先通过 `git status` 确认当前工作区状态。
5. 涉及多文件修改时，优先使用最小改动，保持与现有代码风格一致。

---

**最后更新**: 2026-07-26
