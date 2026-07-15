# AP-Scaffold 架构设计文档

本文档详细描述 AP-Scaffold 的架构设计、分层职责、核心机制与关键设计模式。

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
- [共享库 (AP.Shared)](#共享库-apshotshared)
- [启动宿主 (AP.Host.Desktop)](#启动宿主-aphostdesktop)
- [插件集 (Plugins)](#插件集-plugins)
- [关键设计模式](#关键设计模式)
- [数据流与交互图](#数据流与交互图)

---

## 分层架构概览

```
┌─────────────────────────────────────────────────────────────┐
│                    AP.Host.Desktop (启动宿主)                 │
│  角色识别 → 插件扫描 → 服务注册 → 初始化 → 启动 gRPC → 显示 UI  │
├─────────────────────────────────────────────────────────────┤
│                     Plugins (插件层)                          │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────────┐     │
│  │  Hardware     │ │  Business     │ │  System          │     │
│  │  PLC / 串口   │ │  气密性/配置   │ │  布局管理         │     │
│  └──────┬───────┘ └──────┬───────┘ └────────┬─────────┘     │
│         │                │                   │              │
│         └────────────────┼───────────────────┘              │
│                          │ 依赖 IReportDataProvider 等       │
├──────────────────────────┼──────────────────────────────────┤
│        Contracts (契约层) │                                   │
│  ┌──────────────────┐   │   ┌────────────────────────────┐  │
│  │ AP.Contracts.Core │   │   │   AP.Contracts.Hardware   │  │
│  │ 核心事件/错误模型  │   │   │   硬件服务接口/设备事件    │  │
│  ├──────────────────┤   │   ├────────────────────────────┤  │
│  │ AP.Contracts.Comm│   │   │   AP.Contracts.System     │  │
│  │ gRPC 契约        │   │   │   系统服务接口             │  │
│  └──────────────────┘   │   └────────────────────────────┘  │
├──────────────────────────┼──────────────────────────────────┤
│      Infra (基础设施层)   │                                   │
│  ┌──────────┐ ┌─────────┐ ┌────────┐ ┌───────────────────┐ │
│  │ Database  │ │  gRPC   │ │ Logging│ │ Report / Resilience│ │
│  │ FreeSql   │ │ Server/ │ │Serilog │ │ 报表框架 / Polly  │ │
│  │ Repository│ │ Client  │ │  配置   │ │                   │ │
│  └──────────┘ └─────────┘ └────────┘ └───────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│                    AP.Core (核心框架)                         │
│  PluginFramework │ EventBus │ StateMachine │ Lifecycle      │
├─────────────────────────────────────────────────────────────┤
│           AP.Shared (共享库)                                  │
│  PluginSDK │ UI Controls │ Utilities                        │
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
├── Abstractions/          # 插件接口定义
│   └── IPlugin.cs         # InitializeAsync / StartAsync / StopAsync
├── Attributes/            # 插件特性标注
│   ├── PluginMetadataAttribute.cs    # 插件元数据（ID、名称、版本、角色、依赖等）
│   └── RequiresCapabilitiesAttribute.cs  # 能力依赖声明
└── Loading/               # 插件加载机制
    ├── PluginLoader.cs           # 扫描目录 → 加载 DLL → 创建实例
    ├── PluginDescriptor.cs       # 插件描述符（元数据 + 实例引用）
    └── PluginAssemblyLoadContext.cs  # 隔离的 AssemblyLoadContext
```

#### 插件加载流程

```
Step 1: 目录扫描
  └─→ 遍历 plugins/*/ 目录，收集所有 .dll 文件路径

Step 2: 创建隔离上下文
  └─→ 为每个插件创建独立的 AssemblyLoadContext（支持热卸载）
       └─→ 设置依赖解析回调，解决共享库冲突

Step 3: 加载程序集
  └─→ AssemblyLoadContext.LoadFromAssemblyPath(dllPath)

Step 4: 扫描 IPlugin 实现
  └─→ 反射扫描所有类型，寻找实现 IPlugin 接口的类
       └─→ 读取 [PluginMetadata] 特性
       └─→ 读取 [RequiresCapabilities] 特性

Step 5: 角色校验
  └─→ 对比 PluginMetadata.SupportedRoles 与当前 AppRole
       └─→ 不匹配则跳过该插件

Step 6: 依赖校验
  └─→ 检查 PluginMetadata.Dependencies 中声明的依赖插件是否存在
       └─→ 缺失则跳过或报错（取决于 Required 属性）

Step 7: 实例化
  └─→ 调用 Activator.CreateInstance 创建插件实例
       └─→ 构造函数注入 ILogger

Step 8: 注册到生命周期管理器
  └─→ 创建 PluginDescriptor（元数据 + 实例）
       └─→ 传递给 PluginLifecycleManager.RegisterPlugins()
```

#### 隔离加载上下文 (AssemblyLoadContext)

```csharp
// 每个插件拥有独立的 AssemblyLoadContext
// 好处：
//   1. 同名不同版本的 DLL 可以共存
//   2. 支持理论上热卸载（需要等待 GC 回收）
//   3. 插件 DLL 不会污染宿主的加载上下文
// 代价：
//   1. 跨上下文类型传递需要序列化/接口
//   2. 共享类型必须通过 Shared 程序集加载到默认上下文
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
- **必需插件失败**（`Required = true`）：可配置是否导致应用退出

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

`PluginCapabilities` 是一个 `[Flags]` 枚举，提供细粒度的能力控制，使用位运算组合：

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

#### 使用示例

```csharp
// 发布事件（插件 A）
await eventBus.PublishAsync(new DeviceConnectedEvent
{
    DeviceName = "PLC-01",
    ConnectedAt = DateTime.UtcNow
});

// 订阅事件（插件 B）
public class DeviceConnectionHandler : INotificationHandler<DeviceConnectedEvent>
{
    public Task Handle(DeviceConnectedEvent notification, CancellationToken ct)
    {
        // 处理连接事件
        return Task.CompletedTask;
    }
}
```

---

## 契约层 (AP.Contracts)

契约层定义了核心与业务之间的桥梁接口，主要包括：

| 项目 | 内容 |
|------|------|
| `AP.Contracts.Core` | 核心事件（设备连接/断开）、错误模型 |
| `AP.Contracts.Hardware` | 硬件服务接口（IPlcService 等）、设备事件 |
| `AP.Contracts.Communication` | gRPC 服务/消息协定 |
| `AP.Contracts.System` | 系统服务接口（ILoginService、ISettingsDialogService 等） |
| `AP.Contracts.Security` | 安全/权限/审计日志契约（IIdentityService、ISecurityDbInitializer、IAuditService 等） |
| `AP.Contracts.Recipe` | 配方管理契约（IRecipeService、IRecipeDbInitializer 等） |
| `AP.Contracts.Report` | 报表中心契约（IReportCenterService、报表归档模型等） |

**设计原则**：
- Contracts 只定义接口和模型，不包含实现
- 插件引用 Contracts，Infra 实现 Contracts
- 插件间通过 Contracts 定义的事件进行通信

---

## 基础设施层 (AP.Infra)

### AP.Infra.Database — 数据访问

基于 FreeSql 的 Repository 模式：

```csharp
// 基础仓库接口
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id);
    Task<List<T>> GetAllAsync();
    Task InsertAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}
```

支持 SQLite 和 PostgreSQL 两种数据库提供者，通过配置切换。

### AP.Infra.Grpc — gRPC 通信

| 角色 | 模式 | 行为 |
|------|------|------|
| Server | 服务端 | 启动 Kestrel + gRPC 服务，通过 `StreamBroadcaster` 广播消息 |
| Client | 客户端 | 启动后台 Worker 连接到服务端，接收广播 |
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
│ Broadcaster  │  StreamResponse
└──────────────┘
```

**LoggingInterceptor**：统一的 gRPC 请求/响应日志记录，自动记录每个请求的方法名、耗时和状态。

### AP.Infra.Logging — 结构化日志

基于 Serilog 的配置：

- 默认输出：控制台 + 滚动文件
- 日志格式：JSON 结构化
- 日志保留：可配置天数（默认 90 天）
- 文件大小限制：可配置单文件最大 MB（默认 50MB）

### AP.Infra.Resilience — 容错策略

基于 Polly 的策略工厂，通过配置驱动：

```json
{
  "Resilience": {
    "Pipelines": {
      "PLC-Retry": {
        "MaxRetryAttempts": 5,
        "RetryDelaySeconds": 3
      }
    }
  }
}
```

所有硬件操作自动受策略保护，开发者无需手动处理重试逻辑。

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
│  │  → MiniExcel 写入文件         │    │
│  │  → 应用模板（可选）            │    │
│  └──────────┬───────────────────┘    │
│             ▼                        │
│  ┌──────────────────────────────┐    │
│  │  ReportStorage                │    │
│  │  → 保存到 reports/ 目录       │    │
│  │  → 记录数据库归档记录          │    │
│  └──────────────────────────────┘    │
└──────────────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────┐
│       ReportCleanupService            │
│  定期检查过期报表                      │
│  删除超过保留天数的文件                │
│  DryRun 模式模拟运行                   │
└──────────────────────────────────────┘
```

---

## 共享库 (AP.Shared)

### AP.Shared.PluginSDK — 插件开发 SDK

提供 `PluginBase` 基类，封装了生命周期方法和日志基础设施：

```csharp
public abstract class PluginBase : IPlugin
{
    protected ILogger Logger { get; }

    public virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration);
    public virtual Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct);
    public virtual Task StartAsync(CancellationToken ct);
    public virtual Task StopAsync(CancellationToken ct);
}
```

### AP.Shared.UI — UI 控件库

内置 Material Design 3 风格的 WPF 控件：

| 控件 | 用途 |
|------|------|
| `LoadingSpinner` | 加载动画 |
| `MaterialDialog` | 自定义对话框 |
| `Toast` | 轻量级消息提示 |
| `BoolToVisibility` | 布尔值 ↔ 可见性转换器 |
| `Industrial.Teal.MD3` | 工业风格主题 |

### AP.Shared.Utilities — 通用工具

| 工具 | 用途 |
|------|------|
| `SerializationHelper` | JSON 序列化/反序列化 |
| `ConfigurationHelper` | appsettings.json 配置更新 |

---

## 启动宿主 (AP.Host.Desktop)

宿主的启动流程在 `Bootstrapping/` 目录中，主要步骤：

```
1. 读取 appsettings.json → 确定 AppRole
2. 根据 AppRole 加载对应的专属配置（appsettings.Server.json 等）
3. 配置 Serilog 日志
4. 配置 Prism + DryIoc DI 容器
5. 注册框架服务（Database、gRPC、EventBus、Resilience 等）
6. 注册报表框架
7. 启动插件加载器：
   a. 扫描 plugins/ 目录
   b. 创建隔离 AssemblyLoadContext
   c. 加载并实例化匹配 AppRole 的插件
   d. 注册到 PluginLifecycleManager
8. 调用 PluginLifecycleManager.InitializePluginsAsync()
9. 调用 PluginLifecycleManager.StartPluginsAsync()
10. 手动初始化报表数据库（`ReportDatabaseInitializer.StartAsync`）
11. 调用 `PluginLifecycleManager.InitializePluginsAsync()` 和 `StartPluginsAsync()`
12. 根据 AppRole 启动 gRPC（Server 启动 Kestrel，Client 启动 Worker）
13. 显示主窗口（Standalone 或 Client 模式）
```

---

## 插件集 (Plugins)

### 硬件驱动插件

| 插件 | 协议 | 功能 |
|------|------|------|
| `AP.Plugin.Plc.Mitsubishi` | MC 协议 (Qna_3E) | 读写 bool/short/int/float，批量操作，看门狗心跳，自动重连 |
| `AP.Plugin.Scanner` | 串口协议 | 扫码枪数据接收 |

### 业务功能插件

| 插件 | 功能 |
|------|------|
| `AP.Plugin.AirtightnessCheck` | 气密性检测流程（含报表） |
| `AP.Plugin.DeviceConfiguration` | 设备参数配置界面 |

### 系统功能插件

| 插件 | 功能 |
|------|------|
| `AP.Plugin.Layout` | 布局管理 / Sidebar 导航 |

---

## 关键设计模式

### 1. 策略模式 (Polly Pipeline Factory)

```csharp
// 根据配置键动态选择重试策略
var pipeline = resilienceFactory.GetPipeline("PLC-Retry");
await pipeline.ExecuteAsync(operation, ct);
```

### 2. 观察者模式 (MediatR EventBus)

插件通过发布/订阅事件通信，发布者和订阅者无需互相知道对方存在。

### 3. 状态模式 (PluginStateMachine)

每个插件的状态转换由状态机管理，确保合法性和可追踪性。

### 4. 策略模式 (Report Data Provider)

每个业务插件实现 `IReportDataProvider` 接口提供数据，报表框架统一处理生成和归档。

### 5. 依赖注入 + 模块化 (Prism)

使用 Prism 的模块化能力，每个插件的 `ConfigureServices` 方法向全局 DI 容器注册自己的服务。

### 6. 隔离上下文 (AssemblyLoadContext)

每个插件在独立的 `AssemblyLoadContext` 中运行，避免 DLL 版本冲突。

---

## 数据流与交互图

### 启动阶段

```
Bootstrapper
  │
  ├── 1. 读取 AppRole
  │
  ├── 2. PluginLoader.ScanAndLoad(pluginsDir, appRole)
  │       │
  │       ├── 2a. 扫描目录 → 发现匹配插件 DLL
  │       ├── 2b. 创建 AssemblyLoadContext
  │       ├── 2c. 加载程序集
  │       ├── 2d. 反射扫描 IPlugin 实现
  │       ├── 2e. 校验角色匹配
  │       └── 2f. 实例化插件 → 返回 List<PluginDescriptor>
  │
  ├── 3. PluginLifecycleManager.RegisterPlugins(descriptors)
  │       │
  │       └── 3a. 为每个插件创建 PluginStateMachine
  │           └── 3b. TransitionTo(Discovered → Loading → Loaded)
  │
  ├── 4. PluginLifecycleManager.InitializePluginsAsync()
  │       │
  │       └── 4a. 按优先级依次调用 InitializeAsync
  │           └── 4b. TransitionTo(Initializing → Initialized 或 Failed)
  │
  ├── 5. PluginLifecycleManager.StartPluginsAsync()
  │       │
  │       └── 5a. 按优先级依次调用 StartAsync
  │           └── 5b. TransitionTo(Starting → Running 或 Failed)
  │
  └── 6. 显示主窗口 / 启动 gRPC
```

### 运行时硬件通信

```
用户操作 (UI Plugin)
    │
    ▼
ViewModel.ExecuteCommand
    │
    ▼
[Polly Retry Pipeline]
    │
    ▼
PLC Service (通过 IPlcService 接口)
    │
    ├── ReadAsync / WriteAsync
    │       │
    │       ▼
    │   MitsubishiPlcDriver (MC 协议)
    │       │
    │       ▼
    │   TcpClient.Send/Receive
    │
    └── Watchdog (每 2 秒)
            │
            ├── 正常 → 无操作
            └── 超时 → Publish(DeviceConnectionFailedEvent)
                           │
                           ▼
                      MediatR → 通知 UI 更新连接状态
                               → 自动重连
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
               │   xlsx 文件写入
               │
               └── ReportStorage.Save(filePath, record)
                       │
                       ▼
                reports/2026/01/2026-01-12_Type.xlsx
```

---

## 扩展点

### 添加新的基础设施层模块

1. 在 `platform/infra/` 下创建类库项目
2. 定义接口（可放在 Contracts 或 Infra 内部）
3. 编写扩展方法 `AddXxx(this IServiceCollection, IConfiguration)`
4. 在宿主的 Bootstrapper 中调用注册方法

### 添加新的硬件驱动插件

1. 在 `platform/plugins/hardware/` 下创建类库项目
2. 实现硬件服务接口（定义在 `AP.Contracts.Hardware`）
3. 加上 `[PluginMetadata]` 和 `[RequiresCapabilities]` 特性
4. 在 `ConfigureServices` 中注册服务
5. 配置构建后事件自动复制到 `plugins/` 目录

### 添加新的业务插件

1. 在 `platform/plugins/business/` 下创建类库项目
2. 创建插件主类继承 `PluginBase`
3. 创建 View 和 ViewModel
4. 在 `InitializeAsync` 中注册 Region 视图
5. 可选实现 `IReportDataProvider` 提供报表能力

---

## 参考资料

- [使用指南](GETTING_STARTED.md) — 环境准备、快速开始、详细配置
- [测试指南](TESTING.md) — 测试编写规范与运行方式
- [README](../README.md) — 项目概览与技术栈

---

**最后更新**: 2026-07-14