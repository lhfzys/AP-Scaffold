# Changelog

本文件记录 AP-Scaffold 项目所有重要变更。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，
版本管理遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

---

## [Unreleased]

### 阶段一排雷（2026-07-22）

修复：

- **报表后台任务不运行**：`ReportScheduler` / `ReportCleanupService` 由 `Bootstrapper` 显式启动，恢复定时归档/定期清理；注册改为"单例 + `AddHostedService` 转发"，消除双注册多实例隐患
- **启动异常卡死 Splash**：`OnInitialized` 最外层异常兜底关闭 Splash 并弹出错误提示
- **主题缺键**：补齐 `Brush.Overlay.Background`，修复 `LoadingSpinner` 使用处潜在的 XamlParseException
- **配置写回静默失败**：`ConfigurationHelper.UpdateAppSetting` 改为临时文件+替换原子写入，IO/JSON/权限错误如实抛出（设置中心可正确提示保存失败）
- **安装包升级覆盖现场配置**：`appsettings*.json` 改为仅首次安装写入；.NET 运行时检测改为 8.x 主版本精确匹配；AppMutex 防止应用运行中安装
- **插件语义未执行**：落地 `Required`/`Dependencies`/重复 ID——重复 ID 全部拒绝加载并中止启动；依赖缺失拒绝加载并级联；Required 插件失败中止启动并明确提示
- **业务残留**：移除气密示例插件 `AP.Plugin.AirtightnessCheck`；标题/软件名/工位名中性化；Dashboard 占位数据加 TODO 标注
- **构建警告清零**：修复全部 Nullable 警告（全量 Rebuild 0 警告 0 错误）

变更：

- 插件元数据审计：11 个功能/硬件插件改为 `Required = false`（仅 Login/Layout 保持必需），外设缺失或功能插件故障不再中止系统启动
- 范围决策：仅聚焦 Standalone + SQLite，Server/Client（gRPC）与 PostgreSQL/SQL Server 冻结，README/GETTING_STARTED/AGENTS 已同步标注
- 测试规模：18 个测试文件 / 222 个测试（新增 `PluginGraphValidatorTests` 7 例 + `ConfigurationHelperTests` 2 例）

### 新增

- **安全模块**：新增 `AP.Contracts.Security` / `AP.Infra.Security`
  - 本地用户/角色/权限体系（`IIdentityService`、`IUserRepository`、`IPasswordHasher`）
  - PBKDF2 + 随机盐值密码哈希
  - 审计日志服务 `IAuditService`
  - 启动时自动初始化默认角色、权限和 `admin` 账号
- **配方管理**：新增 `AP.Contracts.Recipe` / `AP.Infra.Recipe`
  - 配方增删改查、版本控制、默认配方、配方切换
  - 首次启动自动创建 `DEFAULT` 默认配方
- **启动画面**：`SplashWindow` 显示启动进度，各初始化阶段实时更新
- **系统托盘**：`TrayIconManager` 支持最小化到托盘、显示主窗口、重启、退出
- **安装包**：新增 `installer/setup.iss`（Inno Setup 脚本）及构建说明
- **全局异常保护**：移除弹窗，改为仅写入崩溃日志 `logs/crash-yyyyMMdd.log`
- **API 文档**：新增 `docs/ARCHITECTURE.md` 架构设计文档
  - 完整的分层架构概览与调用链说明
  - 核心框架（Core）各模块详细文档：插件框架、生命周期、状态机、能力声明、事件总线
  - 基础设施层各模块详细文档：数据库、gRPC、日志、容错、报表
  - 启动宿主启动流程图、运行时硬件通信数据流
  - 扩展点文档：添加新插件和新基础设施模块的步骤

- **测试文档**：新增 `docs/TESTING.md` 测试指南
  - 测试技术栈说明（xUnit + NSubstitute + FluentAssertions）
  - 测试编写规范（AAA 模式、命名约定、断言风格）
  - 各模块测试指南（状态机、生命周期、插件接口、事件总线、Attribute）
  - 测试运行命令、CI/CD 集成示例、覆盖率目标

- **状态机增强**：新增 `Frozen`（已冻结）和 `Deprecated`（已废弃）状态
  - 完整的状态枚举定义（14 个状态，值 0-13）
  - 对应更新 `StateTransitionValidator` 转换规则
- **登录认证插件**：新增 `AP.Plugin.Login`
  - 启动时弹出登录窗口（`Security:Enabled=true`）
  - 默认账号 `admin/admin123` 首次登录强制改密
  - 主界面顶部显示当前用户、支持退出登录后重新登录
  - 登录/登出/改密记录审计日志
  - `ILoginService` 接口定义在 `AP.Contracts.System` 中，宿主与插件解耦
- **用户管理插件**：新增 `AP.Plugin.UserManagement`
  - 用户列表、搜索、新增/编辑/删除/重置密码
  - 仅对拥有 `user.manage` 权限的用户可见
  - 用户操作（增删改、重置密码）记录审计日志
- **布局优化**：启用 `SidebarRegion` 作为系统管理导航菜单
  - 左侧边栏显示系统配置、用户管理、角色管理（预留）、审计日志（预留）入口
  - 用户管理入口按 `user.manage` 权限动态显示/隐藏
- **角色管理插件**：新增 `AP.Plugin.RoleManagement`
  - 角色列表、权限分配、新增/编辑/删除
  - 仅对拥有 `role.manage` 权限的用户可见
  - 角色操作记录审计日志
- **审计日志可视化插件**：新增 `AP.Plugin.AuditLog`
  - 审计日志列表查询、筛选、分页
  - 仅对拥有 `audit.view` 权限的用户可见
- **配方管理插件**：新增 `AP.Plugin.RecipeManagement`
  - 配方列表、编辑、参数维护、设为默认、切换配方
  - 按 `recipe.view` / `recipe.edit` / `recipe.switch` 控制界面
- **报表中心插件**：新增 `AP.Contracts.Report` / `AP.Plugin.ReportCenter`
  - 报表归档查询、手动生成、打开、导出
  - 按 `report.view` 权限显示
- **PLC 驱动抽象与西门子支持**
  - 新增 `AP.Contracts.Hardware` 抽象：`IPlcDriverFactory`、`PlcOptions`
  - 新增 `AP.Infra.Hardware`：`PlcDriverRegistry`、`ActivePlcService`
  - 新增 `AP.Plugin.Plc.Siemens` 西门子 PLC 驱动插件（基于 IoTClient.SiemensClient）
  - 改造 `AP.Plugin.Plc.Mitsubishi`：由直接注册 `IPlcService` 改为注册 `IPlcDriverFactory`
  - `AP.Host.Desktop` 通过 `AddPlcHardware` 统一注册 `IPlcService`
  - 业务代码通过统一 `IPlcService` 无感知切换三菱/西门子/欧姆龙
  - 新增 `AP.Plugin.SystemSettings` 统一 PLC 配置编辑器
  - 移除三菱插件专用的 `MitsubishiPlcConfigurationContributor`，统一由系统设置中的 PLC 配置管理
  - `appsettings.json` 新增 `Plc` 配置节
- **用户/角色管理视图修复**
  - `RoleListView` 改为构造函数注入 `RoleListViewModel`，移除 `AutoWireViewModel`
  - 统一所有插件视图采用构造函数注入 ViewModel 模式
  - `UserManagementPlugin` / `RoleManagementPlugin` 按 `Security:Enabled` 和当前权限条件注册 Region 视图
- **报表初始化修复**
  - 在 `AP.Host.Desktop` 中直接引用 `AP.Infra.Report` 和 `AP.Contracts.Report`，解决启动时 `ReflectionTypeLoadException`
  - 在 `Bootstrapper.OnInitialized` 中手动调用 `ReportDatabaseInitializer.StartAsync`，解决 `report_archives` 表缺失
- **声明式导航贡献者模式**
  - 新增 `AP.Shared.PluginSDK/Navigation/`：`INavigationContributor`、`NavigationMenuItem`、`NavigationMenuItemBuilder`
  - 插件实现接口即出现在 Sidebar，菜单统一去重/排序/按权限过滤
  - `Bootstrapper` 桥接 DryIoc 时自动注册插件实例的 `INavigationContributor` 接口
  - 支持 `AppConfiguration:DefaultNavigationTarget` 指定默认页，默认选中延迟到 UI 就绪后导航
  - `Security:Enabled=false` 时按 `AppConfiguration:NavigationWhenSecurityDisabled` 白名单过滤菜单，安全类插件跳过视图注册
- **设置贡献者模式**
  - 新增 `ISettingsContributor` / `ISettingsEditorViewModel`（`AP.Shared.PluginSDK/Configuration/`）
  - 系统配置中心自动收集贡献者按分类分组，统一校验/备份/写回
  - `AP.Plugin.DeviceConfiguration` 通过该模式提供"扫码枪配置"设置页
- **UI 主题与修复**
  - `Industrial.Teal.MD3.xaml` 重构为全浅色 Material Design 3 主题（主色深蓝 `#1E3A5F`、强调色青蓝 `#0891B2`）
  - 用户管理“角色”列、角色管理“权限”列的 Chip 文字颜色由白色改为 `MaterialDesign.Brush.OnSurface`，解决浅色背景下看不清的问题
  - 修复扫码枪"换行符"默认值在设置页显示为空的问题
- **文档更新**
  - 新增 `AGENTS.md`（AI 交接文档）
  - 新增 `docs/PROJECT_STATUS.md`（项目状态与工作计划）
  - 2026-07-21：全面核对实际代码（36 个项目），重写 `AGENTS.md`、`README.md`、`docs/ARCHITECTURE.md`、`docs/GETTING_STARTED.md`、`docs/PROJECT_STATUS.md`、`docs/TESTING.md`
  - 修正：权限清单（12 个种子权限）、导航/设置贡献者模式、统一 `Plc` 配置节、`Resilience` 扁平配置键、gRPC `ServerUrl` 配置键、数据库嵌套配置结构、测试数量（17 文件 / 213 测试）、状态机 14 态、移除不存在的 Toast 组件说明

### 变更

- 初始项目结构搭建完成
- 完善测试项目结构：AP.Core.Tests、AP.Shared.Tests、AP.Infra.Tests
- 测试覆盖核心框架关键路径
- 修复 `PluginLifecycleManager.RegisterPlugins` 未按优先级排序的问题
- 修复 `ConfigurationHelper.UpdateAppSetting` 空 section 未抛异常的问题
- 修复测试项目 CPM 版本管理配置不一致的问题
- 安全模块改为可选：`Security:Enabled` 配置开关，关闭时跳过用户/角色/权限表初始化并注入匿名实现
- 配置界面改为独立模态弹窗：`SettingsDialogWindow` + `ISettingsDialogService`，替代原右侧抽屉模式
- `UserInfo` 增加 `MustChangePassword` 属性，`IdentityService` 改密成功后自动清除该标志
- `ViewModelBase` 增加 `RequestClose` 事件，统一模态窗口关闭机制
- 修复 `ConfigurationHelperTests` 使用 .NET 9 才有的 `Type.IsStatic` 导致 net8.0 编译失败的问题

### 技术栈

- .NET 8（目标框架），使用 .NET 10 SDK 构建
- WPF + Prism + DryIoc
- FreeSql (SQLite / PostgreSQL)
- gRPC (ASP.NET Core gRPC)
- MediatR (事件总线)
- Serilog (结构化日志)
- Polly (容错策略)
- MiniExcel (报表生成)
- xUnit + NSubstitute + FluentAssertions (测试)

---

## [0.1.0] - 2026-07-13

### 新增

- **项目初始化**：创建解决方案 `AP-Automation.Platform.slnx`
- **核心框架 (AP.Core)**
  - 插件框架：`IPlugin` 接口、`PluginMetadataAttribute`、`RequiresCapabilitiesAttribute`
  - 插件加载：`PluginLoader` 扫描加载、`PluginAssemblyLoadContext` 隔离上下文
  - 状态机：`PluginStateMachine`、`StateTransitionValidator`、14 种状态
  - 生命周期管理器：`PluginLifecycleManager`（注册/初始化/启动/停止）
  - 事件总线：`IEventBus` 接口 + MediatR 实现
  - 能力声明：`PluginCapabilities` 位标志枚举（14 项能力 + 4 种预定义组合）
  - 应用角色：`AppRole` 位标志枚举（Client / Server / Standalone）
  - DI 扩展方法：`AddCoreServices`
- **共享库 (AP.Shared)**
  - `PluginBase` 基类
  - UI 控件：`LoadingSpinner`、`MaterialDialog`、`Toast`、主题样式
  - 工具类：`SerializationHelper`、`ConfigurationHelper`
- **契约层 (AP.Contracts)**
  - `AP.Contracts.Core`：核心事件、错误模型
  - `AP.Contracts.Hardware`：硬件接口、设备事件
  - `AP.Contracts.Communication`：gRPC 消息协定
  - `AP.Contracts.System`：系统服务接口
- **基础设施层 (AP.Infra)**
  - `AP.Infra.Database`：FreeSql Repository 实现（SQLite + PostgreSQL）
  - `AP.Infra.Grpc`：gRPC Server/Client、`StreamBroadcaster`、`LoggingInterceptor`
  - `AP.Infra.Logging`：Serilog 结构化日志配置
  - `AP.Infra.Resilience`：Polly 策略工厂（可配置重试）
  - `AP.Infra.Report`：报表框架（生成/归档/清理）
- **启动宿主 (AP.Host.Desktop)**
  - 角色感知启动（Server / Client / Standalone）
  - Prism + DryIoc DI 容器配置
  - 插件自动扫描加载
- **插件**
  - `AP.Plugin.Plc.Mitsubishi`：三菱 PLC MC 协议驱动
  - `AP.Plugin.Scanner`：串口扫码枪驱动
  - `AP.Plugin.AirtightnessCheck`：气密性检测业务
  - `AP.Plugin.DeviceConfiguration`：设备参数配置
  - `AP.Plugin.Layout`：布局管理
- **测试项目**
  - `AP.Core.Tests`：状态机、生命周期、事件总线、插件框架测试
  - `AP.Shared.Tests`：PluginBase、工具类测试
  - `AP.Infra.Tests`：报表、容错策略测试
- **文档**
  - `docs/GETTING_STARTED.md`：使用指南

---

## 格式说明

- `新增` 新功能
- `变更` 已有功能的变更
- `废弃` 即将移除的功能
- `移除` 已移除的功能
- `修复` 问题修复
- `安全` 安全修复

---

**最后更新**: 2026-07-21