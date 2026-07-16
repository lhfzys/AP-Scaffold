# AP-Scaffold — 工业自动化通用平台脚手架

基于 **.NET 8 + WPF + Prism + MediatR + Polly** 的高扩展性工业软件底座，专为快速构建 **上位机、MES 客户端、产线监控系统** 设计。

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
- 🔌 开箱即用的硬件通信能力（PLC、扫码枪等）
- 🛡️ 工业级容错与自愈机制（断线重连、断路器、看门狗）
- 🖥️ Material Design 风格的 WPF UI 控件库
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
| 运行时 | .NET 8 | 8.0 | 应用运行时 |
| UI 框架 | WPF + Prism | 9.0 | MVVM 导航、依赖注入、模块化 |
| DI 容器 | DryIoc (via Prism) | 6.2 | 高性能轻量 IoC 容器 |
| MVVM 工具 | CommunityToolkit.Mvvm | 8.4 | 属性通知、命令绑定 |
| 消息总线 | MediatR | 12.4 | 进程内 CQRS 消息分发 |
| UI 主题 | MaterialDesignThemes | 5.3 | MD3 风格控件 |
| 图表 | LiveChartsCore | 2.0-rc | 实时数据可视化 |
| 容错框架 | Polly | 8.6 | 重试、断路器、超时策略 |
| 日志 | Serilog | 4.3 | 结构化日志（文件 + 控制台） |
| 数据库 ORM | FreeSql | 3.5 | 支持 SQLite / PostgreSQL |
| gRPC | Grpc.AspNetCore | 2.76 | 服务端 / 客户端通信 |
| PLC 通信 | IoTClient | 1.0 | 三菱 MC 协议 |
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
│   │       └── Views/                 # 主窗口 XAML
│   │
│   ├── core/                          # 核心框架
│   │   └── AP.Core/
│   │       ├── PluginFramework/       # 插件加载、隔离上下文、元数据扫描
│   │       ├── Lifecycle/             # 插件生命周期管理
│   │       ├── StateMachine/          # 插件状态机
│   │       ├── Capability/            # 插件能力声明
│   │       ├── EventBus/              # MediatR 事件总线封装
│   │       └── Extensions/            # 框架扩展方法
│   │
│   ├── contracts/                     # 接口契约层 (核心与业务之间的桥梁)
│   │   ├── AP.Contracts.Core/         # 核心事件、错误模型
│   │   ├── AP.Contracts.Hardware/     # 硬件服务接口、设备事件
│   │   ├── AP.Contracts.Communication/ # gRPC 契约
│   │   ├── AP.Contracts.System/       # 系统服务接口
│   │   ├── AP.Contracts.Security/     # 安全/权限/审计日志契约
│   │   ├── AP.Contracts.Recipe/       # 配方管理契约
│   │   └── AP.Contracts.Report/       # 报表中心契约
│   │
│   ├── infra/                         # 基础设施层 (可复用的横切关注点)
│   │   ├── AP.Infra.Database/         # 数据访问 (FreeSql + Repository模式)
│   │   ├── AP.Infra.Grpc/             # gRPC 服务端/客户端/拦截器
│   │   ├── AP.Infra.Logging/          # Serilog 日志配置与增强器
│   │   ├── AP.Infra.Report/           # 通用报表框架 (归档/导出/清理)
│   │   ├── AP.Infra.Resilience/       # Polly 策略工厂与配置
│   │   ├── AP.Infra.Security/         # 安全/权限/审计日志实现
│   │   └── AP.Infra.Recipe/           # 配方管理实现
│   │
│   ├── plugins/                       # 插件集 (可插拔的业务/硬件模块)
│   │   ├── hardware/                  # 硬件驱动插件
│   │   │   ├── AP.Plugin.Plc.Mitsubishi/ # 三菱 PLC (MC协议 + 看门狗)
│   │   │   ├── AP.Plugin.Plc.Siemens/    # 西门子 PLC (S7协议)
│   │   │   └── AP.Plugin.Scanner/        # 串口扫码枪
│   │   ├── business/                  # 业务功能插件
│   │   │   ├── AP.Plugin.AirtightnessCheck/   # 气密性检测
│   │   │   └── AP.Plugin.DeviceConfiguration/ # 设备参数配置
│   │   └── system/                    # 系统功能插件
│   │       ├── AP.Plugin.Layout/      # 布局管理/Sidebar
│   │       ├── AP.Plugin.Login/       # 登录认证
│   │       ├── AP.Plugin.SystemSettings/ # 系统配置中心
│   │       ├── AP.Plugin.UserManagement/ # 用户管理
│   │       ├── AP.Plugin.RoleManagement/ # 角色管理
│   │       ├── AP.Plugin.AuditLog/    # 审计日志查看
│   │       ├── AP.Plugin.RecipeManagement/ # 配方管理
│   │       └── AP.Plugin.ReportCenter/ # 报表中心
│   │
│   └── shared/                        # 共享库
│       ├── AP.Shared.PluginSDK/       # 插件开发 SDK (PluginBase 基类)
│       ├── AP.Shared.UI/              # UI 控件库 (Loading/Dialog/Toast/主题)
│       └── AP.Shared.Utilities/       # 通用工具与常量
│
├── Directory.Build.props              # 全局编译属性
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
│  │ (PLC/串口)│ (气密/配置) │ (布局)     │      │
│  └────────── ─────────── ──────────────┘      │
├──────────────┬───────────────────────────────┤
│  Contracts   │  Infra (基础设施)              │
│  (接口契约)   │  Database │ gRPC │ Log │ Polly │
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
1. 扫描 `plugins/` 目录下的子文件夹
2. 创建隔离的 `AssemblyLoadContext`（支持热卸载）
3. 加载 DLL → 扫描 `IPlugin` 实现 → 读取 `[PluginMetadata]` 特性
4. 校验角色匹配（Server / Client / Standalone）
5. 按优先级排序 → 实例化 → 注册服务 → 初始化 → 启动

```csharp
// 定义一个插件只需这样
[PluginMetadata("MyPlugin", Name = "我的插件", Version = "1.0.0",
    SupportedRoles = AppRole.Server | AppRole.Standalone, Priority = 100)]
public class MyPlugin : PluginBase
{
    public MyPlugin(ILogger logger) : base(logger) { }
    
    public override void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // 注册插件自己的服务
        services.AddSingleton<IMyService, MyService>();
    }
}
```

### 2. 三菱 PLC 通信

完整的 MC 协议驱动，内置工业级可靠性保障：

| 特性 | 说明 |
|------|------|
| 基础读写 | 支持 bool/short/int/float 等多种数据类型 |
| 批量操作 | `ReadBatchAsync` / `WriteBatchAsync` |
| **看门狗** | `PeriodicTimer` 驱动，每 2 秒心跳检测 |
| **自动重连** | 检测掉线 → Polly 重试策略自动恢复 |
| **事件通知** | `DeviceConnectingEvent` → `DeviceConnectedEvent` / `DeviceConnectionFailedEvent` |

```json
// 配置示例
{
  "Plugins": {
    "Configuration": {
      "AP.Plugin.Plc.Mitsubishi": {
        "IpAddress": "192.168.1.10",
        "Port": 6000,
        "Version": "Qna_3E",
        "HeartbeatAddress": "M0",
        "Timeout": 3000
      }
    }
  }
}
```

### 3. MediatR 消息总线

插件之间**零引用依赖**，通过消息解耦：

```
插件A ──Publish(DeviceConnectedEvent)──→ MediatR ──→ 插件B 收到通知
                                            │
                                            └────→ 插件C 更新 UI
```

### 4. Polly 工业级容错

`ResiliencePipelineFactory` 统一管理策略，支持：

- **重试策略**：PLC 连接失败自动重试 N 次
- **断路器**：连续失败后熔断，避免雪崩
- **超时控制**：单次操作超时自动取消

```csharp
// 所有 PLC 操作自动受策略保护
var pipeline = resilienceFactory.GetPipeline("PLC-Retry");
await pipeline.ExecuteAsync(async token => {
    await plcService.ReadAsync<int>("D100", token);
}, ct);
```

### 5. gRPC 分布式通信

- **服务端**：在 Server 模式下自动启动 Kestrel + gRPC 服务
- **客户端**：在 Client 模式下启动后台 Worker 保持连接
- `StreamBroadcaster`：服务端向所有连接客户端广播消息
- `LoggingInterceptor`：统一的请求日志记录

### 6. Material Design UI

```
内置组件：
├── LoadingSpinner     # 加载动画
├── MaterialDialog     # 自定义对话框
├── Toast 通知         # 轻量级消息提示
├── BoolToVisibility   # 布尔值 ↔ 可见性
└── Industrial.Teal.MD3 # 工业风格主题
```

### 7. 通用报表框架

`AP.Infra.Report` 提供工业级报表归档能力，支持定时归档、手动导出、补档和定期清理。

| 特性 | 说明 |
|------|------|
| **定时归档** | 每天指定时间自动生成前一天的日报 |
| **手动导出** | 通过 `ReportService` 随时生成指定日期报表 |
| **补档** | 支持重新生成历史日期报表（覆盖或新建） |
| **定期清理** | 自动删除过期报表，保留天数可配置 |
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

---

## 快速开始

### 环境准备

- Visual Studio 2022（任意版本）
- .NET 8 SDK
- Windows 10/11

### 1. 克隆仓库

```bash
git clone <your-repo-url>
cd AP-Scaffold
```

### 2. 打开解决方案

用 Visual Studio 2022 打开 `AP-Automation.Platform.slnx`

### 3. 配置运行模式

项目支持三种运行模式，在 `AP.Host.Desktop/Configuration/` 下选择对应配置：

```
Configuration/
├── appsettings.json             # 基础配置（所有模式共用）
├── appsettings.Standalone.json  # 单机模式
├── appsettings.Server.json      # 服务端模式
└── appsettings.Client.json      # 客户端模式
```

### 4. 修改硬件配置

根据现场实际情况修改插件配置，例如三菱 PLC 的 IP 和端口：

```json
{
  "Plugins": {
    "Configuration": {
      "AP.Plugin.Plc.Mitsubishi": {
        "IpAddress": "192.168.1.10",
        "Port": 6000
      },
      "AP.Plugin.Scanner": {
        "PortName": "COM3"
      }
    }
  }
}
```

### 5. 运行

将 `AP.Host.Desktop` 设为启动项目，按 F5 运行。

---

## 配置说明

### 运行角色

项目通过 `AppRole` 枚举控制运行模式，使用位标志组合：

| 角色 | 说明 | 加载的插件范围 |
|------|------|----------------|
| `Standalone` | 单机模式（默认） | 所有 `Standalone` 标记的插件 |
| `Server` | 服务端（含 gRPC Server） | `Server` 标记的插件 + 启动 Kestrel |
| `Client` | 客户端（含 gRPC Client） | `Client` 标记的插件 + 启动 gRPC Worker |

一个插件可以同时支持多种模式：`SupportedRoles = AppRole.Server | AppRole.Standalone`

### Polly 策略配置

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

### gRPC 配置

```json
{
  "Grpc": {
    "ServerPort": 5000,
    "ClientAddress": "https://192.168.1.100:5000"
  }
}
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

**第 1 步**：在 `platform/plugins/` 下创建类库项目（按分类放到 `hardware/`、`business/` 或 `system/`）

**第 2 步**：创建插件主类，继承 `PluginBase`

```csharp
using AP.Core.Enums;
using AP.Core.PluginFramework.Attributes;
using AP.Shared.PluginSDK.Base;
using Microsoft.Extensions.Logging;

namespace AP.Plugin.MyFeature;

[PluginMetadata(
    "AP.Plugin.MyFeature",           // 插件 ID（必须与目录名及 DLL 名一致）
    Name = "我的功能插件",            // 显示名称
    Version = "1.0.0",
    SupportedRoles = AppRole.Server | AppRole.Standalone,
    Priority = 100                   // 越小越先加载
)]
public class MyPlugin : PluginBase
{
    public MyPlugin(ILogger logger) : base(logger) { }

    public override void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // 注册你的服务
        services.AddSingleton<IMyService, MyService>();
    }

    public override async Task InitializeAsync(IServiceProvider sp, CancellationToken ct)
    {
        await base.InitializeAsync(sp, ct);
        // 初始化资源（数据库迁移等）
    }

    public override async Task StartAsync(CancellationToken ct)
    {
        await base.StartAsync(ct);
        // 启动后台服务
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        // 清理资源
        await base.StopAsync(ct);
    }
}
```

**第 3 步**：将编译输出复制到 `plugins/<插件ID>/` 目录，或配置构建后事件自动复制

**第 4 步**：运行宿主，插件会被自动发现和加载

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

namespace AP.Plugin.AirtightnessCheck.Reports;

public class AirtightnessReportProvider : IReportDataProvider
{
    private readonly ITestRecordRepository _repository;

    public AirtightnessReportProvider(ITestRecordRepository repository)
    {
        _repository = repository;
    }

    // 报表类型标识（英文，用于文件命名）
    public string ReportType => "Airtightness";

    // 报表显示名称
    public string ReportName => "气密性检测日报";

    // 获取指定日期的报表数据
    public async Task<ReportData> GetReportDataAsync(DateTime date, CancellationToken ct = default)
    {
        // 从业务数据库查询数据
        var records = await _repository.GetByDateAsync(date, ct);

        return new ReportData
        {
            ReportType = ReportType,
            ReportName = ReportName,
            ReportDate = date,
            // 列标题
            Headers = ["序号", "激光码", "检测结果", "检测值", "检测时间", "操作员"],
            // 数据行
            Rows = records.Select((r, i) => new List<object>
            {
                i + 1, r.LaserCode, r.Result, r.TestValue, r.TestTime, r.Operator
            }).ToList(),
            // 汇总信息
            Summary = new Dictionary<string, object>
            {
                ["总数"] = records.Count,
                ["合格数"] = records.Count(r => r.Result == "Pass"),
                ["不合格数"] = records.Count(r => r.Result == "Fail"),
                ["合格率"] = records.Count > 0
                    ? $"{records.Count(r => r.Result == "Pass") * 100.0 / records.Count:F1}%"
                    : "N/A"
            }
        };
    }

    // 可选：指定 Excel 模板路径
    public string? GetTemplatePath() => null;
}
```

**第 3 步**：在插件中注册

```csharp
public class AirtightnessPlugin : PluginBase
{
    public override void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // 注册报表框架（如果宿主未注册）
        services.AddReportFramework(config);

        // 注册本插件的报表数据提供者
        services.AddReportDataProvider<AirtightnessReportProvider>();
    }
}
```

**第 4 步**：使用 `ReportService` 手动导出或补档

```csharp
// 注入 ReportService
public class ReportViewModel : ViewModelBase
{
    private readonly ReportService _reportService;

    [RelayCommand]
    private async Task ExportTodayReport()
    {
        // 生成今天的报表
        var path = await _reportService.GenerateReportAsync("Airtightness", DateTime.Today);
        // 打开文件所在目录
        Process.Start("explorer.exe", Path.GetDirectoryName(path));
    }

    [RelayCommand]
    private async Task RegenerateReport(DateTime date)
    {
        // 补档：重新生成指定日期的报表
        var path = await _reportService.RegenerateReportAsync("Airtightness", date);
    }
}
```

**报表文件输出示例：**

```
reports/
└── 2026/
    └── 01/
        ├── 2026-01-12_Airtightness.xlsx
        └── 2026-01-13_Airtightness.xlsx
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

---

## 测试覆盖

项目当前包含 **12 个测试文件**，覆盖核心业务逻辑、配置模型和实体对象。

### 测试项目结构

```
platform/tests/
├── AP.Core.Tests/                       # 核心框架测试（6 个）
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
├── AP.Shared.Tests/                     # 共享库测试（3 个）
│   ├── PluginSDK/
│   │   └── PluginBaseTests.cs                  # 插件基类
│   └── Utilities/
│       ├── SerializationHelperTests.cs         # JSON 序列化/反序列化
│       └── ConfigurationHelperTests.cs         # appsettings.json 配置更新
│
└── AP.Infra.Tests/                      # 基础设施层测试（3 个）
    ├── Report/
    │   ├── ReportOptionsTests.cs               # 报表配置选项
    │   └── ReportArchiveEntityTests.cs         # 报表归档实体
    └── Resilience/
        └── ResilienceOptionsTests.cs           # 弹性策略配置选项
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
| **[CHANGELOG.md](CHANGELOG.md)** | 版本变更日志 |
| **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** | 详细的分层架构设计文档 |
| **[docs/TESTING.md](docs/TESTING.md)** | 测试编写规范与运行指南 |
| **[docs/GETTING_STARTED.md](docs/GETTING_STARTED.md)** | 环境准备、配置步骤与插件开发示例 |

---

## 开发路线图

- [x] 插件化架构核心（动态加载、隔离上下文、生命周期）
- [x] 三菱 PLC 通信（MC 协议 + 看门狗自愈）
- [x] 串口扫码枪集成
- [x] gRPC 服务端 / 客户端通信
- [x] Polly 容错策略工厂
- [x] MediatR 消息总线
- [x] Material Design UI 控件库
- [x] 中央包版本管理
- [x] 通用报表框架（定时归档 / 手动导出 / 补档 / 定期清理）
- [x] 身份认证与授权（单机本地账号版）
- [x] 身份认证 UI（登录/用户管理/角色管理）
- [x] 审计日志查看界面
- [x] 配方/工艺参数管理（骨架）
- [x] 报表中心（骨架）
- [ ] 报表/配方接入真实业务数据
- [ ] OpenTelemetry 可观测性集成
- [ ] 更多 PLC 协议支持（西门子、欧姆龙）

---

## 许可证

[待定]
