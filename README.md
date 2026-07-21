# AP-Scaffold — 工业自动化通用平台脚手架

基于 **.NET 8 + WPF + Prism + MediatR + Polly** 的高扩展性工业软件底座，专为快速构建 **上位机、MES 客户端、产线监控系统** 设计。

脚手架的三大设计目标：

- **可快速复用** — 插件化架构 + 契约层解耦 + 声明式导航/设置贡献者模式，新功能以插件形式即插即用
- **安全可靠** — 本地用户/角色/权限体系、审计日志、PBKDF2 密码哈希、Polly 容错、PLC 看门狗自愈、崩溃日志
- **统一视觉** — 全浅色 Material Design 3 主题、统一控件/文字/间距资源、权限行为附加属性

---

## 📋 目录

- [项目简介](#项目简介)
- [技术栈](#技术栈)
- [项目结构](#项目结构)
- [核心特性](#核心特性)
- [快速开始](#快速开始)
- [配置说明](#配置说明)
- [插件开发指南](#插件开发指南)
- [部署模式](#部署模式)
- [测试覆盖](#测试覆盖)
- [开发路线图](#开发路线图)
- [文档参考](#文档参考)

---

## 项目简介

AP-Scaffold 是一个**框架级脚手架项目**，提供工业自动化软件的通用底座。它不是直接可用的业务系统，而是为业务开发提供：

- 🧩 可插拔的插件架构，核心与业务强制解耦
- 🧭 声明式导航与设置贡献者模式，新页面/新配置页零侵入接入
- 🔌 开箱即用的硬件通信能力（三菱/西门子 PLC、扫码枪），配置切换品牌
- 🛡️ 工业级容错与自愈机制（断线重连、断路器、看门狗）
- 🔐 完整的安全体系（登录、用户/角色/权限、审计日志，可一键关闭）
- 🖥️ 全浅色 Material Design 3 统一视觉主题
- 📡 进程内消息总线 + gRPC 分布式通信

### 适用场景

- 工厂产线数据采集与监控（SCADA 上位机）
- MES 制造执行系统客户端
- 设备自动化测试平台
- 工业物联网边缘网关

---

## 技术栈

| 分类 | 技术 | 版本 | 用途 |
|------|------|------|------|
| 运行时 | .NET 8 | 8.0 | 应用运行时（使用 .NET 10 SDK 构建，见 `global.json`） |
| UI 框架 | WPF + Prism | 9.0.537 | MVVM 导航、依赖注入、Region 管理 |
| DI 容器 | DryIoc (via Prism) | 6.2 | 高性能轻量 IoC 容器 |
| MVVM 工具 | CommunityToolkit.Mvvm | 8.4 | 属性通知、命令绑定 |
| 消息总线 | MediatR | 12.4 | 进程内 CQRS 消息分发 |
| UI 主题 | MaterialDesignThemes | 5.3 | Material Design 3 控件（全浅色主题） |
| 图表 | LiveChartsCore | 2.0-rc6 | 实时数据可视化 |
| 容错框架 | Polly | 8.6 | 重试、断路器、超时策略 |
| 日志 | Serilog | 4.3 | 结构化日志（文件 + 控制台） |
| 数据库 ORM | FreeSql | 3.5 | 支持 SQLite / PostgreSQL |
| gRPC | Grpc.AspNetCore | 2.76 | 服务端 / 客户端通信 |
| PLC 通信 | IoTClient | 1.0 | 三菱 MC 协议 / 西门子 S7 协议 |
| 串口通信 | System.IO.Ports | 4.6 | 串口扫码枪等设备 |
| Excel | MiniExcel | 1.42 | 轻量级 Excel 导入导出 |

---

## 项目结构

```
AP-Scaffold/
├── platform/
│   ├── hosts/                         # 启动宿主
│   │   └── AP.Host.Desktop/           # WPF 桌面端启动项目
│   │       ├── Bootstrapping/         # 启动器 (角色识别、插件加载、gRPC启动)
│   │       ├── Configuration/         # 多角色配置文件
│   │       ├── ViewModels/            # 主窗口 ViewModel
│   │       └── Views/                 # 主窗口/Splash XAML
│   │
│   ├── core/                          # 核心框架
│   │   └── AP.Core/
│   │       ├── PluginFramework/       # 插件加载、隔离上下文、元数据扫描
│   │       ├── Lifecycle/             # 插件生命周期管理
│   │       ├── StateMachine/          # 插件状态机（14 种状态）
│   │       ├── Capability/            # 插件能力声明
│   │       ├── EventBus/              # MediatR 事件总线封装
│   │       └── Extensions/            # 框架扩展方法
│   │
│   ├── contracts/                     # 接口契约层 (核心与业务之间的桥梁)
│   │   ├── AP.Contracts.Core/         # OperationResult、ErrorCode、应用事件
│   │   ├── AP.Contracts.Hardware/     # IPlcService、IPlcDriverFactory、PlcOptions、设备事件
│   │   ├── AP.Contracts.Communication/ # gRPC proto 契约
│   │   ├── AP.Contracts.System/       # 系统服务接口（登录/设置对话框）
│   │   ├── AP.Contracts.Security/     # 安全/权限/审计日志契约
│   │   ├── AP.Contracts.Recipe/       # 配方管理契约（IRecipeManager）
│   │   └── AP.Contracts.Report/       # 报表中心契约（IReportCenterService）
│   │
│   ├── infra/                         # 基础设施层 (可复用的横切关注点)
│   │   ├── AP.Infra.Database/         # 数据访问 (FreeSql + Repository模式)
│   │   ├── AP.Infra.Grpc/             # gRPC 服务端/客户端/拦截器
│   │   ├── AP.Infra.Hardware/         # PLC 驱动注册表与统一激活服务
│   │   ├── AP.Infra.Logging/          # Serilog 日志配置与清理
│   │   ├── AP.Infra.Report/           # 通用报表框架 (归档/导出/清理)
│   │   ├── AP.Infra.Resilience/       # Polly 策略工厂与配置
│   │   ├── AP.Infra.Security/         # 安全/权限/审计日志实现
│   │   └── AP.Infra.Recipe/           # 配方管理实现
│   │
│   ├── plugins/                       # 插件集 (可插拔的业务/硬件模块)
│   │   ├── hardware/                  # 硬件驱动插件
│   │   │   ├── AP.Plugin.Plc.Mitsubishi/ # 三菱 PLC (MC协议 + 看门狗)
│   │   │   ├── AP.Plugin.Plc.Siemens/    # 西门子 PLC (S7协议 + 看门狗)
│   │   │   └── AP.Plugin.Scanner/        # 串口扫码枪
│   │   ├── business/                  # 业务功能插件
│   │   │   ├── AP.Plugin.AirtightnessCheck/   # 气密性检测（UI 骨架）
│   │   │   └── AP.Plugin.DeviceConfiguration/ # 设备参数配置（设置贡献者）
│   │   └── system/                    # 系统功能插件
│   │       ├── AP.Plugin.Layout/      # 布局管理/Sidebar/仪表盘
│   │       ├── AP.Plugin.Login/       # 登录认证
│   │       ├── AP.Plugin.SystemSettings/ # 系统配置中心
│   │       ├── AP.Plugin.UserManagement/ # 用户管理
│   │       ├── AP.Plugin.RoleManagement/ # 角色管理
│   │       ├── AP.Plugin.AuditLog/    # 审计日志查看
│   │       ├── AP.Plugin.RecipeManagement/ # 配方管理
│   │       └── AP.Plugin.ReportCenter/ # 报表中心
│   │
│   └── shared/                        # 共享库
│       ├── AP.Shared.PluginSDK/       # 插件 SDK (PluginBase/导航贡献者/设置贡献者)
│       ├── AP.Shared.UI/              # UI 控件库 (LoadingSpinner/对话框/权限行为/浅色主题)
│       └── AP.Shared.Utilities/       # 通用工具与常量
│
├── installer/                         # Inno Setup 安装包脚本
├── docs/                              # 架构/使用/测试/状态文档
├── Directory.Build.props              # 全局编译属性（含插件输出规则）
├── Directory.Packages.props           # 中央包版本管理
└── AP-Automation.Platform.slnx        # 解决方案文件
```

### 分层架构图

```
┌──────────────────────────────────────────────┐
│  AP.Host.Desktop (启动宿主)                    │
│  根据 AppRole 动态加载对应插件组合              │
├──────────────────────────────────────────────┤
│  Plugins (插件层)                              │
│  ┌────────── ─────────── ──────────────┐      │
│  │ Hardware │ Business  │  System     │      │
│  │(PLC/串口)│ (气密/配置) │(布局/登录等)│      │
│  └────────── ─────────── ──────────────┘      │
├──────────────┬───────────────────────────────┤
│  Contracts   │  Infra (基础设施)              │
│  (接口契约)   │  Database│gRPC│Report│Security │
├──────────────┴───────────────────────────────┤
│  AP.Core (核心框架)                            │
│  PluginFramework │ EventBus │ StateMachine    │
├──────────────────────────────────────────────┤
│  Shared (共享库)                               │
│  PluginSDK │ UI │ Utilities                   │
└──────────────────────────────────────────────┘
```

---

## 核心特性

### 1. 插件化架构

每个业务模块以**独立 DLL** 形式存在，运行时动态发现和加载。

**加载流程：**
1. 扫描 `plugins/{插件名}/{插件名}.dll`
2. 为每个插件创建隔离的 `PluginLoadContext`（AssemblyLoadContext）
3. 加载 DLL → 扫描 `IPlugin` 实现 → 读取 `[PluginMetadata]` 特性
4. 校验角色匹配（Server / Client / Standalone）
5. 按优先级排序 → 实例化 → 收集服务注册 → 初始化 → 启动

```csharp
// 定义一个插件只需这样
[PluginMetadata("AP.Plugin.MyPlugin", Name = "我的插件", Version = "1.0.0",
    SupportedRoles = AppRole.Server | AppRole.Standalone, Priority = 100)]
public class MyPlugin : PluginBase
{
    public MyPlugin(ILogger<MyPlugin> logger) : base(logger) { }

    public override void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // 注册插件自己的服务
        services.AddSingleton<IMyService, MyService>();
    }
}
```

### 2. 声明式导航贡献者模式

新增页面无需修改任何现有代码，插件实现 `INavigationContributor` 即可出现在侧边栏菜单：

```csharp
public class MyPlugin : PluginBase, INavigationContributor
{
    public IEnumerable<NavigationMenuItem> GetMenuItems()
    {
        yield return new NavigationMenuItem
        {
            Label = "我的页面",
            IconKind = "ViewDashboard",      // Material Design 图标名
            NavigationTarget = "MyView",     // 需注册到 ContentRegion 的视图名
            Order = 1500,                    // 菜单排序（越小越靠前）
            Permission = "mypage.view",      // 可选：所需权限码
            IsDefault = false                // 可选：是否为启动默认页
        };
    }
}
```

菜单由 `SidebarViewModel` 统一构建：按 `NavigationTarget` 去重 → 按 Order 排序 → 按当前用户权限过滤 → 按配置白名单过滤（Security 关闭时）。默认选中项延迟到 UI 就绪后自动导航。

### 3. 设置贡献者模式

新增配置页同样零侵入，实现 `ISettingsContributor` 即自动出现在系统配置中心：

- 按 `Category` 分组（如"系统"/"硬件"），统一保存/校验/备份流程
- 保存时自动备份 `appsettings.json` → 写回配置 → 提示需要重启的项
- 现有贡献者：应用基础信息、PLC 配置（SystemSettings 插件）、扫码枪配置（DeviceConfiguration 插件）

### 4. PLC 通信与品牌统一切换

三菱（MC 协议）与西门子（S7 协议）驱动均已内置，通过配置即可切换品牌：

```json
{
  "Plc": {
    "DriverType": "Mitsubishi",   // 或 "Siemens"（预留 "Omron"）
    "IpAddress": "192.168.1.10",
    "Port": 6000,                 // 西门子默认 102
    "Timeout": 1000,
    "Model": "Qna_3E",            // 西门子如 "S7_1200"
    "HeartbeatAddress": "D0.0"    // 西门子如 "DB1.0.0"
  }
}
```

| 特性 | 说明 |
|------|------|
| 统一抽象 | 业务代码只依赖 `IPlcService`，`ActivePlcService` 按 `DriverType` 转发到真实驱动 |
| 基础读写 | 支持 bool/short/ushort/int/uint/float（西门子另支持 string） |
| 批量操作 | `IPlcBatchReadWrite`（西门子为真批量，三菱为循环单点） |
| **看门狗** | 2 秒 `PeriodicTimer` 心跳检测，掉线自动重连（失败退避 5 秒） |
| **事件通知** | `DeviceConnectingEvent` → `DeviceConnectedEvent` / `DeviceConnectionFailedEvent` |

新增品牌（如欧姆龙）只需实现 `IPlcDriverFactory` 并注册到 DI，业务代码零修改。

### 5. MediatR 消息总线

插件之间**零引用依赖**，通过消息解耦：

```
插件A ──Publish(DeviceConnectedEvent)──→ MediatR ──→ 插件B 收到通知
                                            │
                                            └────→ 插件C 更新 UI
```

同时提供 `MediatRToPrismBridge`，将关键的 MediatR 事件桥接到 Prism `IEventAggregator`（PLC 数据变更、扫码完成、设备断开）。

### 6. Polly 工业级容错

`ResiliencePipelineFactory` 统一管理策略，内置三条管道：

| 管道 Key | 用途 |
|----------|------|
| `Database-Retry` | 数据库操作指数退避重试 |
| `PLC-Retry` | PLC 连接/读写固定间隔重试 |
| `Grpc-CircuitBreaker` | gRPC 调用熔断保护 |

```csharp
var pipeline = resilienceFactory.GetPipeline(ResiliencePipelineFactory.Keys.Plc);
await pipeline.ExecuteAsync(async token => {
    await plcService.ReadAsync<short>("D100", token);
}, ct);
```

### 7. gRPC 分布式通信

- **服务端**：Server 模式下内嵌 Kestrel（仅 HTTP/2）+ gRPC 服务，proto 契约定义于 `AP.Contracts.Communication`
- **客户端**：Client 模式下 `GrpcClientWorker` 保持连接，接收数据流并经 MediatR 转发
- `StreamBroadcaster`：基于 `Channel<T>` 的服务端广播，按客户端背压
- `LoggingInterceptor`：统一的请求日志记录

### 8. 安全体系（可开关）

- 本地用户/角色/权限（12 个种子权限、3 个默认角色），PBKDF2-SHA256 密码哈希（10 万次迭代）
- 登录窗口 + 首次登录强制改密（默认账号 `admin / admin123`）
- 审计日志：登录/登出/改密/用户角色操作等自动记录，可视化查询
- 视图级权限控制：`PermissionBehavior` 附加属性（无权限时隐藏或禁用）
- `Security:Enabled=false` 一键关闭：跳过登录、注入匿名身份（拥有全部权限）、菜单按白名单过滤

### 9. 统一视觉（全浅色 Material Design 3）

- 主题文件 `Industrial.Teal.MD3.xaml`：主色深蓝 `#1E3A5F`、强调色青蓝 `#0891B2`，全套语义色
- 统一资源键：表面色、文字样式（`TextStyle.*`）、间距（`Layout.Spacing.*`）、圆角、海拔
- 内置组件：`LoadingSpinner`、对话框服务（`ICustomDialogService`：Alert/Confirm/Error）、4 个转换器、`PermissionBehavior`

### 10. 通用报表框架

`AP.Infra.Report` 提供工业级报表归档能力，支持定时归档、手动导出、补档和定期清理。

| 特性 | 说明 |
|------|------|
| **定时归档** | 每天指定时间自动生成前一天的日报（`ReportScheduler`） |
| **手动导出** | 通过 `ReportService` 随时生成指定日期报表 |
| **补档** | 支持重新生成历史日期报表（覆盖或新建） |
| **定期清理** | 自动删除过期报表，保留天数可配置（`ReportCleanupService`） |
| **保护机制** | 受保护的报表类型不被自动清理 |
| **模拟运行** | DryRun 模式预览清理结果，防止误删 |

**架构设计：**

```
业务插件                          报表框架
┌─────────────┐                  ┌─────────────────────┐
│ 检测完成     │   实现接口        │  ReportService      │
│ 业务数据     │────────────────▶│  ExcelExporter      │
│ 各自不同     │  IReportDataProvider   │  ReportStorage      │
└─────────────┘                  │  ReportScheduler    │
                                 │  ReportCleanupService│
                                 └─────────────────────┘
                                           │
                                           ▼
                                 reports/2026/01/2026-01-12_Airtightness.xlsx
```

> 注意：`IReportDataProvider` 定义在 `AP.Infra.Report`（非契约层）；`ReportScheduler` / `ReportCleanupService` 以 `IHostedService` 注册，而宿主不会自动启动 `IHostedService`（详见 `AGENTS.md` 5.6）。

---

## 快速开始

### 环境准备

- Visual Studio 2022（任意版本）
- .NET 10 SDK（构建用，目标框架仍为 .NET 8；见 `global.json`）
- Windows 10/11

### 1. 克隆仓库

```bash
git clone <your-repo-url>
cd AP-Scaffold
```

### 2. 打开解决方案

用 Visual Studio 2022 打开 `AP-Automation.Platform.slnx`

### 3. 配置运行模式

项目支持三种运行模式，配置文件位于 `platform/hosts/AP.Host.Desktop/Configuration/`：

```
Configuration/
├── appsettings.json             # 基础配置（所有模式共用）
├── appsettings.Standalone.json  # 单机模式
├── appsettings.server.json      # 服务端模式（注意：文件名小写）
└── appsettings.Client.json      # 客户端模式
```

通过 `appsettings.json` 的 `AppRole` 键或命令行参数 `--role=Server` 选择模式。

### 4. 修改硬件配置

根据现场实际情况修改统一的 `Plc` 配置节和扫码枪配置：

```json
{
  "Plc": {
    "DriverType": "Mitsubishi",
    "IpAddress": "192.168.1.10",
    "Port": 6000,
    "Model": "Qna_3E",
    "HeartbeatAddress": "D0.0"
  },
  "Plugins": {
    "Configuration": {
      "AP.Plugin.Scanner": {
        "PortName": "COM3"
      }
    }
  }
}
```

> PLC 配置也可以直接在应用内修改：系统配置 → 硬件 → PLC 配置（切换品牌自动填默认值，保存后需重启）。

### 5. 运行

将 `AP.Host.Desktop` 设为启动项目，按 F5 运行。默认 `Security:Enabled=true`，使用 `admin / admin123` 登录（首次登录强制改密）。

---

## 配置说明

### 运行角色

项目通过 `AppRole` 枚举控制运行模式，使用位标志组合（`Client=1, Server=2, Standalone=4, All=7`）：

| 角色 | 说明 | 加载的插件范围 |
|------|------|----------------|
| `Standalone` | 单机模式（默认） | 所有 `Standalone` 标记的插件 |
| `Server` | 服务端（含 gRPC Server） | `Server` 标记的插件 + 启动 Kestrel |
| `Client` | 客户端（含 gRPC Client） | `Client` 标记的插件 + 启动 gRPC Worker |

一个插件可以同时支持多种模式：`SupportedRoles = AppRole.Server | AppRole.Standalone`

### 应用与导航

```json
{
  "AppConfiguration": {
    "MachineId": "Station-01",
    "MachineName": "一号工位电脑",
    "CompanyName": "自动化系统",
    "SoftwareName": "气密检测监控系统",
    "LayoutMode": "Standard",
    "DefaultNavigationTarget": "DashboardView",
    "NavigationWhenSecurityDisabled": ["DashboardView", "SettingsShellView", "RecipeListView", "ReportListView"]
  }
}
```

| 配置项 | 说明 |
|--------|------|
| `DefaultNavigationTarget` | 启动后默认选中的菜单页（视图名） |
| `NavigationWhenSecurityDisabled` | `Security:Enabled=false` 时 Sidebar 只显示这些视图 |
| `LayoutMode` | `Standard`（带 Sidebar）/ `SinglePage`（单页） |

### 数据库

```json
{
  "Database": {
    "Provider": "SQLite",
    "SQLite": { "ConnectionString": "Data Source=local_data.db;Version=3;" },
    "PostgreSQL": { "ConnectionString": "Host=localhost;Port=5432;Database=automation_db;Username=postgres;Password=***" }
  }
}
```

> 注意：`UseAutoSyncStructure` 已关闭，数据库表由各模块初始化器显式创建。SQLite 启动前会自动备份数据库文件。

### 安全

```json
{
  "Security": {
    "Enabled": true,
    "Audit": { "Enabled": true }
  }
}
```

### Polly 策略配置

```json
{
  "Resilience": {
    "DatabaseRetryCount": 3,
    "PlcRetryCount": 5,
    "GrpcCircuitBreakerThreshold": 5,
    "CircuitBreakerDurationSeconds": 30
  }
}
```

### gRPC 配置

```json
// 服务端
{ "Grpc": { "ServerPort": 5000 } }

// 客户端
{ "Grpc": { "ServerUrl": "http://192.168.1.100:5000", "ClientName": "Station-01" } }
```

### 报表配置

```json
{
  "Report": {
    "Storage": {
      "RootPath": "reports",
      "PathFormat": "{year}/{month}/{date}_{type}.xlsx",
      "DefaultTemplatePath": null
    },
    "Archive": {
      "Enabled": true,
      "Time": "02:00",
      "ReportTypes": []
    },
    "Retention": {
      "Enabled": true,
      "Days": 180,
      "CheckInterval": "1.00:00:00",
      "DeleteFiles": true,
      "ProtectedTypes": []
    },
    "Cleanup": {
      "Enabled": true,
      "DryRun": false
    }
  }
}
```

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `Storage.RootPath` | 报表存储根目录 | `reports` |
| `Storage.PathFormat` | 路径格式模板，支持 `{year}` `{month}` `{date}` `{type}` | `{year}/{month}/{date}_{type}.xlsx` |
| `Archive.Enabled` | 是否启用定时归档 | `true` |
| `Archive.Time` | 每天归档执行时间（HH:mm） | `02:00` |
| `Archive.ReportTypes` | 需归档的报表类型，空则归档所有 | `[]` |
| `Retention.Days` | 报表保留天数 | `180` |
| `Retention.DeleteFiles` | 清理时是否删除文件（false 仅删数据库记录） | `true` |
| `Retention.ProtectedTypes` | 受保护的报表类型（不被自动清理） | `[]` |
| `Cleanup.DryRun` | 模拟运行模式（true = 只记录不删除） | `false` |

---

## 插件开发指南

### 新建一个插件

**第 1 步**：在 `platform/plugins/` 下创建类库项目（按分类放到 `hardware/`、`business/` 或 `system/`）。`Directory.Build.props` 会自动把 `AP.Plugin.*` 项目的输出定向到 `bin/$(Configuration)/plugins/{插件名}/`，并清理与宿主重复的共享 DLL —— **无需配置任何构建后事件**。

**第 2 步**：创建插件主类，继承 `PluginBase`；如需出现在侧边栏，同时实现 `INavigationContributor`

```csharp
using AP.Core.Enums;
using AP.Core.PluginFramework.Attributes;
using AP.Shared.PluginSDK.Base;
using AP.Shared.PluginSDK.Navigation;

namespace AP.Plugin.MyFeature;

[PluginMetadata(
    "AP.Plugin.MyFeature",           // 插件 ID（必须与目录名及 DLL 名一致）
    Name = "我的功能插件",            // 显示名称
    Version = "1.0.0",
    SupportedRoles = AppRole.Server | AppRole.Standalone,
    Priority = 100                   // 越小越先加载
)]
public class MyFeaturePlugin : PluginBase, INavigationContributor
{
    public MyFeaturePlugin(ILogger<MyFeaturePlugin> logger) : base(logger) { }

    public override void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddTransient<MyFeatureView>();
        services.AddTransient<MyFeatureViewModel>();
    }

    public override async Task InitializeAsync(IServiceProvider sp, CancellationToken ct)
    {
        await base.InitializeAsync(sp, ct);
        // 按权限条件注册视图到内容区（门控与菜单 Permission 保持一致）
        var identity = sp.GetRequiredService<IIdentityService>();
        if (identity.HasPermission("myfeature.view"))
        {
            var regionManager = sp.GetRequiredService<IRegionManager>();
            regionManager.RegisterViewWithRegion("ContentRegion", typeof(MyFeatureView));
        }
    }

    public IEnumerable<NavigationMenuItem> GetMenuItems()
    {
        yield return new NavigationMenuItem
        {
            Label = "我的功能",
            IconKind = "Puzzle",
            NavigationTarget = "MyFeatureView",
            Order = 1500,
            Permission = "myfeature.view"
        };
    }
}
```

**第 3 步**：创建 View / ViewModel。View 统一采用**构造函数注入 ViewModel**（全仓库约定，不使用 `AutoWireViewModel`）：

```csharp
public partial class MyFeatureView : UserControl
{
    public MyFeatureView(MyFeatureViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
```

**第 4 步**：构建解决方案，插件会被自动发现和加载。

### 插件间通信

使用 MediatR 发送事件，无需直接引用目标插件：

```csharp
// 发布事件
await mediator.Publish(new MyEvent { Data = "hello" });

// 订阅事件（在任意插件中）
public class MyEventHandler : INotificationHandler<MyEvent>
{
    public Task Handle(MyEvent evt, CancellationToken ct)
    {
        // 处理事件
        return Task.CompletedTask;
    }
}
```

### 为业务插件添加报表能力

**第 1 步**：在业务插件中引用报表框架

```xml
<!-- 在 .csproj 中添加 -->
<ProjectReference Include="..\..\..\infra\AP.Infra.Report\AP.Infra.Report.csproj" />
```

**第 2 步**：实现 `IReportDataProvider` 接口

```csharp
using AP.Infra.Report.Abstractions;
using AP.Infra.Report.Entities;

namespace AP.Plugin.MyFeature.Reports;

public class MyReportProvider : IReportDataProvider
{
    // 报表类型标识（英文，用于文件命名）
    public string ReportType => "MyReport";

    // 报表显示名称
    public string ReportName => "我的业务日报";

    // 获取指定日期的报表数据
    public async Task<ReportData> GetReportDataAsync(DateTime date, CancellationToken ct = default)
    {
        var records = await LoadRecordsAsync(date, ct);   // 查询业务数据

        return new ReportData
        {
            ReportType = ReportType,
            ReportName = ReportName,
            ReportDate = date,
            Headers = ["序号", "产品编号", "检测结果", "检测时间"],
            Rows = records.Select((r, i) => new List<object>
            {
                i + 1, r.ProductCode, r.Result, r.TestTime
            }).ToList(),
            Summary = new Dictionary<string, object>
            {
                ["总数"] = records.Count,
                ["合格数"] = records.Count(r => r.Result == "Pass")
            }
        };
    }

    // 可选：指定 Excel 模板路径
    public string? GetTemplatePath() => null;
}
```

**第 3 步**：在插件中注册

```csharp
public override void ConfigureServices(IServiceCollection services, IConfiguration config)
{
    // 注册本插件的报表数据提供者（报表框架由宿主统一注册）
    services.AddReportDataProvider<MyReportProvider>();
}
```

**第 4 步**：使用 `ReportService` 手动导出或补档

```csharp
// 生成今天的报表
var path = await _reportService.GenerateReportAsync("MyReport", DateTime.Today);

// 补档：重新生成指定日期的报表
var path = await _reportService.RegenerateReportAsync("MyReport", date);
```

**报表文件输出示例：**

```
reports/
└── 2026/
    └── 01/
        ├── 2026-01-12_MyReport.xlsx
        └── 2026-01-13_MyReport.xlsx
```

---

## 部署模式

```
┌──────────────┐     gRPC      ┌──────────────┐
│  Server 端    │◄────────────►│  Client 端    │
│  (含PLC连接)  │   Stream       │  (展示/操作)  │
│  + Kestrel   │   Broadcaster  │  + gRPC Worker│
└──────────────┘               └──────────────┘

         ┌──────────────┐
         │  Standalone  │
         │  (单机运行)   │
         │  PLC+UI 合体  │
         └──────────────┘
```

| 模式 | 适用场景 |
|------|----------|
| **Standalone** | 工控一体机，直接连 PLC 并显示界面 |
| **Server** | 作为数据网关，连接硬件并对外提供 gRPC 服务 |
| **Client** | 远程监控终端，通过 gRPC 获取服务端数据 |

安装包：`installer/setup.iss`（Inno Setup 6），先 `dotnet publish -c Release` 再编译脚本，安装时自动检测 .NET 8 桌面运行时。

---

## 测试覆盖

项目当前包含 **17 个测试文件 / 213 个测试**（全部通过），覆盖核心框架、共享库与基础设施配置。

### 测试项目结构

```
platform/tests/
├── AP.Core.Tests/                       # 核心框架测试（8 个文件 / 123 个测试）
│   ├── StateMachine/
│   │   ├── StateTransitionValidatorTests.cs   # 状态转换验证
│   │   └── PluginStateMachineTests.cs         # 插件状态机
│   ├── Capability/
│   │   └── PluginCapabilitiesTests.cs         # 插件能力声明
│   ├── EventBus/
│   │   └── EventBusTests.cs                   # MediatR 事件总线
│   ├── Lifecycle/
│   │   └── PluginLifecycleManagerTests.cs     # 插件生命周期管理
│   └── PluginFramework/
│       ├── PluginMetadataAttributeTests.cs    # 插件元数据特性
│       ├── RequiresCapabilitiesAttributeTests.cs  # 能力依赖特性
│       └── PluginInterfaceTests.cs            # IPlugin 接口契约
│
├── AP.Shared.Tests/                     # 共享库测试（4 个文件 / 46 个测试）
│   ├── PluginSDK/
│   │   ├── PluginBaseTests.cs                 # 插件基类
│   │   └── NavigationMenuItemBuilderTests.cs  # 导航菜单构建器
│   └── Utilities/
│       ├── SerializationHelperTests.cs        # JSON 序列化/反序列化
│       └── ConfigurationHelperTests.cs        # appsettings.json 配置更新
│
└── AP.Infra.Tests/                      # 基础设施层测试（5 个文件 / 44 个测试）
    ├── Hardware/
    │   ├── ActivePlcServiceTests.cs           # PLC 统一激活服务
    │   └── PlcDriverRegistryTests.cs          # PLC 驱动注册表
    ├── Report/
    │   ├── ReportOptionsTests.cs              # 报表配置选项
    │   └── ReportArchiveEntityTests.cs        # 报表归档实体
    └── Resilience/
        └── ResilienceOptionsTests.cs          # 弹性策略配置选项
```

### 技术栈

| 工具 | 用途 |
|------|------|
| xUnit | 测试框架 |
| NSubstitute | 接口模拟 / Mock |
| FluentAssertions | 可读性断言 |

> 详细的测试编写规范、命令运行指南和覆盖率目标，请参阅 **[docs/TESTING.md](docs/TESTING.md)**。

### 运行测试

```bash
# 运行所有测试
dotnet test

# 运行特定项目测试
dotnet test platform/tests/AP.Core.Tests/AP.Core.Tests.csproj
dotnet test platform/tests/AP.Shared.Tests/AP.Shared.Tests.csproj
dotnet test platform/tests/AP.Infra.Tests/AP.Infra.Tests.csproj
```

---

## 文档参考

| 文档 | 说明 |
|------|------|
| **[AGENTS.md](AGENTS.md)** | AI 协作交接文档（核心约定与坑点） |
| **[CHANGELOG.md](CHANGELOG.md)** | 版本变更日志 |
| **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** | 详细的分层架构设计文档 |
| **[docs/GETTING_STARTED.md](docs/GETTING_STARTED.md)** | 环境准备、配置步骤与插件开发示例 |
| **[docs/TESTING.md](docs/TESTING.md)** | 测试编写规范与运行指南 |
| **[docs/PROJECT_STATUS.md](docs/PROJECT_STATUS.md)** | 项目状态与工作计划 |

---

## 开发路线图

- [x] 插件化架构核心（动态加载、隔离上下文、生命周期）
- [x] 声明式导航贡献者模式 + 设置贡献者模式
- [x] 三菱 PLC 通信（MC 协议 + 看门狗自愈）
- [x] 西门子 PLC 通信（S7 协议 + 统一驱动切换）
- [x] 串口扫码枪集成
- [x] gRPC 服务端 / 客户端通信
- [x] Polly 容错策略工厂
- [x] MediatR 消息总线
- [x] Material Design 3 全浅色统一主题
- [x] 中央包版本管理
- [x] 通用报表框架（定时归档 / 手动导出 / 补档 / 定期清理）
- [x] 身份认证与授权（本地用户/角色/权限 + 登录 + 强制改密）
- [x] 用户管理 / 角色管理 / 审计日志可视化
- [x] 配方/工艺参数管理（骨架）
- [x] 报表中心（骨架）
- [ ] 报表/配方接入真实业务数据
- [ ] 欧姆龙 PLC 协议支持
- [ ] OpenTelemetry 可观测性集成

---

## 许可证

[待定]
