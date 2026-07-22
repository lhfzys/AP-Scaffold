# AP-Scaffold 架构设计文档

本文档详细描述 AP-Scaffold 的架构设计、分层职责、核心机制与关键设计模式。内容以当前实际代码为准。

脚手架的三大设计目标贯穿全文：

- **可快速复用**：插件化 + 契约层 + 声明式贡献者模式，新业务以最小成本接入
- **安全可靠**：安全/权限/审计体系、Polly 容错、看门狗自愈、崩溃日志
- **统一视觉**：全浅色 Material Design 3 主题、统一 UI 资源与权限行为

---

## 目录

- [分层架构概览](#分层架构概览)
- [核心框架 (AP.Core)](#核心框架-apcore)
  - [插件框架 (PluginFramework)](#插件框架-pluginframework)
  - [插件生命周期管理 (Lifecycle)](#插件生命周期管理-lifecycle)
  - [状态机 (StateMachine)](#状态机-statemachine)
  - [能力声明 (Capability)](#能力声明-capability)
  - [事件总线 (EventBus)](#事件总线-eventbus)
- [契约层 (AP.Contracts)](#契约层-apcontracts)
- [基础设施层 (AP.Infra)](#基础设施层-apinfra)
- [共享库 (AP.Shared)](#共享库-apshared)
- [启动宿主 (AP.Host.Desktop)](#启动宿主-aphostdesktop)
- [插件集 (Plugins)](#插件集-plugins)
- [关键设计模式](#关键设计模式)
- [数据流与交互图](#数据流与交互图)
- [扩展点](#扩展点)

---

## 分层架构概览

```
┌─────────────────────────────────────────────────────────────┐
│                    AP.Host.Desktop (启动宿主)                 │
│  角色识别 → 插件扫描 → 服务注册 → 登录 → 初始化 → gRPC → UI   │
├─────────────────────────────────────────────────────────────┤
│                     Plugins (插件层)                          │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────────┐     │
│  │  Hardware     │ │  Business     │ │  System          │     │
│  │  PLC×2 / 串口 │ │  设备配置      │ │  布局/登录/设置/  │     │
│  │              │ │              │ │  用户/角色/审计/  │     │
│  │              │ │              │ │  配方/报表        │     │
│  └──────┬───────┘ └──────┬───────┘ └────────┬─────────┘     │
│         │                │                   │              │
│         └────────────────┼───────────────────┘              │
│                          │ 依赖 Contracts 接口 / 贡献者模式   │
├──────────────────────────┼──────────────────────────────────┤
│        Contracts (契约层) │                                   │
│  Core │ Hardware │ Communication(proto) │ System │           │
│  Security │ Recipe │ Report                                 │
├──────────────────────────┼──────────────────────────────────┤
│      Infra (基础设施层)   │                                   │
│  Database │ Grpc │ Hardware │ Logging │ Report │             │
│  Resilience │ Security │ Recipe                              │
├─────────────────────────────────────────────────────────────┤
│                    AP.Core (核心框架)                         │
│  PluginFramework │ EventBus │ StateMachine │ Lifecycle      │
├─────────────────────────────────────────────────────────────┤
│           AP.Shared (共享库)                                  │
│  PluginSDK(导航/设置贡献者) │ UI │ Utilities                  │
└─────────────────────────────────────────────────────────────┘
```

### 分层原则

| 层 | 依赖方向 | 职责 |
|----|---------|------|
| **AP.Core** | 无外部业务依赖 | 核心机制：插件加载、状态管理、事件总线 |
| **AP.Contracts** | 仅引用 Core | 接口与事件定义，核心与业务的桥梁 |
| **AP.Infra** | 引用 Contracts + Core | 可复用的横切关注点实现 |
| **Plugins** | 引用 Shared + Contracts (+ Infra) | 具体的业务/硬件逻辑 |
| **AP.Shared** | 无外部依赖 | 基类、控件、工具，被各层引用 |

> **关键约束**：Plugins 之间不允许互相引用，只能通过 Contracts 定义的接口和事件通信。

---

## 核心框架 (AP.Core)

### 插件框架 (PluginFramework)

#### 目录结构

```
PluginFramework/
├── Abstractions/              # 插件接口定义
│   ├── IPlugin.cs             # InitializeAsync / StartAsync / StopAsync
│   ├── IConfigurablePlugin.cs # ConfigureServices(services, configuration)
│   └── IApplicationLifecycle.cs # 应用生命周期（插件可注入，停止应用）
├── Attributes/                # 插件特性标注
│   ├── PluginMetadataAttribute.cs    # 插件元数据（ID、名称、版本、角色、依赖等）
│   └── RequiresCapabilitiesAttribute.cs  # 能力依赖声明
└── Loading/                   # 插件加载机制
    ├── PluginLoader.cs            # 扫描目录 → 加载 DLL → 发现描述符
    ├── PluginDescriptor.cs        # 插件描述符（元数据 + 实例引用）
    ├── PluginAssemblyLoadContext.cs  # 隔离的 AssemblyLoadContext
    └── AssemblyScanner.cs         # 反射扫描 IPlugin 实现与特性
```

#### 插件加载流程

```
Step 1: 目录扫描
  └─→ 遍历 plugins/{插件名}/ 目录，定位与目录同名的 .dll

Step 2: 创建隔离上下文
  └─→ 为每个插件创建独立的 PluginLoadContext（AssemblyLoadContext）
       └─→ 设置依赖解析回调，共享程序集回落到默认上下文

Step 3: 加载程序集
  └─→ AssemblyLoadContext.LoadFromAssemblyPath(dllPath)

Step 4: 扫描 IPlugin 实现
  └─→ AssemblyScanner 反射扫描，寻找实现 IPlugin 的类
       └─→ 读取 [PluginMetadata] 与 [RequiresCapabilities] 特性

Step 5: 角色校验
  └─→ (PluginMetadata.SupportedRoles & 当前 AppRole) == 0 则卸载跳过

Step 6: 排序
  └─→ 按 Priority 升序（数值越小越先加载）

Step 7: 两阶段实例化（由 Bootstrapper 执行）
  └─→ 阶段一：临时 ServiceProvider 实例化插件 → 调 ConfigureServices 收集服务注册
       → 收集插件程序集供 MediatR 扫描；失败插件记入 failedPlugins
  └─→ 阶段二：用最终 ServiceProvider 重新实例化插件存入 descriptor.Instance

Step 8: 注册到生命周期管理器
  └─→ PluginLifecycleManager.RegisterPlugins(descriptors)
```

#### 插件元数据 (PluginMetadataAttribute)

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `Id` | （构造函数必传） | 插件 ID，必须与目录名及 DLL 名一致 |
| `Name` | `""` | 显示名称 |
| `Version` | `"1.0.0"` | 版本 |
| `SupportedRoles` | `AppRole.All` | 支持的运行角色（位标志组合） |
| `Dependencies` | `[]` | 依赖的其他插件 ID |
| `Priority` | `100` | 加载优先级，越小越先加载 |
| `Required` | `true` | 必需插件标记 |

#### 隔离加载上下文 (AssemblyLoadContext)

```csharp
// 每个插件拥有独立的 AssemblyLoadContext
// 好处：
//   1. 同名不同版本的 DLL 可以共存
//   2. 支持理论上热卸载（需要等待 GC 回收）
//   3. 插件 DLL 不会污染宿主的加载上下文
// 代价：
//   1. 跨上下文类型传递需要序列化/接口
//   2. 共享类型必须通过 Shared/Contracts 程序集加载到默认上下文
//      （因此契约程序集必须被 Host 直接引用，否则 MediatR 扫描失败）
```

### 插件生命周期管理 (Lifecycle)

`PluginLifecycleManager` 是插件生命周期的编排器，负责按优先级顺序执行所有插件的初始化、启动和停止。

#### 生命周期状态图

```
                    ┌─────────┐
                    │ Unloaded │
                    └────┬─────┘
                         │
                    ┌────▼──────┐
                    │ Discovered │  ← 文件扫描发现
                    └────┬──────┘
                         │
                    ┌────▼───┐
                    │ Loading │  ← 程序集加载到内存
                    └────┬───┘
                         │
                    ┌────▼───┐
                    │ Loaded │  ← 实例化完成
                    └────┬───┘
                         │ InitializeAsync
                    ┌────▼────────┐
                    │ Initializing │
                    └────┬────────┘
                         │ 成功   失败
                    ┌────▼──────┐   ┌────────┐
                    │Initialized│   │ Failed │
                    └────┬──────┘   └────────┘
                         │ StartAsync
                    ┌────▼────┐
                    │ Starting │
                    └────┬────┘
                         │ 成功   失败
                    ┌────▼───┐   ┌────────┐
                    │ Running │   │ Failed │
                    └────┬───┘   └────────┘
                         │ StopAsync
                    ┌────▼──────┐
                    │ Stopping  │
                    └────┬─────┘
                         │
                    ┌────▼──────┐
                    │ Stopped   │
                    └───────────┘
```

#### 生命周期方法调用顺序

```
RegisterPlugins()
    ↓
InitializePluginsAsync()     // 按 Priority 升序
    ↓
StartPluginsAsync()          // 按 Priority 升序
    ↓
... 运行中 ...
    ↓
StopPluginsAsync()           // 按 Priority 降序（后启动的先停止）
```

#### 错误处理策略

- **初始化失败**：仅记录日志，不阻塞其他插件的初始化和启动
- **启动失败**：仅记录日志，不影响其他插件
- **停止失败**：仅记录日志，继续停止其他插件
- 宿主在全部启动后汇总失败插件并告警（`Bootstrapper`）

### 状态机 (StateMachine)

`PluginStateMachine` 为每个插件维护一个状态实例，确保状态转换的合法性。

#### 状态枚举

| 状态 | 值 | 含义 |
|------|----|------|
| `Unloaded` | 0 | 未加载（初始状态） |
| `Discovered` | 1 | 已发现（文件存在） |
| `Loading` | 2 | 正在加载程序集 |
| `Loaded` | 3 | 已加载到内存 |
| `Initializing` | 4 | 正在执行 `InitializeAsync` |
| `Initialized` | 5 | 初始化完成 |
| `Starting` | 6 | 正在执行 `StartAsync` |
| `Running` | 7 | 正常运行 |
| `Degraded` | 8 | 降级运行（部分功能不可用） |
| `Stopping` | 9 | 正在执行 `StopAsync` |
| `Stopped` | 10 | 已停止 |
| `Failed` | 11 | 不可恢复错误 |
| `Frozen` | 12 | 已冻结（被禁用） |
| `Deprecated` | 13 | 已废弃 |

#### 状态转换规则

由 `StateTransitionValidator` 维护允许的转换矩阵，核心规则：

- 仅允许正向转换（除 Failed → 重试场景）
- 不允许跳过中间状态（如 Unloaded → 直接 Running）
- Failed 状态只能从 Initializing / Starting / Running / Stopping 进入
- 基础流程：Unloaded → Discovered → Loading → Loaded → Initializing → Initialized → Starting → Running → Stopping → Stopped

### 能力声明 (Capability)

`PluginCapabilities` 是一个 `[Flags]` 枚举，提供细粒度的能力声明，使用位运算组合：

```csharp
[Flags]
public enum PluginCapabilities
{
    None = 0,
    ReadConfiguration   = 1 << 0,   // 读取配置
    WriteConfiguration  = 1 << 1,   // 写入配置
    AccessDatabase      = 1 << 2,   // 访问数据库
    AccessFileSystem    = 1 << 3,   // 访问文件系统
    AccessNetwork       = 1 << 4,   // 访问网络
    AccessPLC           = 1 << 5,   // 访问 PLC
    AccessSerialPort    = 1 << 6,   // 访问串口
    AccessCamera        = 1 << 7,   // 访问相机
    RegisterViews       = 1 << 8,   // 注册视图
    ShowDialogs         = 1 << 9,   // 显示弹窗
    PublishEvents       = 1 << 10,  // 发布事件
    SubscribeEvents     = 1 << 11,  // 订阅事件
    CallGrpcServices    = 1 << 12,  // 调用 gRPC
    ProvideGrpcServices = 1 << 13,  // 提供 gRPC 服务
}
```

**预定义组合**：

| 名称 | 包含的能力 |
|------|-----------|
| `ReadOnly` | ReadConfiguration, AccessDatabase |
| `Standard` | ReadConfiguration, AccessDatabase, PublishEvents, SubscribeEvents, RegisterViews |
| `Hardware` | Standard + AccessPLC, AccessSerialPort, AccessNetwork |
| `FullAccess` | 所有能力 |

> **现状**：`[RequiresCapabilities]` 目前只是元数据声明，加载器不做运行时强制检查（路线图项）。

### 事件总线 (EventBus)

基于 MediatR 封装，实现插件间的解耦通信。

#### 架构

```
  ┌──────────┐     Publish()     ┌──────────┐
  │  Plugin A │ ────────────────→│  MediatR  │
  │ (Publisher)│                 │  (IMediator)│
  └──────────┘                   └─────┬─────┘
                                       │
                          ┌────────────┼────────────┐
                          │            │            │
                          ▼            ▼            ▼
                    ┌──────────┐ ┌──────────┐ ┌──────────┐
                    │ Plugin B │ │ Plugin C │ │ Plugin D │
                    │ (Handler)│ │ (Handler)│ │ (Handler)│
                    └──────────┘ └──────────┘ └──────────┘
```

#### 接口定义

```csharp
public interface IEventBus
{
    /// <summary>发布事件（通知所有订阅者）</summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : INotification;

    /// <summary>发送命令（仅一个处理器接收）</summary>
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);
}
```

#### MediatR ↔ Prism 桥接

宿主程序集中的 `MediatRToPrismBridge` 实现 `INotificationHandler<PlcDataChangedEvent>` / `INotificationHandler<ScanCompletedEvent>` / `INotificationHandler<DeviceDisconnectedEvent>`，把关键的 MediatR 事件转发到 Prism `IEventAggregator` 的 `PrismXxxEvent`，供习惯 Prism 事件机制的 UI 组件订阅。桥接器随 MediatR 扫描 Host 程序集自动注册，无显式注册代码。

---

## 契约层 (AP.Contracts)

契约层定义了核心与业务之间的桥梁接口：

| 项目 | 内容 |
|------|------|
| `AP.Contracts.Core` | `OperationResult<T>`、`ErrorCode`、`PlatformException`、`AppInitializedEvent`（Prism PubSubEvent） |
| `AP.Contracts.Hardware` | `IPlcService` / `IPlcBatchReadWrite` / `IScannerService` / `IPlcDriverFactory`、`PlcOptions`、`PlcServiceFeatures`、设备事件（MediatR + Prism 双形式）、`ConnectDeviceCommand` |
| `AP.Contracts.Communication` | gRPC proto 契约（`automation_gate.proto`：`AutomationGate` 服务，`StreamPlcData` 服务端流 + `Heartbeat`；`common.proto`） |
| `AP.Contracts.System` | `ILoginService`、`ISettingsDialogService`、`ISystemMonitorService`、`SystemMetrics` |
| `AP.Contracts.Security` | `IIdentityService`、`IUserRepository` / `IRoleRepository` / `IPermissionRepository` / `IPasswordHasher`、`ISecurityDbInitializer`、`IAuditService` / `AuditLogEntry` / `AuditActionType`、用户/角色/权限模型 |
| `AP.Contracts.Recipe` | `IRecipeManager`（含 `CurrentRecipe`、`SwitchAsync`）、`IRecipeDbInitializer`、`RecipeInfo` / `RecipeParameter` |
| `AP.Contracts.Report` | `IReportCenterService`、`ReportTypeInfo`、`ReportArchiveDto`、`IReportDataProvider`、`ReportData` |

**设计原则**：
- Contracts 只定义接口和模型，不包含实现
- 插件引用 Contracts，Infra 实现 Contracts
- 插件间通过 Contracts 定义的事件进行通信
- 报表数据提供者接口 `IReportDataProvider` 与数据模型 `ReportData` 定义在 `AP.Contracts.Report`（契约程序集经 `PluginLoadContext` 前缀强制共享，跨 ALC 类型标识一致），业务插件实现并注册后即可被报表框架收集

---

## 基础设施层 (AP.Infra)

### AP.Infra.Database — 数据访问

基于 FreeSql 的 Repository 模式，`AddPlatformDatabase(configuration, appRole)` 注册：

- 按 `Database:Provider` 选择 SQLite / PostgreSQL（**当前仅 SQLite 受支持**，PostgreSQL ❄ 冻结）
- **SQLite**：启动前自动备份（`.db → .db.bak`，连同 `-wal`/`-shm`），启用 WAL 等 PRAGMA 优化
- `UseAutoSyncStructure(false)`：**不自动建表**，各模块初始化器显式 `CodeFirst.SyncStructure<T>()`
- `IRepository<T>` / `FreeSqlRepository<T>`（Scoped）：`GetAsync / GetListAsync / InsertAsync / UpdateAsync / DeleteAsync`
- `BaseEntity`：`Id` 自增主键、`CreatedAt` / `UpdatedAt`

### AP.Infra.Security — 安全模块

- `AddPlatformSecurity(configuration)`，配置键 `Security:Enabled`（代码缺省 true；随附 appsettings 默认显式 `false`，即免登录）、`Security:Audit:Enabled`（缺省回退到 `Security:Enabled`，随附配置显式 `true`）
- 实体：`User`/`Role`/`Permission`/`UserRole`/`RolePermission`/`AuditLog`（表：`sys_users`、`sys_roles`、`sys_permissions`、`sys_user_roles`、`sys_role_permissions`、`sys_audit_logs`）
- `PasswordHasher`：PBKDF2-SHA256，16 字节盐 + 32 字节密钥 + 100,000 次迭代，定时间比较验证
- `SecurityDbInitializer`：建表 + 种子数据（12 个权限、3 个角色、admin/admin123 强制改密）；`sys_audit_logs` 表另由 `AuditService` 构造函数幂等自建
- `Security:Enabled=false` 时：`IIdentityService` 替换为 `AnonymousIdentityService`（全部权限）；审计按 `Security:Audit:Enabled` 独立判断，关闭时用 `NullAuditService`

### AP.Infra.Recipe — 配方管理

- `AddPlatformRecipe(configuration)`；`RecipeManager` 实现 `IRecipeManager`
- 实体 `Recipe`（表 `recipes`）：`Code`/`Name`/`Version`/`IsDefault`/`ParametersJson`（JSON 序列化的参数列表）
- `UpdateAsync` 自动 `Version+1`；`SetDefaultAsync` 先清除其他默认；`SwitchAsync` 设置内存 `CurrentRecipe`（事件发布留 TODO）
- `RecipeDbInitializer`：建表 + 无 DEFAULT 配方时创建默认配方

### AP.Infra.Grpc — gRPC 通信（❄ 冻结，未支持）

> Server/Client 分布式模式当前范围外：代码保留但不维护、不验证。Standalone 模式不启动 gRPC。

| 角色 | 模式 | 行为 |
|------|------|------|
| Server ❄ | 服务端 | 内嵌 Kestrel（仅 HTTP/2）+ `GrpcGateService`，通过 `StreamBroadcaster` 广播消息 |
| Client ❄ | 客户端 | `GrpcClientWorker` 连接服务端，接收数据流经 MediatR 转发 |
| Standalone | 单机 | 不启动 gRPC，插件间通过 MediatR 通信 |

**StreamBroadcaster 机制**：
```
Server 端                    Client 端
┌────────────┐              ┌────────────┐
│ Plugin A   │              │ Plugin B   │
│ 发布事件    │              │ 接收广播    │
└─────┬──────┘              └─────▲──────┘
      │                          │
      ▼                          │
┌──────────────┐                │
│ gRPC Service │────────────────┘
│ Broadcaster  │  StreamResponse（Channel 背压）
└──────────────┘
```

- 契约 proto 位于 `AP.Contracts.Communication/Grpc/`，由 Grpc.Tools 生成代码
- `LoggingInterceptor`：统一的 gRPC 请求/响应日志（方法名、耗时、状态）
- 服务端与 WPF 共享同一个 `StreamBroadcaster` 单例（从 WPF 容器桥接给 ASP.NET Core）
- 配置键：服务端 `Grpc:ServerPort`（默认 5000）；客户端 `Grpc:ServerUrl` / `Grpc:ClientId` / `Grpc:ClientName`

### AP.Infra.Logging — 结构化日志

基于 Serilog：

- 输出：控制台 + 滚动文件（`logs/log-yyyyMMdd.txt`）
- 增强器：`MachineNameEnricher`、`ThreadIdEnricher`、ProcessId
- 保留策略：`Logging:RetainedFileCount`（默认 90 天）、`Logging:MaxFileSizeMb`（默认 50MB，`rollOnFileSizeLimit`）
- `LogCleanupHelper.CleanupIfNeeded`：启动时一次性删除过期日志文件，失败仅警告

### AP.Infra.Resilience — 容错策略

基于 Polly 8 的 `ResiliencePipelineFactory`，构造时注册全部管道：

| 管道 Key（`ResiliencePipelineFactory.Keys`） | 策略 |
|------|------|
| `Database-Retry` | 指数退避重试（1s 起），捕获所有异常 |
| `PLC-Retry` | 固定 500ms 间隔重试 |
| `Grpc-CircuitBreaker` | 熔断器（失败率 50%、采样窗 30s、熔断时长可配置） |

配置为扁平键：`Resilience:DatabaseRetryCount` / `PlcRetryCount` / `GrpcCircuitBreakerThreshold` / `CircuitBreakerDurationSeconds`。

所有硬件操作自动受策略保护，开发者无需手动处理重试逻辑。

### AP.Infra.Hardware — PLC 硬件抽象

统一的 PLC 驱动注册与代理：

- `IPlcDriverFactory`（契约层）：`DriverType`、`SupportedFeatures`、`CreateDriver(PlcOptions, IServiceProvider)`
- `PlcDriverRegistry`：收集各品牌插件注册的工厂（按 DriverType 大小写不敏感索引）
- `ActivePlcService`：懒加载代理（`Lazy<IPlcService>`），实现 `IPlcService` / `IPlcBatchReadWrite`，首次调用时按 `Plc:DriverType` 创建真实驱动并转发全部调用
- `AuditingPlcServiceDecorator`：包装 `ActivePlcService` 的审计拦截器（`IPlcService` 实际解析类型），`WriteAsync` / `WriteBatchAsync` 自动留痕（操作人/地址/值/结果），读操作与连接管理不审计
- `AddPlcHardware`：宿主统一注册 `IPlcService`

业务代码只依赖 `IPlcService`，切换 PLC 品牌只需修改 `Plc:DriverType` 配置（或系统设置中的 PLC 配置页）。

### AP.Infra.Report — 报表框架

完整的报表生成、归档、清理机制。详见 [报表框架使用](GETTING_STARTED.md#报表框架使用)。

**内部工作流程**：

```
定时触发 (每天 02:00)      手动触发 (用户操作)
       │                        │
       ▼                        ▼
┌──────────────────────────────────────┐
│           ReportScheduler             │
│  每天指定时间触发 ArchiveService       │
│  （IHostedService，需宿主显式启动）     │
└──────────────────┬───────────────────┘
                   │
                   ▼
┌──────────────────────────────────────┐
│           ReportService               │
│  ┌──────────────────────────────┐    │
│  │  GetReportDataAsync (插件)    │    │
│  │  → 查询业务数据               │    │
│  │  → 组装 ReportData            │    │
│  └──────────┬───────────────────┘    │
│             ▼                        │
│  ┌──────────────────────────────┐    │
│  │  ExcelExporter                │    │
│  │  → MiniExcel 写入临时文件      │    │
│  │  → 原子重命名                 │    │
│  └──────────┬───────────────────┘    │
│             ▼                        │
│  ┌──────────────────────────────┐    │
│  │  ReportStorage                │    │
│  │  → 按 PathFormat 保存         │    │
│  │  → 记录 report_archives 归档   │    │
│  └──────────────────────────────┘    │
└──────────────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────┐
│       ReportCleanupService            │
│  定期检查过期报表（IHostedService）     │
│  删除超过保留天数的文件                │
│  跳过 ProtectedTypes，支持 DryRun      │
└──────────────────────────────────────┘
```

归档实体 `ReportArchive`（表 `report_archives`）：`Id`（GUID 字符串）、`ReportDate`、`ReportType`、`ReportName`、`FilePath`、`RecordCount`、`FileSize`、`GeneratedAt`、`Status`（Success/Failed/Cleaned）、`FailureReason`。

---

## 共享库 (AP.Shared)

### AP.Shared.PluginSDK — 插件开发 SDK

**`PluginBase` 基类**（实现 `IConfigurablePlugin`）：

```csharp
public abstract class PluginBase : IConfigurablePlugin
{
    protected ILogger Logger { get; }
    protected IServiceProvider ServiceProvider { get; }  // InitializeAsync 后可用

    public virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration);
    public virtual Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct);
    public virtual Task StartAsync(CancellationToken ct);
    public virtual Task StopAsync(CancellationToken ct);
}
```

**导航贡献者模式**（`Navigation/`）：

```csharp
public interface INavigationContributor
{
    IEnumerable<NavigationMenuItem> GetMenuItems();
}

public class NavigationMenuItem
{
    public string Label { get; set; }            // 显示文本
    public string IconKind { get; set; }         // Material Design 图标名
    public string NavigationTarget { get; set; } // 导航目标视图名（需注册到 ContentRegion）
    public int Order { get; set; }               // 排序权重，越小越靠前
    public string? Permission { get; set; }      // 可选权限码，空 = 无需权限
    public string? Category { get; set; }        // 分组名（预留给二级菜单）
    public bool IsDefault { get; set; }          // 是否启动默认页
}
```

`NavigationMenuItemBuilder.Build(contributors, hasPermission, defaultTarget, visibilityFilter)` 负责：扁平化 → 过滤无效项 → 按 `NavigationTarget` 去重（取最小 Order）→ 排序 → 默认项匹配（含 `defaultTarget` 配置）→ 权限过滤 → 白名单过滤 → 无默认项时取第一个可见项。

**设置贡献者模式**（`Configuration/`）：

```csharp
public interface ISettingsContributor
{
    string Category { get; }            // 分组（如 "系统"/"硬件"）
    string Title { get; }               // 页标题
    string IconKind { get; }            // 图标
    int Order { get; }                  // 排序
    string ConfigurationSection { get; } // 对应配置节
    ISettingsEditorViewModel CreateViewModel(IServiceProvider serviceProvider);
}

public interface ISettingsEditorViewModel : INotifyPropertyChanged
{
    void LoadFromConfiguration(IConfiguration configuration);
    bool Validate(out string errorMessage);
    object GetConfigurationValue();
    bool RequiresRestart { get; }
}
```

系统配置中心（`SettingsShellViewModel`）自动收集所有 `ISettingsContributor`，保存时统一 Validate → 备份 appsettings → 写回 → 汇总需要重启的项。

### AP.Shared.UI — UI 控件库

全浅色 Material Design 3 视觉体系：

| 资源 | 说明 |
|------|------|
| `Industrial.Teal.MD3.xaml` | 主题文件（文件名保留，内容为全浅色）：主色 `#1E3A5F`、强调色 `#0891B2`、语义色、表面色、文字样式、间距/圆角/海拔 |
| `LoadingSpinner` | 加载动画控件（`IsLoading` / `LoadingText`） |
| `ICustomDialogService` | 对话框服务：`ShowAlertAsync` / `ShowConfirmAsync` / `ShowErrorAsync`（基于 DialogHost `RootDialogHost`） |
| `PermissionBehavior` | 附加属性 `Permission` + `HideWhenUnauthorized`（true=隐藏，false=禁用） |
| 转换器 ×4 | `BoolToVisibilityConverter`、`InverseBoolToVisibilityConverter`、`BoolToStatusConverter`、`FileSizeConverter` |
| `ViewModelBase` | `ObservableObject` + `INavigationAware` + `IDestructible`；`Title`/`IsBusy`/`BusyText`；`RequestClose` 事件 |

主题引用链：`App.xaml` 合并 `MaterialDesign3.Defaults.xaml` + `BundledTheme(Light, BlueGrey, Cyan)` + `AP.Shared.UI` 的 `ResourceDictionary.xaml`。

### AP.Shared.Utilities — 通用工具

| 工具 | 用途 |
|------|------|
| `SerializationHelper` | JSON 序列化/反序列化（System.Text.Json，忽略大小写/忽略 null/枚举转字符串） |
| `ConfigurationHelper` | appsettings.json 配置写回（`UpdateAppSetting`，写 `{BaseDirectory}/Configuration/appsettings.json`） |
| `GlobalConstants` | 项目前缀、Region 名（`MainRegion`/`ContentRegion`/`SettingsRegion` 等）、配置键常量 |
| `AppConstants` | 对话框宿主标识（`RootDialogHost`） |
| `AppConfigurationOptions` | `AppConfiguration` 配置节模型 |

---

## 启动宿主 (AP.Host.Desktop)

宿主无 Prism Module，全部功能由插件目录扫描驱动。启动流程（`Bootstrapping/Bootstrapper.cs`，按实际代码）：

```
App.OnStartup
  │
  ├── 1. GlobalExceptionHandler.Initialize()（崩溃日志 logs/crash-*.log）
  ├── 2. RoleResolver.Resolve(args)（--role=xxx > appsettings AppRole > 默认 Standalone）
  ├── 3. 显示 SplashWindow
  └── 4. new Bootstrapper(role, splash).Run()
         │
         │  RegisterTypes:
         ├── 5. 加载配置（appsettings.json + appsettings.{Role}.json + 环境变量）
         ├── 6. 注册 IConfiguration / AppRole
         ├── 7. ServiceCollection 依次注册：
         │       AddPlatformLogging → AddPlatformDatabase → AddPlatformResilience
         │       → AddPlatformSecurity → AddPlatformRecipe → AddPlcHardware → AddReportFramework
         │       （Server 角色：AddPlatformGrpcServer + StreamBroadcaster；
         │         Client 角色：GrpcClientWorker 等客户端服务）
         ├── 8. PluginLoader.DiscoverPlugins(appRole)：扫描 → 隔离加载 → 角色过滤 → 按 Priority 排序
         ├── 9. 两阶段实例化：临时容器实例化插件 → ConfigureServices 收集服务 → 收集程序集
         ├── 10. AddMediatR（扫描插件程序集 + Host 程序集，含 MediatRToPrismBridge）
         ├── 11. 构建最终 ServiceProvider，重新实例化插件
         ├── 12. DryIoc 桥接：containerRegistry.GetContainer().Populate(services)
         │       + 注册插件实例（IPlugin；若为 INavigationContributor 再注册一份）
         └── 13. 创建 PluginLifecycleManager 并 RegisterPlugins；注册 ICustomDialogService 等
         │
         │  CreateShell / InitializeShell:
         ├── 14. Resolve MainWindow；Security 启用时：先同步跑 SecurityDbInitializer
         │       → 关 Splash → ILoginService.ShowLoginDialog()（失败退出）
         │       → MustChangePassword 则强制改密 → MainWindow.Show()
         │
         │  OnInitialized（异步，进度写 Splash）:
         ├── 15. LogCleanupHelper 清理过期日志
         ├── 16. Security 启用时再次 SecurityDbInitializer（幂等）
         ├── 17. RecipeDbInitializer
         ├── 18. ReportDatabaseInitializer.StartAsync（手动调用，IHostedService 不自动启动）
         ├── 19. PluginLifecycleManager.InitializePluginsAsync + StartPluginsAsync
         ├── 20. Server 角色：内嵌 Kestrel 启动 gRPC（共享 StreamBroadcaster 单例）
         │       Client 角色：手动 GrpcClientWorker.StartAsync
         ├── 21. 汇总失败插件告警 → 发布 AppInitializedEvent → 关闭 Splash
         │
         └── 22. TrayIconManager.Attach(MainWindow)（最小化到托盘/重启/退出）
```

---

## 插件集 (Plugins)

### 硬件驱动插件

| 插件 | Priority | 角色 | 协议 | 功能 |
|------|---------|------|------|------|
| `AP.Plugin.Plc.Mitsubishi` | 20 | Server\|Standalone | MC 协议 (Qna_3E) | 注册 `IPlcDriverFactory`；读写 bool/short/ushort/int/uint/float；批量为循环单点；看门狗 2 秒心跳 + 自动重连 |
| `AP.Plugin.Plc.Siemens` | 21 | Server\|Standalone | S7 协议 (S7_200/300/400/1200/1500/Smart) | 注册 `IPlcDriverFactory`；额外支持 string；`BatchRead`/`BatchWrite` 真批量；看门狗 + 自动重连 |
| `AP.Plugin.Scanner` | 20 | Client\|Standalone | 串口协议 | 扫码枪数据接收（SerialPort → Channel → MediatR `ScanCompletedEvent`） |

### 业务功能插件

| 插件 | Priority | 功能 |
|------|---------|------|
| `AP.Plugin.DeviceConfiguration` | 100 | 通过 `ISettingsContributor` 提供"扫码枪配置"设置页 |

### 系统功能插件

| 插件 | Priority | 功能 | 菜单（Order / 权限） |
|------|---------|------|---------------------|
| `AP.Plugin.Layout` | 10 | 布局（Standard/SinglePage）、Sidebar、Header、仪表盘 | 仪表板（100 / 无，IsDefault） |
| `AP.Plugin.Login` | 1 | 登录认证、强制改密、重新登录 | — |
| `AP.Plugin.SystemSettings` | 5 | 系统配置中心（设置贡献者宿主） | 系统配置（1000 / `system.settings`） |
| `AP.Plugin.UserManagement` | 5 | 用户 CRUD、重置密码 | 用户管理（4000 / `user.manage`） |
| `AP.Plugin.RoleManagement` | 6 | 角色 CRUD、权限分配 | 角色管理（4100 / `role.manage`） |
| `AP.Plugin.AuditLog` | 7 | 审计日志查询/筛选/分页 | 审计日志（4200 / `audit.view`） |
| `AP.Plugin.RecipeManagement` | 8 | 配方 CRUD、默认配方、切换 | 配方管理（2000 / `recipe.view`） |
| `AP.Plugin.ReportCenter` | 9 | 报表归档查询/生成/打开/导出 | 报表中心（3000 / `report.view`） |

---

## 关键设计模式

### 1. 策略模式 (Polly Pipeline Factory)

```csharp
// 根据 Key 获取预建管道
var pipeline = resilienceFactory.GetPipeline(ResiliencePipelineFactory.Keys.Plc);
await pipeline.ExecuteAsync(operation, ct);
```

### 2. 观察者模式 (MediatR EventBus)

插件通过发布/订阅事件通信，发布者和订阅者无需互相知道对方存在；关键事件经 `MediatRToPrismBridge` 桥接到 Prism 事件聚合器。

### 3. 状态模式 (PluginStateMachine)

每个插件的状态转换由状态机管理，确保合法性和可追踪性。

### 4. 策略模式 (Report Data Provider)

每个业务插件实现 `IReportDataProvider` 接口提供数据，报表框架统一处理生成和归档。

### 5. 贡献者模式 (Contributor Pattern)

- **导航贡献者**：插件实现 `INavigationContributor` 声明菜单项，Sidebar 统一收集、去重、排序、按权限过滤——新增页面零侵入。
- **设置贡献者**：插件实现 `ISettingsContributor` 声明配置页，系统配置中心统一收集、分组、保存——新增配置零侵入。

### 6. 工厂 + 注册表 + 代理 (PLC 驱动切换)

各品牌插件注册 `IPlcDriverFactory` → `PlcDriverRegistry` 按 DriverType 索引 → `ActivePlcService` 懒加载代理转发 `IPlcService` 调用。业务代码与品牌解耦。

### 7. 依赖注入 + 两阶段服务收集 (Prism + MS-DI 桥接)

插件的 `ConfigureServices` 先向 `ServiceCollection` 注册服务（阶段一），最终容器建成后重新实例化插件（阶段二），再通过 `DryIoc.Microsoft.DependencyInjection` 的 `Populate` 桥接到 Prism 的 DryIoc 容器——插件服务与 Prism 服务共用一个容器。

### 8. 隔离上下文 (AssemblyLoadContext)

每个插件在独立的 `AssemblyLoadContext` 中运行，避免 DLL 版本冲突；共享契约回落到默认上下文加载。

---

## 数据流与交互图

### 启动阶段

```
Bootstrapper
  │
  ├── 1. 读取 AppRole（命令行 > 配置 > 默认 Standalone）
  │
  ├── 2. PluginLoader.DiscoverPlugins(pluginsDir, appRole)
  │       │
  │       ├── 2a. 扫描目录 → 发现插件 DLL
  │       ├── 2b. 创建 PluginLoadContext
  │       ├── 2c. 加载程序集
  │       ├── 2d. AssemblyScanner 找 IPlugin 实现 + 读特性
  │       ├── 2e. 校验角色匹配（不匹配则卸载）
  │       └── 2f. 按 Priority 排序 → List<PluginDescriptor>
  │
  ├── 3. 两阶段实例化 + ConfigureServices 收集
  │
  ├── 4. MediatR 扫描（插件程序集 + Host）
  │
  ├── 5. DryIoc Populate 桥接 + 注册插件实例/INavigationContributor
  │
  ├── 6. PluginLifecycleManager.RegisterPlugins(descriptors)
  │       └── 为每个插件创建 PluginStateMachine
  │
  ├── 7. （Security 启用）安全库初始化 → 登录窗口 → 强制改密
  │
  ├── 8. 数据库初始化器（Recipe / Report）
  │
  ├── 9. InitializePluginsAsync() → StartPluginsAsync()
  │       └── 按优先级调用，状态机迁移，失败仅记录
  │
  └── 10. 按角色启动 gRPC / 显示主窗口 / 发布 AppInitializedEvent
```

### 运行时硬件通信

```
用户操作 (UI Plugin)
    │
    ▼
ViewModel.ExecuteCommand
    │
    ▼
[Polly Pipeline: PLC-Retry]
    │
    ▼
IPlcService (ActivePlcService 代理)
    │
    ├── 按 Plc:DriverType 懒加载真实驱动
    │       │
    │       ▼
    │   MitsubishiPlcService / SiemensPlcService (IoTClient)
    │       │
    │       ▼
    │   TcpClient.Send/Receive
    │
    └── Watchdog (每 2 秒)
            │
            ├── 正常 → 心跳读写
            └── 超时 → Publish(DeviceConnectionFailedEvent)
                           │
                           ▼
                      MediatR → 通知 UI 更新连接状态
                               → 自动重连（失败退避 5 秒）
```

### 报表生成

```
定时器 (每天 02:00)        手动导出 (用户点击)
    │                          │
    └──────────┬───────────────┘
               │
               ▼
        ReportService.GenerateReportAsync(type, date)
               │
               ├── 查找 IReportDataProvider 实现
               │       │
               │       ▼
               │   Plugin.GetReportDataAsync(date)
               │       │
               │       ▼
               │   ReportData (Headers + Rows + Summary)
               │
               ├── ExcelExporter.Export(data, template)
               │       │
               │       ▼
               │   临时文件 → 原子重命名为 xlsx
               │
               └── ReportStorage.Save(filePath, record)
                       │
                       ▼
                reports/2026/01/2026-01-12_Type.xlsx
                + report_archives 数据库归档记录
```

---

## 扩展点

### 添加新的基础设施层模块

1. 在 `platform/infra/` 下创建类库项目
2. 定义接口（可放在 Contracts 或 Infra 内部）
3. 编写扩展方法 `AddXxx(this IServiceCollection, IConfiguration)`
4. 在宿主的 `Bootstrapper.RegisterTypes` 中调用注册方法
5. 如有数据库实体：编写初始化器并在 `Bootstrapper.OnInitialized` 中显式调用（IHostedService 不会自动启动）

### 添加新的硬件驱动插件

1. 在 `platform/plugins/hardware/` 下创建类库项目
2. 实现 `IPlcDriverFactory`（契约在 `AP.Contracts.Hardware`）
3. 加上 `[PluginMetadata]`（注意 `SupportedRoles` 与 `Priority`）
4. 在 `ConfigureServices` 中 `AddSingleton<IPlcDriverFactory, YourFactory>()`
5. 业务代码无需任何修改，`Plc:DriverType` 配置即可切换

### 添加新的业务插件

1. 在 `platform/plugins/business/` 下创建类库项目（输出目录由 `Directory.Build.props` 自动处理）
2. 创建插件主类继承 `PluginBase`，按需实现 `INavigationContributor` / `ISettingsContributor`
3. 创建 View 和 ViewModel（View 构造函数注入 ViewModel）
4. 在 `InitializeAsync` 中按权限条件注册 Region 视图
5. 可选：实现 `IReportDataProvider` 提供报表能力；新增权限码时在 `SecurityDbInitializer` 中补充种子数据

---

## 参考资料

- [使用指南](GETTING_STARTED.md) — 环境准备、快速开始、详细配置
- [测试指南](TESTING.md) — 测试编写规范与运行方式
- [项目状态](PROJECT_STATUS.md) — 模块成熟度与工作计划
- [README](../README.md) — 项目概览与技术栈

---

**最后更新**: 2026-07-21
