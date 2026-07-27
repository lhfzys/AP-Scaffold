# AP-Scaffold 项目状态与工作计划

本文档以**当前代码实际状态**为准，描述 AP-Scaffold 项目已具备的框架内容、各模块成熟度以及后续工作计划。用于在团队内部对齐进度，也作为 AI 协作时的参考之一。

> 如果你刚进入项目，请先阅读根目录的 `AGENTS.md`，它包含了更精简的 AI 交接信息。

---

## 目录

- [项目简介](#项目简介)
- [当前框架实际内容](#当前框架实际内容)
- [各模块成熟度](#各模块成熟度)
- [当前工作计划](#当前工作计划)
- [已知问题与注意事项](#已知问题与注意事项)

---

## 项目简介

AP-Scaffold 是一个面向工业自动化场景的 **.NET 8 WPF 插件化平台脚手架**，设计目标是**可快速复用、安全可靠、统一视觉**。为上位机、MES 客户端、产线监控等系统提供可插拔的通用底座：核心与业务解耦，硬件驱动、业务模块、系统功能均以插件形式存在，通过 Contracts 接口和 MediatR 事件通信。

- **目标框架**：.NET 8（使用 .NET 10 SDK 构建，`global.json` 指定 SDK 10.0.102）
- **UI 框架**：WPF + Prism 9 + DryIoc + MaterialDesignThemes（全浅色 MD3 主题）
- **MVVM 工具**：CommunityToolkit.Mvvm
- **消息总线**：MediatR（关键事件桥接到 Prism IEventAggregator）
- **数据库 ORM**：FreeSql（SQLite / PostgreSQL）
- **通信**：gRPC（Server / Client / Standalone）
- **日志**：Serilog
- **容错**：Polly
- **测试**：xUnit + NSubstitute + FluentAssertions

---

## 当前框架实际内容

### 1. 核心层 `platform/core/AP.Core`

| 模块 | 说明 |
|------|------|
| `PluginFramework` | 插件抽象（`IPlugin`、`IConfigurablePlugin`、`IApplicationLifecycle`）、元数据特性（`PluginMetadataAttribute`、`RequiresCapabilitiesAttribute`）、`PluginLoader`、隔离加载上下文 `PluginLoadContext`、`AssemblyScanner` |
| `Lifecycle` | `PluginLifecycleManager` 按优先级编排插件初始化/启动/停止，维护状态机 |
| `StateMachine` | `PluginStateMachine` + `StateTransitionValidator`，14 种插件状态（Unloaded=0 … Deprecated=13） |
| `Capability` | `PluginCapabilities` 位标志枚举（14 项能力 + ReadOnly/Standard/Hardware/FullAccess 预定义组合）。**注意：目前仅有声明，无运行时强制检查** |
| `EventBus` | 基于 MediatR 的 `IEventBus` 封装（`MediatREventBus`） |
| `Enums` | `AppRole`（Client=1 / Server=2 / Standalone=4 / All=7，位标志） |

### 2. 契约层 `platform/contracts`

| 项目 | 说明 |
|------|------|
| `AP.Contracts.Core` | `OperationResult<T>`、错误模型（`ErrorCode`、`PlatformException`）、`AppInitializedEvent`（Prism PubSubEvent） |
| `AP.Contracts.Hardware` | 硬件服务接口（`IPlcService`、`IPlcBatchReadWrite`、`IScannerService`、`IPlcDriverFactory`）、`PlcOptions`、`PlcServiceFeatures`、设备事件（MediatR record + Prism 桥接事件）、`ConnectDeviceCommand`；**DeviceRuntime/**（2026-07-25 起）：`IDevice`/`IDeviceRegistry`/`IDeviceStateChangedEvent` 等设备抽象、`TagDefinition`/`TagValue`/`ITagService`/`ITagTable`/`IAddressValidator` 等 Tag 契约、`DeviceConnectionState` 六态枚举 |
| `AP.Contracts.Communication` | 仅 proto 文件（`automation_gate.proto`、`common.proto`），`AutomationGate` 服务（`StreamPlcData` 服务端流 + `Heartbeat`） |
| `AP.Contracts.System` | 系统服务接口（`ILoginService`、`ISettingsDialogService`、`ISystemMonitorService`）、`SystemMetrics` |
| `AP.Contracts.Security` | 安全/权限契约：`IIdentityService`、`IUserRepository`、`IRoleRepository`、`IPermissionRepository`、`IPasswordHasher`、`ISecurityDbInitializer`、审计 `IAuditService` / `AuditLogEntry` / `AuditActionType`、用户/角色/权限模型 |
| `AP.Contracts.Recipe` | 配方管理契约：`IRecipeManager`（含 `SwitchAsync`、`CurrentRecipe`）、`IRecipeDbInitializer`、`RecipeInfo` / `RecipeParameter` |
| `AP.Contracts.Report` | 报表中心契约：`IReportCenterService`、`ReportTypeInfo`、`ReportArchiveDto` |

### 3. 基础设施层 `platform/infra`

| 项目 | 说明 |
|------|------|
| `AP.Infra.Database` | `AddPlatformDatabase(config, appRole)`：FreeSql 配置（`UseAutoSyncStructure(false)`）、SQLite 启动前自动备份 + WAL 优化、`IRepository<T>` / `FreeSqlRepository<T>`、`BaseEntity` |
| `AP.Infra.Grpc` | `GrpcGateService`（proto 生成基类实现）、`StreamBroadcaster`（Channel 背压广播）、`GrpcClientWorker`、`GrpcChannelFactory`、`LoggingInterceptor` |
| `AP.Infra.Hardware` | `AddPlcHardware`：`PlcDriverRegistry`（按 DriverType 索引工厂）+ `ActivePlcService`（懒加载代理，统一 `IPlcService`）；**DeviceRuntime/**（2026-07-25 起）：`DeviceConnectionStateMachine`、`ConnectionSupervisor`（全部硬件共享的连接监督器）、`DeviceRegistry`、`PlcDeviceAdapter`、`TagTable`（点表加载/快速失败校验）、`TagService`（按点名读写）、`TagAcquisitionEngine`（采集引擎）、`LatestTagValueStore`（最新值表）、事件发布器 |
| `AP.Infra.Logging` | Serilog 配置（控制台 + 按天滚动文件，含 MachineName/ThreadId/ProcessId 增强器）、`LogCleanupHelper`（启动时清理过期日志） |
| `AP.Infra.Resilience` | `ResiliencePipelineFactory`（Keys：`Database-Retry` / `PLC-Retry` / `Grpc-CircuitBreaker`）、扁平配置键 `Resilience:*RetryCount` 等 |
| `AP.Infra.Security` | 安全模块实现：用户/角色/权限 Repository、PBKDF2-SHA256 密码哈希（10 万次迭代）、`IdentityService` / `AnonymousIdentityService`、`AuditService` / `NullAuditService`、`SecurityDbInitializer`（12 权限 + 3 角色 + admin 种子数据） |
| `AP.Infra.Recipe` | `RecipeManager`（CRUD + 版本 + 默认配方 + 切换；`SwitchAsync` 事件发布留 TODO）、`RecipeDbInitializer`（自动创建 DEFAULT 配方） |
| `AP.Infra.Report` | 报表框架：`ReportService`、`ReportCenterService`、`ExcelExporter`（MiniExcel）、`ReportStorage`、`ReportScheduler`、`ReportCleanupService`、`ReportDatabaseInitializer`、`SampleReportDataProvider` 示例（`IReportDataProvider`/`ReportData` 已移至契约层） |

### 4. 共享库 `platform/shared`

| 项目 | 说明 |
|------|------|
| `AP.Shared.PluginSDK` | `PluginBase` 基类、**`INavigationContributor` + `NavigationMenuItem` + `NavigationMenuItemBuilder`**（声明式导航）、**`ISettingsContributor` + `ISettingsEditorViewModel`**（设置贡献者） |
| `AP.Shared.UI` | `ViewModelBase`（含 `RequestClose` 事件、`Title`/`IsBusy`/`BusyText`）、`LoadingSpinner` 控件、`ICustomDialogService` / `MaterialDialogService`（Alert/Confirm/Error）、4 个转换器、`PermissionBehavior`（附加属性 `Permission` / `HideWhenUnauthorized`）、全浅色主题 `Industrial.Teal.MD3.xaml` |
| `AP.Shared.Utilities` | `ConfigurationHelper`（appsettings 写回）、`SerializationHelper`、`GlobalConstants`（RegionNames/ConfigKeys）、`AppConstants`、`AppConfigurationOptions` |

### 5. 系统插件 `platform/plugins/system`

| 插件 | Priority | 功能 | 导航菜单（Order/权限） | 视图注册门控 |
|------|---------|------|----------------------|-------------|
| `AP.Plugin.Layout` | 10 | 布局（Standard/SinglePage）、Sidebar、顶部状态栏、仪表盘（2026-07-26 起全部为真实数据） | 仪表板（100，无权限，IsDefault） | 无 |
| `AP.Plugin.Login` | 1 | 登录窗口、强制改密、重新登录、登录/登出审计 | 无 | 无 |
| `AP.Plugin.SystemSettings` | 5 | 系统配置中心（设置贡献者收集 + 统一保存/备份） | 系统配置（1000，`system.settings`） | 无 |
| `AP.Plugin.UserManagement` | 5 | 用户列表、新增/编辑/删除/重置密码 | 用户管理（4000，`user.manage`） | `Security:Enabled` + `user.manage` |
| `AP.Plugin.RoleManagement` | 6 | 角色列表、权限分配、新增/编辑/删除 | 角色管理（4100，`role.manage`） | `Security:Enabled` + `role.manage` |
| `AP.Plugin.AuditLog` | 7 | 审计日志查询、筛选、分页 | 审计日志（4200，`audit.view`） | `Security:Enabled` + `audit.view` |
| `AP.Plugin.RecipeManagement` | 8 | 配方列表、编辑、参数维护、默认配方/切换 | 配方管理（2000，`recipe.view`） | 仅 `recipe.view`（未检查 Security 开关） |
| `AP.Plugin.ReportCenter` | 9 | 报表归档查询、生成、打开、导出 | 报表中心（3000，`report.view`） | 仅 `report.view`（未检查 Security 开关） |

### 6. 业务插件 `platform/plugins/business`

| 插件 | Priority | 功能 |
|------|---------|------|
| `AP.Plugin.DeviceConfiguration` | 100 | 通过 `ISettingsContributor` 提供"扫码枪配置"设置页（硬件类，含校验，需重启生效） |

### 7. 硬件插件 `platform/plugins/hardware`

| 插件 | Priority | 角色 | 功能 |
|------|---------|------|------|
| `AP.Plugin.Plc.Mitsubishi` | 20 | Server\|Standalone | 三菱 MC 协议（IoTClient），注册 `IPlcDriverFactory`；连接管理经共享 `ConnectionSupervisor`（心跳 ReadInt16）；实现 `IDevice`；内置 `McAddress` 地址对象 + 地址验证器；读写 bool/short/ushort/int/uint/float；批量为循环单点 |
| `AP.Plugin.Plc.Siemens` | 21 | Server\|Standalone | 西门子 S7 协议（IoTClient），注册 `IPlcDriverFactory`；连接管理经共享 `ConnectionSupervisor`（心跳 ReadBoolean）；实现 `IDevice`；内置 `S7Address` 地址对象 + 地址验证器；额外支持 string；`BatchRead`/`BatchWrite` 真批量（仅 Int16） |
| `AP.Plugin.Plc.Omron` | 22 | Server\|Standalone | 欧姆龙 FINS/TCP（IoTClient），注册 `IPlcDriverFactory`；连接管理经共享 `ConnectionSupervisor`；实现 `IDevice`；内置 `FinsAddress` 地址对象 + 地址验证器；字符串不支持、批量写退化逐条 |
| `AP.Plugin.Scanner` | 20 | Client\|Standalone | 串口扫码枪（System.IO.Ports），DataReceived → Channel → MediatR `ScanCompletedEvent`；实现 `IDevice`（重连经共享 `ConnectionSupervisor`；首开失败仍同步抛出——A 方案例外） |

### 8. 启动宿主 `platform/hosts/AP.Host.Desktop`

- `Bootstrapper`（`PrismBootstrapper`）：配置读取 → Serilog → Infra 服务注册（Database/Resilience/Security/Recipe/Hardware/Report，按角色加 gRPC）→ 插件发现 → 两阶段实例化（先收集 `ConfigureServices`，再造最终容器）→ MediatR 扫描 → DryIoc 桥接（`Populate` + 注册插件实例，含 `INavigationContributor`）→ 登录窗口 → 数据库初始化器（Security/Recipe/Report）→ 插件 Initialize/Start → 按角色启动 Kestrel gRPC / GrpcClientWorker
- `MainWindow`：仅一个 `MainRegion`（布局由 Layout 插件注入），`RootDialogHost` 对话框宿主，启动遮罩在 `AppInitializedEvent` 后关闭
- `SplashWindow`：启动画面，按初始化进度实时更新百分比与状态文本
- `TrayIconManager`：WinForms NotifyIcon，最小化到托盘、显示主窗口、重启、退出（关闭需确认）
- `GlobalExceptionHandler`：三级异常捕获，崩溃写 `logs/crash-yyyyMMdd.log`，致命异常 `Environment.Exit(1)`

### 9. 测试 `platform/tests`

| 项目 | 文件数 | 测试数（`dotnet test` 实测，全部通过） | 覆盖范围 |
|------|-------|--------------------------------|----------|
| `AP.Core.Tests` | 9 | 130 | 状态机、生命周期、事件总线、插件框架、Capability |
| `AP.Shared.Tests` | 4 | 48 | PluginBase、导航菜单构建器、序列化、配置更新 |
| `AP.Infra.Tests` | 31 | 287 | 报表/弹性/DB 仓储/PLC 注册表与激活服务；**Device Runtime 全套**（连接状态机、监督器、桥接、设备注册表、适配器、点表、Tag 服务、采集引擎+批量合并、最新值表、发布器、审计日报）；**三品牌地址对象与批量类型映射**（经 `InternalsVisibleTo` 测试插件 internal） |

---

## 各模块成熟度

> 骨架 = 接口和基础 UI 已具备，但缺少真实业务数据或完整流程；可用 = 功能已基本可运行；完善 = 有完整流程和较高健壮性。

| 模块 | 成熟度 | 说明 |
|------|--------|------|
| AP.Core 插件框架 | 完善 | 动态加载、生命周期、状态机、事件总线均已实现并测试 |
| 导航/设置贡献者模式 | 可用 | 声明式菜单与配置页接入，Builder 有测试覆盖；`Category` 分组在导航中预留未用 |
| AP.Infra.Database / Logging / Resilience | 可用 | 基础设施已就绪 |
| AP.Infra.Security | 可用 | 本地用户/角色/权限、审计日志服务已实现；可整体关闭 |
| AP.Plugin.Login / UserManagement / RoleManagement | 可用 | 登录、用户/角色管理 UI 已落地，按权限显示 |
| AP.Plugin.AuditLog | 可用 | 查询/筛选/分页 UI 已有，数据来自真实审计日志 |
| AP.Infra.Recipe / AP.Plugin.RecipeManagement | 骨架 | 配方 CRUD、版本、默认配方 UI 已有；切换联动业务待补 |
| AP.Infra.Report / AP.Plugin.ReportCenter | 骨架 | 报表归档查询/生成/导出 UI 已有，数据提供者仅示例实现 |
| AP.Plugin.DeviceConfiguration | 可用 | 扫码枪设置页完整（校验 + 写回配置） |
| Device Runtime Model（设备运行时） | 完善 | 六态状态机 + 统一连接监督器（全部硬件共享、测试覆盖）+ 设备抽象/注册表/统一状态事件（2026-07-25） |
| Tag 系统 | 可用 | 点表校验/`ITagService`/采集引擎/最新值表/变化事件端到端贯通；点表目前为示例条目，批量合并待带类型批量契约（2026-07-25） |
| AP.Plugin.Plc.Mitsubishi / Siemens / Omron | 完善 | 三品牌驱动统一：共享连接监督器 + `IDevice` + 地址对象/验证器（2026-07-25 重构） |
| AP.Plugin.Scanner | 可用 | 重连已迁入统一监督器；首开失败保留同步抛出（A 方案例外） |
| gRPC Server/Client | ❄ 冻结 | 已实现但范围外（Standalone 不启用）；不再投入验证与改进 |
| Dashboard 仪表盘 | 可用 | 全部真实数据：设备状态/采集点/Tag 变化/真实事件流（2026-07-26） |

---

## 当前工作计划

> **范围决策（2026-07-21）**：当前仅聚焦 **Standalone 单机模式 + SQLite 数据库**；Server/Client（gRPC）与 PostgreSQL/SQL Server 支持冻结（代码保留，不维护不验证）。详细差距分析与三阶段改进计划见 **[docs/IMPROVEMENT_PLAN.md](IMPROVEMENT_PLAN.md)**。

### 近期（当前 Sprint）

> **架构演进（2026-07-25/26 完成阶段 0~4；2026-07-28 全部收官）**：阶段 0~6 全部 24 个任务 + 停车场第一项（带类型批量契约）已完成。阶段：规范先行 → 连接监督收敛（四套看门狗归一）→ 地址对象化 → Device 抽象 → Tag 系统（点表/读写/采集/Dashboard 真实数据）→ 业务防线与依赖清理（LAYERING/DEPENDENCIES 规范、契约层死引用删除、PLC 插件 UI 死引用删除）。附加交付：采集引擎批量合并读、首个真实报表（操作审计日报）。西门子仿真环境真机验证通过。**此后不接新框架功能，等真实外包项目驱动需求**。完成记录见 **[docs/EVOLUTION_PLAN.md](EVOLUTION_PLAN.md)**。

按 `IMPROVEMENT_PLAN.md` 阶段一（排雷，P0）执行：

1. **报表中心完善**
   - [x] 接入真实业务报表数据提供者（2026-07-28：操作审计日报 `AuditDailyReportProvider`，数据源=审计日志，替换示例注册）
   - [x] 修复 `ReportScheduler` / `ReportCleanupService`（IHostedService）不启动问题，恢复定时归档/清理（2026-07-22，`5c98faf`）
   - [ ] 报表模板化支持验证、手动生成/补档端到端测试

2. **排雷修复**（详见 IMPROVEMENT_PLAN 阶段一，2026-07-22 全部完成）
   - [x] 主题补齐 `Brush.Overlay.Background` 键（`5c98faf`）
   - [x] 启动异常兜底关闭 Splash（`5c98faf`）
   - [x] 配置写回失败不再静默 + 原子化（`b8a95c2`）
   - [x] 安装包升级不覆盖现场配置 + .NET 主版本检测（`16704af`）
   - [x] `Required`/`Dependencies`/重复插件 ID 语义落地（`b1a4ab9`）
   - [x] 业务残留清理（标题/示例插件/占位数据，`3d8c86c`）
   - [x] 构建警告清零（`1b68462`，全量 Rebuild 0 警告）

3. **配方管理完善**
   - [ ] 配方参数校验规则
   - [ ] 配方切换与业务插件联动（`SwitchAsync` 事件发布）
   - [ ] 配方版本历史查看

4. **审计日志增强**
   - [ ] 更多业务操作记录审计日志（重点：PLC 写操作）
   - [ ] 导出审计日志

### 中期（对应 IMPROVEMENT_PLAN 阶段二，2026-07-22 已按外包单机形态修订）

- [x] 韧性管道接线（DB 操作接 `Database-Retry`；2026-07-22 完成，含移除误导性 Empty 注册）
- [x] `IReportDataProvider` 移至契约层（2026-07-22 完成；`AP.Contracts` 前缀强制共享保证类型标识，首个真实 Provider 落地时做端到端验证）
- [x] PLC 写操作审计 + 配置修改审计 + 审计拦截器化（2026-07-22 完成：`AuditingPlcServiceDecorator` + `SettingsService` 审计）
- [x] PLC 看门狗监督重启 + Scanner 断线重连（2026-07-22 完成；2026-07-25 进一步统一为共享 `ConnectionSupervisor`，真机验证待现场）
- [x] 托盘重启单实例 Mutex（2026-07-22 完成）
- [x] 仓库示例连接串占位符化（2026-07-22 完成，3d0dd97）
- [x] UI 一致性五批次（2026-07-22 完成：Dashboard 移除快捷入口、编辑窗 Owner 居中、视图级权限防线统一、LoadingSpinner 浅色遮罩收敛+补齐忙碌反馈、DataGrid 全局样式收敛；Splash 深色保留并移除英文公司行）
- [x] UI 一致性第二批次（2026-07-24 完成：DataGrid 单元格模板覆写垂直居中——MD 原模板不消费 `VerticalContentAlignment`；`RaisedButton.Primary` 深蓝底白字按钮替换 14 处 Raised 引用；Header 用户区随 `Security:Enabled` 显隐；配方「更新时间」列加宽）
- [x] Dashboard 接入真实统计数据（2026-07-26，T4.6：在线设备=设备注册表、采集点=点表+引擎、今日变化=Tag 变化计数、最近事件=真实事件流）
- [ ] （可选）列表页 CRUD 交互统一（Recipe 行内按钮 vs User/Role 工具栏）；SettingsShell 补页头标题；AuditLog/Report 工具栏补刷新按钮
- [x] 欧姆龙 PLC 协议支持（2026-07-24 完成：`AP.Plugin.Plc.Omron`，FINS/TCP；字符串读写不支持抛 NotSupportedException、批量写退化为逐条写入）

### 长期

- [ ] （保留项）登录失败锁定 + 密码复杂度策略 + 重置密码随机化——联网部署/等保要求出现时启动
- [ ] （保留项）服务层权限校验、`AP.Infra.Security` 单元测试 + CI 流水线——开放插件生态/CI 环境出现时启动
- [ ] 迁移 .NET 10（net8 于 2026-11 停止支持）
- [ ] 身份认证与授权：支持更多认证方式（如 Windows 域账号、LDAP）
- [ ] OpenTelemetry 可观测性集成（日志、指标、追踪）
- [ ] 插件市场/启用禁用机制（隔离上下文已为热卸载预留，但热卸载本身不做）
- [ ] `RequiresCapabilitiesAttribute` 能力声明的运行时强制执行
- [ ] 多语言/国际化支持
- [ ] 完善的安装包与自动更新
- [ ] ❄（冻结，需求出现时再议）Server/Client 分布式模式、PostgreSQL/SQL Server 支持

---

## 已知问题与注意事项

1. **IHostedService 不会自动启动**
   - `AP.Host.Desktop` 使用 `PrismBootstrapper` 手动构建容器，不会调用 `IHost.StartAsync()`。
   - 当前安全/配方/报表的数据库初始化器已在 `Bootstrapper` 中手动调用；`GrpcClientWorker` 手动 Start。
   - `ReportScheduler` / `ReportCleanupService` 已修复：注册为"单例 + `AddHostedService` 转发"并由 `Bootstrapper` 显式 `StartAsync`（2026-07-21）。
   - 若未来新增 `IHostedService`，需沿用同一模式在 `Bootstrapper.OnInitialized` 中显式解析并调用。

2. **契约程序集必须被 Host 直接引用**
   - 插件通过 `PluginLoadContext` 隔离加载，但共享契约程序集必须能被主程序默认上下文加载，否则 MediatR 扫描类型时抛出 `ReflectionTypeLoadException`。
   - 解决方案：在 `AP.Host.Desktop.csproj` 中直接引用相关 Contracts / Infra 项目。

3. **数据库自动建表已关闭**
   - `AddPlatformDatabase` 设置了 `.UseAutoSyncStructure(false)`，所有表必须通过初始化器显式 `SyncStructure` 创建。
   - 表清单：`sys_users`、`sys_roles`、`sys_permissions`、`sys_user_roles`、`sys_role_permissions`、`sys_audit_logs`、`recipes`、`report_archives`。

4. **PLC 品牌切换机制**
   - 统一通过 `Plc` 配置节（`Plc:DriverType` 等）切换三菱/西门子/欧姆龙，各品牌插件只注册 `IPlcDriverFactory`。
   - `ActivePlcService` 懒加载代理按 DriverType 转发；驱动不支持批量时抛 `NotSupportedException`。
   - 系统设置中的 PLC 配置页编辑同一 `Plc` 节（含心跳/重连/监督重启三项连接参数），保存后需重启。

5. **安全模块可关闭（当前默认关闭）**
   - 随附配置默认 `Security:Enabled=false`（2026-07-22 起）：跳过登录窗口、注入匿名身份（全部权限），Sidebar 按 `AppConfiguration:NavigationWhenSecurityDisabled` 白名单过滤菜单。
   - 审计开关 `Security:Audit:Enabled` 独立判断，缺省回退到 `Security:Enabled`；当前配置显式 `true`，免登录下审计保留（审计表由 `AuditService` 构造函数幂等自建）。
   - 注意：RecipeManagement / ReportCenter 视图注册只检查各自权限，未检查 Security 开关（匿名身份下权限恒 true，行为一致）。

6. **插件输出目录**
   - `Directory.Build.props` 将所有 `AP.Plugin.*` 项目输出到 `bin/$(Configuration)/plugins/{PluginName}/`，无需构建后事件。
   - 构建后自动清理插件目录中与 Host 重复的共享 DLL（Core、Shared、Contracts、Prism、Polly、DryIoc、MediatR、CommunityToolkit、MaterialDesign、Serilog、FreeSql 等）。
   - Publish 时 `CopyPluginsToPublish` 目标会把插件拷贝到发布目录。

7. **权限字符串约定**
   - 12 个种子权限：`system.view`、`system.settings`、`user.manage`、`role.manage`、`audit.view`、`recipe.view`、`recipe.edit`、`recipe.switch`、`report.view`、`report.export`、`device.config`、`test.start`。
   - 新增系统功能插件时：实现 `INavigationContributor`（声明 Permission）+ 在 `InitializeAsync` 按权限注册视图 + 在 `SecurityDbInitializer` 中补充种子权限与角色映射。

8. **主题资源**
   - `Industrial.Teal.MD3.xaml` 文件名保留但内容已是全浅色主题；新 UI 一律使用主题资源键，不硬编码颜色。
   - 加载遮罩统一用 `LoadingSpinner` 控件（浅色 `Brush.Surface` 0.85，绑 `IsBusy`/`BusyText`），不手写遮罩 Grid；DataGrid 不显式设 `Style`，公共属性已在 `App.xaml` 隐式样式全局化；深色例外：Splash 启动页与 Login/ChangePassword 蓝色横幅（刻意保留）。

9. **配置文件大小写**
   - `appsettings.server.json` 文件名是小写 server（Linux 上与 `appsettings.Server.json` 不匹配；Windows 无碍）。

10. **Device Runtime / Tag 系统（2026-07-25/26 落地）**
    - 连接状态唯一事实来源是 `DeviceConnectionStateMachine`（六态）；全部硬件共享 `ConnectionSupervisor`，新设备/新品牌**不允许再写独立看门狗**。
    - 业务按逻辑点名读写（`ITagService`），点名→设备+地址在 `Configuration/tags.json` 点表配置；点表非法即**中止启动**（快速失败），这是有意策略。
    - 启动时序约束：**设备注册与点表加载必须先于插件初始化**（Bootstrapper 1.0/1.1 段）——视图在插件初始化时即可能解析 `ITagTable`（2026-07-26 踩坑修复）。
    - 驱动 internal 地址对象经 `InternalsVisibleTo` 对 `AP.Infra.Tests` 开放测试（测试项目 TFM 为 `net8.0-windows`）。

---

**最后更新**: 2026-07-28
