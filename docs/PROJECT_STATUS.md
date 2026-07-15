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

AP-Scaffold 是一个面向工业自动化场景的 **.NET 8 WPF 插件化平台脚手架**。目标是为上位机、MES 客户端、产线监控等系统提供可插拔的通用底座：核心与业务解耦，硬件驱动、业务模块、系统功能均以插件形式存在，通过 Contracts 接口和 MediatR 事件通信。

- **目标框架**：.NET 8（使用 .NET 10 SDK 构建，`global.json` 指定 SDK 10.0.102）
- **UI 框架**：WPF + Prism 9 + DryIoc + MaterialDesignThemes
- **MVVM 工具**：CommunityToolkit.Mvvm
- **消息总线**：MediatR
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
| `PluginFramework` | 插件抽象（`IPlugin`、`IConfigurablePlugin`）、元数据特性（`PluginMetadataAttribute`、`RequiresCapabilitiesAttribute`）、`PluginLoader`、隔离加载上下文 `PluginLoadContext` |
| `Lifecycle` | `PluginLifecycleManager` 按优先级编排插件初始化/启动/停止，维护状态机 |
| `StateMachine` | `PluginStateMachine` + `StateTransitionValidator`，13 种插件状态 |
| `Capability` | `PluginCapabilities` 位标志枚举，声明插件所需能力 |
| `EventBus` | 基于 MediatR 的 `IEventBus` 封装 |
| `Enums` | `AppRole`（Server / Client / Standalone 位标志） |

### 2. 契约层 `platform/contracts`

| 项目 | 说明 |
|------|------|
| `AP.Contracts.Core` | 核心事件（`AppInitializedEvent`、`DeviceConnectedEvent` 等）、错误模型 |
| `AP.Contracts.Hardware` | 硬件服务接口（`IPlcService` 等）、设备事件、能力声明 |
| `AP.Contracts.Communication` | gRPC 消息协定 |
| `AP.Contracts.System` | 系统服务接口（`ILoginService`、`ISettingsDialogService` 等） |
| `AP.Contracts.Security` | 安全/权限契约：`IIdentityService`、用户/角色/权限模型、审计日志接口 `IAuditService`、初始化接口 `ISecurityDbInitializer` |
| `AP.Contracts.Recipe` | 配方管理契约：`IRecipeService`、`IRecipeDbInitializer`、配方模型 |
| `AP.Contracts.Report` | 报表中心契约：`IReportCenterService`、报表归档模型、查询参数 |

### 3. 基础设施层 `platform/infra`

| 项目 | 说明 |
|------|------|
| `AP.Infra.Database` | FreeSql 配置、`IFreeSql` 注册、仓库基类、数据库类型转换 |
| `AP.Infra.Grpc` | gRPC Server/Client、`StreamBroadcaster`、`LoggingInterceptor` |
| `AP.Infra.Logging` | Serilog 配置、日志清理辅助类 `LogCleanupHelper` |
| `AP.Infra.Resilience` | Polly 策略工厂与配置 |
| `AP.Infra.Security` | 安全模块实现：用户/角色/权限 Repository、密码哈希、身份服务、审计日志服务、数据库初始化 `SecurityDbInitializer` |
| `AP.Infra.Recipe` | 配方管理实现：配方服务、数据库初始化 `RecipeDbInitializer` |
| `AP.Infra.Report` | 报表框架：`ReportService`、`ReportCenterService`、Excel 导出、报表存储、归档/清理后台服务、数据库初始化 `ReportDatabaseInitializer` |

### 4. 共享库 `platform/shared`

| 项目 | 说明 |
|------|------|
| `AP.Shared.PluginSDK` | `PluginBase` 基类、插件配置扩展 |
| `AP.Shared.UI` | `ViewModelBase`、权限行为 `PermissionBehavior`、对话框服务、转换器、Material Design 3 主题 `Industrial.Teal.MD3` |
| `AP.Shared.Utilities` | `SerializationHelper`、`ConfigurationHelper` 等工具 |

### 5. 系统插件 `platform/plugins/system`

| 插件 | 功能 | 权限/条件 |
|------|------|-----------|
| `AP.Plugin.Layout` | 主界面布局、Sidebar 导航、顶部状态栏、当前用户信息 | 无 |
| `AP.Plugin.Login` | 登录窗口、修改密码、重新登录、登录/登出审计 | 依赖 `Security:Enabled` |
| `AP.Plugin.SystemSettings` | 系统配置中心（模态弹窗） | 无 |
| `AP.Plugin.UserManagement` | 用户列表、新增/编辑/删除/重置密码 | `user.manage` |
| `AP.Plugin.RoleManagement` | 角色列表、权限分配、新增/编辑/删除 | `role.manage` |
| `AP.Plugin.AuditLog` | 审计日志查询、筛选、分页 | `audit.view` |
| `AP.Plugin.RecipeManagement` | 配方列表、编辑、参数维护、默认配方/切换 | `recipe.view` / `recipe.edit` / `recipe.switch` |
| `AP.Plugin.ReportCenter` | 报表归档查询、生成、打开、导出 | `report.view` |

### 6. 业务插件 `platform/plugins/business`

| 插件 | 功能 |
|------|------|
| `AP.Plugin.AirtightnessCheck` | 气密性检测流程（含报表数据提供者骨架） |
| `AP.Plugin.DeviceConfiguration` | 设备参数配置界面 |

### 7. 硬件插件 `platform/plugins/hardware`

| 插件 | 功能 |
|------|------|
| `AP.Plugin.Plc.Mitsubishi` | 三菱 PLC MC 协议驱动、读写 bool/short/int/float、批量操作、看门狗心跳、自动重连 |
| `AP.Plugin.Scanner` | 串口扫码枪数据接收 |

### 8. 启动宿主 `platform/hosts/AP.Host.Desktop`

- `Bootstrapper`：PrismBootstrapper 实现，负责配置读取、服务注册、插件发现、容器桥接、插件生命周期启动
- `MainWindow`：主窗口，包含 `SidebarRegion` / `ContentRegion`
- `SplashWindow`：启动画面，显示初始化进度
- `TrayIconManager`：系统托盘最小化/退出
- 启动流程（按实际代码）：
  1. 读取配置 → 确定 `AppRole`
  2. 注册平台基础设施（Database、Security、Recipe、Report、Resilience、gRPC 等）
  3. 扫描并实例化插件
  4. 注册 MediatR
  5. 桥接 DryIoc 容器
  6. 安全模块数据库初始化（`ISecurityDbInitializer`）
  7. 登录窗口
  8. 配方数据库初始化（`IRecipeDbInitializer`）
  9. 报表数据库初始化（`ReportDatabaseInitializer.StartAsync`）
  10. 插件 `InitializeAsync` / `StartAsync`
  11. 启动 gRPC Server/Client

### 9. 测试 `platform/tests`

| 项目 | 测试数（当前） | 覆盖范围 |
|------|---------------|----------|
| `AP.Core.Tests` | 123 | 状态机、生命周期、事件总线、插件框架、Capability |
| `AP.Shared.Tests` | 39 | PluginBase、序列化、配置更新 |
| `AP.Infra.Tests` | 38 | 报表选项/实体、弹性策略选项 |

---

## 各模块成熟度

> 骨架 = 接口和基础 UI 已具备，但缺少真实业务数据或完整流程；可用 = 功能已基本可运行；完善 = 有完整流程和较高健壮性。

| 模块 | 成熟度 | 说明 |
|------|--------|------|
| AP.Core 插件框架 | 完善 | 动态加载、生命周期、状态机、事件总线均已实现并测试 |
| AP.Infra.Database / Logging / Resilience | 可用 | 基础设施已就绪 |
| AP.Infra.Security | 可用 | 本地用户/角色/权限、审计日志服务已实现 |
| AP.Plugin.Login / UserManagement / RoleManagement | 可用 | 登录、用户/角色管理 UI 已落地，按权限显示 |
| AP.Plugin.AuditLog | 骨架 | 查询/筛选/分页 UI 已有，数据来自真实审计日志 |
| AP.Infra.Recipe / AP.Plugin.RecipeManagement | 骨架 | 配方 CRUD、版本、默认配方 UI 已有 |
| AP.Infra.Report / AP.Plugin.ReportCenter | 骨架 | 报表归档查询/生成/导出 UI 已有，报表数据提供者为示例实现 |
| AP.Plugin.AirtightnessCheck / DeviceConfiguration | 骨架 | 业务插件界面已有，部分流程待补全 |
| AP.Plugin.Plc.Mitsubishi / Scanner | 可用 | 硬件驱动实现较完整 |
| gRPC Server/Client | 可用 | 已实现，但现场大规模验证不足 |

---

## 当前工作计划

### 近期（当前 Sprint）

1. **报表中心完善**
   - [ ] 接入真实业务报表数据提供者（如气密性检测日报）
   - [ ] 报表模板化支持验证
   - [ ] 手动生成/补档流程端到端测试

2. **配方管理完善**
   - [ ] 配方参数校验规则
   - [ ] 配方切换与业务插件联动
   - [ ] 配方版本历史查看

3. **审计日志增强**
   - [ ] 更多业务操作记录审计日志
   - [ ] 导出审计日志

### 中期

- [ ] 身份认证与授权：支持更多认证方式（如 Windows 域账号、LDAP）
- [ ] OpenTelemetry 可观测性集成（日志、指标、追踪）
- [ ] 更多 PLC 协议支持（西门子、欧姆龙）
- [ ] 集成测试与端到端测试补充

### 长期

- [ ] 插件市场/热更新机制
- [ ] 多语言/国际化支持
- [ ] 完善的安装包与自动更新

---

## 已知问题与注意事项

1. **IHostedService 不会自动启动**
   - `AP.Host.Desktop` 使用 `PrismBootstrapper` 手动构建容器，不会调用 `IHost.StartAsync()`，因此 `IHostedService` 实现不会自动运行。
   - 当前安全/配方/报表模块的数据库初始化器均已在 `Bootstrapper.OnInitialized` 中手动调用。
   - 若未来新增 `IHostedService`，需在 `Bootstrapper.OnInitialized` 中显式解析并调用。

2. **契约程序集必须被 Host 直接引用**
   - 插件通过 `PluginLoadContext` 隔离加载，但共享契约程序集（如 `AP.Contracts.Report`）必须能被主程序默认上下文加载。
   - 否则 MediatR 扫描类型时会抛出 `ReflectionTypeLoadException`。
   - 解决方案：在 `AP.Host.Desktop.csproj` 中直接引用相关 Contracts / Infra 项目。

3. **数据库自动建表已关闭**
   - `AddPlatformDatabase` 设置了 `.UseAutoSyncStructure(false)`，所有表必须通过初始化器显式 `SyncStructure` 创建。
   - 新增实体时，记得在对应模块的初始化器中调用 `SyncStructure`。

4. **安全模块可关闭**
   - `Security:Enabled=false` 时会跳过登录窗口，注入匿名身份实现。开发/测试环境可关闭。

5. **插件输出目录**
   - `Directory.Build.props` 将所有 `AP.Plugin.*` 项目输出到 `bin/$(Configuration)/plugins/{PluginName}/`。
   - 构建后会自动清理插件目录中与 Host 重复的共享库（Core、Shared、Contracts、Prism、Polly、DryIoc、MediatR、CommunityToolkit 等）。

6. **权限字符串约定**
   - 当前已使用的权限字符串：`user.manage`、`role.manage`、`audit.view`、`recipe.view`、`recipe.edit`、`recipe.switch`、`report.view`。
   - 新增系统功能插件时，应在 `SidebarViewModel` 和插件 `InitializeAsync` 中按权限条件注册视图。

---

**最后更新**: 2026-07-14
