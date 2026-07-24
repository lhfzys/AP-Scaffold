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
- **最近提交主题**: UI 一致性第二批次（DataGrid 单元格模板覆写垂直居中、`RaisedButton.Primary` 深蓝底白字按钮、Header 用户区随 `Security:Enabled` 显隐、时间列加宽）；UI 一致性五批次（Dashboard 移除快捷入口、编辑窗 Owner 居中、视图级权限防线统一、LoadingSpinner 遮罩收敛+忙碌反馈、DataGrid 全局样式、Splash 去英文）；Security 默认关闭免登录（审计独立保留）；阶段二加固（韧性管道接线、`IReportDataProvider` 移契约层、PLC 写操作+配置修改审计、PLC 看门狗监督+Scanner 断线重连、单实例 Mutex+托盘重启交接、关闭卡死修复）
- **工作区状态**: 干净（开始新任务前请再次 `git status` 确认）

### 2.2 已完成功能

- 插件化核心框架（加载、隔离上下文、生命周期、14 态状态机、MediatR 事件总线）
- **声明式导航贡献者模式**（`INavigationContributor`，见 5.1）
- **设置贡献者模式**（`ISettingsContributor`，见 5.2）
- 三菱 / 西门子 PLC 驱动与统一切换机制（`IPlcDriverFactory` + `ActivePlcService`）
- 串口扫码枪硬件驱动
- gRPC Server/Client 分布式通信（proto 契约 + StreamBroadcaster 广播）
- 安全模块：本地用户/角色/权限（12 个种子权限、3 个默认角色）、登录、强制改密、审计日志
- 用户管理、角色管理、审计日志可视化、配方管理骨架、报表中心骨架
- 全浅色统一视觉主题（`Industrial.Teal.MD3.xaml`，文件名保留但内容已是浅色）
- 系统托盘、启动画面（Splash）、全局异常崩溃日志

### 2.3 活跃问题 / 待办

> **范围决策（2026-07-21）**：当前仅聚焦 **Standalone 单机模式 + SQLite 数据库**。Server/Client（gRPC 技术栈）与 PostgreSQL/SQL Server 支持**冻结**——代码保留但不维护、不验证、不投入改进；`AppRole.Server`/`AppRole.Client` 保留但视为"未支持"。除非用户明确要求解冻，不要在这些方向做改动。详见 `docs/IMPROVEMENT_PLAN.md` 1.3 节与第八章末「冻结事项清单」。

- [ ] 报表中心接入真实业务数据提供者（目前仅 `SampleReportDataProvider` 示例）
- [ ] 配方管理完善校验与版本历史；`IRecipeManager.SwitchAsync` 的事件发布仍是 TODO
- [ ] 审计日志导出（PLC 写操作与配置修改审计已于 2026-07-22 接入）
- [x] 西门子 PLC 协议支持
- [ ] 欧姆龙 PLC 协议支持（`PlcOptions.DriverType` 注释已预留 "Omron"）
- [ ] OpenTelemetry 可观测性
- [ ] Dashboard 仪表盘目前为硬编码占位数据（`DashboardViewModel.LoadPlaceholderData`，已加 TODO(sample) 标注）
- [ ] `RequiresCapabilitiesAttribute` 目前仅有声明，无运行时强制检查

---

## 3. 目录结构速查

```
AP-Scaffold/
├── platform/
│   ├── core/AP.Core                      # 插件框架、生命周期、状态机、事件总线
│   ├── contracts/                        # 接口与事件契约
│   │   ├── AP.Contracts.Core             # OperationResult、ErrorCode、AppInitializedEvent（Prism PubSubEvent）
│   │   ├── AP.Contracts.Hardware         # IPlcService、IPlcDriverFactory、PlcOptions、设备事件
│   │   ├── AP.Contracts.Communication    # 仅 proto 文件（automation_gate.proto / common.proto）
│   │   ├── AP.Contracts.System           # ILoginService、ISettingsDialogService、ISystemMonitorService
│   │   ├── AP.Contracts.Security         # IIdentityService、IAuditService、用户/角色/权限模型
│   │   ├── AP.Contracts.Recipe           # IRecipeManager（注意：不是 IRecipeService）
│   │   └── AP.Contracts.Report           # IReportCenterService、ReportArchiveDto
│   ├── infra/                            # 基础设施实现
│   │   ├── AP.Infra.Database             # FreeSql (SQLite/PostgreSQL)，UseAutoSyncStructure(false)
│   │   ├── AP.Infra.Grpc                 # gRPC Server/Client、StreamBroadcaster、LoggingInterceptor
│   │   ├── AP.Infra.Hardware             # PlcDriverRegistry、ActivePlcService（统一 IPlcService 代理）
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
│   └── tests/                            # xUnit 测试（22 个测试文件 / 237 个测试）
│       ├── AP.Core.Tests                 # 9 文件 / 130 测试
│       ├── AP.Shared.Tests               # 4 文件 / 48 测试
│       └── AP.Infra.Tests                # 9 文件 / 59 测试
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
  `Plc:DriverType`（`Mitsubishi` / `Siemens`，预留 `Omron`）、`Plc:IpAddress`、`Plc:Port`、`Plc:Timeout`、`Plc:Model`（三菱 `Qna_3E`；西门子 `S7_1200` 等）、`Plc:HeartbeatAddress`。
- 各 PLC 插件注册 `IPlcDriverFactory`，`AP.Infra.Hardware.ActivePlcService`（懒加载代理）按 `DriverType` 从 `PlcDriverRegistry` 取工厂创建真实驱动并转发 `IPlcService` / `IPlcBatchReadWrite` 调用。
- **PLC 写操作审计**：DI 中 `IPlcService` 实际解析为 `AuditingPlcServiceDecorator`（包装 `ActivePlcService`），`WriteAsync`/`WriteBatchAsync` 自动写审计日志（`ManualControl`，含地址/值/结果；操作人取 `IIdentityService.CurrentUser`，未登录记 `system`，Security 禁用恒 `anonymous`）；审计/身份服务未注册时降级不审计，审计失败不影响写入。读操作与连接管理不审计。
- **看门狗监督**：三菱/西门子驱动的看门狗由监督者循环托管（`StartWatchdog` → `RunWatchdogLoopAsync`），循环异常退出 5s 后自动重启，仅取消时真正退出；心跳读到已释放客户端（`ObjectDisposedException`，重连换客户端竞态）判定掉线走重连分支，不再退出看门狗。
- `PlcDriverRegistry` 为单例，**首次解析时从 DI 收集所有 `IPlcDriverFactory`**（`AddPlcHardware` 中的工厂委托完成，2026-07-22 修复了注册表恒为空的缺陷）；不要在插件里手动 `new PlcDriverRegistry()` 或调 `Register`。
- 新增品牌只需实现 `IPlcDriverFactory` 并注册到 DI，业务代码无需修改；插件的 `StartAsync`/`StopAsync` 需按 `IsActiveDriver()` 门控（仅激活品牌发起/断开连接，参考三菱/西门子插件），避免多品牌插件重复连接同一 `ActivePlcService` 代理。
- 西门子地址格式与三菱不同，业务插件中的地址应通过配置或参数传入，避免硬编码。
- 系统设置中已有 PLC 配置编辑页（`PlcConfigurationContributor`，DriverType 切换自动填默认值，RequiresRestart）。

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
- **崩溃日志**：全局异常写入 `logs/crash-yyyyMMdd.log`，致命异常 `Environment.Exit(1)`，不弹窗。
- **单实例**：`App.OnStartup` 用命名互斥体 `AP.SCAFFOLD.PLATFORM.RUNNING` 检查（非首实例弹提示退出；该互斥体同时供 Inno `AppMutex` 检测）；托盘重启给新进程传 `--restart`，新进程等待旧实例释放互斥体（最长 60s，含 `AbandonedMutexException` 处理）后再启动。
- **关闭流程**：`App.OnExit` 把优雅关闭放到线程池执行 + 15s 硬上限（禁止 UI 线程 sync-over-async，会卡死关闭）；`PluginLifecycleManager.StopPluginsAsync` 单插件停止有 5s 独立超时（超时/失败记错后继续其余插件）。注意：构建不会自动删除 `bin/Release/plugins` 里已从源码移除的插件目录，残留 DLL 仍会被加载——删除插件源码后需手动清理该目录。
- **报表 IHostedService**：见 5.6。
- **扫码枪断线重连**：`SerialPortScannerService` 订阅 `ErrorReceived`（标记重连）+ 5s 周期重连监控（`GetPortNames` 检测 USB 拔出→关闭残留句柄等待重插；设备恢复或出错后自动重开）；数据通道与消费者只建一次，重连只涉及串口句柄、不重建通道；初始化失败由监控持续重试。

---

## 6. 文档索引

| 文档 | 用途 |
|------|------|
| `README.md` | 项目概览、快速开始、技术栈 |
| `docs/ARCHITECTURE.md` | 分层架构、设计模式、数据流 |
| `docs/GETTING_STARTED.md` | 环境准备、配置、插件开发示例 |
| `docs/TESTING.md` | 测试规范与运行方式 |
| `docs/PROJECT_STATUS.md` | 项目状态与工作计划 |
| `docs/IMPROVEMENT_PLAN.md` | 五维度差距分析与改进计划（稳定/复用/安全/可持续/通用） |
| `CHANGELOG.md` | 版本变更日志 |

---

## 7. 如何继续工作

1. 先 `dotnet build AP-Automation.Platform.slnx -c Release` 确认当前代码可编译。
2. 运行三个测试项目确认 237 个测试通过。
3. 查看 `docs/PROJECT_STATUS.md` 了解当前进度和待办。
4. 处理用户请求前，先通过 `git status` 确认当前工作区状态。
5. 涉及多文件修改时，优先使用最小改动，保持与现有代码风格一致。

---

**最后更新**: 2026-07-24
