# AP-Scaffold 使用指南

本文档介绍如何使用 AP-Scaffold 框架快速开发工业上位机应用。内容以当前实际代码为准。

---

## 目录

- [环境准备](#环境准备)
- [快速开始](#快速开始)
- [创建业务插件](#创建业务插件)
- [配置说明](#配置说明)
- [报表框架使用](#报表框架使用)
- [PLC 通信使用](#plc-通信使用)
- [常见问题](#常见问题)
- [项目结构说明](#项目结构说明)

---

## 环境准备

### 必需工具

- **Visual Studio 2022**（任意版本，推荐 Professional 或 Enterprise）
- **.NET 10 SDK**（构建 SDK，目标框架仍为 .NET 8；`global.json` 指定 10.0.102，[下载地址](https://dotnet.microsoft.com/download)）
- **Windows 10/11**（WPF 应用仅支持 Windows）

### 验证安装

```bash
# 检查 .NET SDK 版本
dotnet --version
# 应输出 10.0.x（rollForward: latestMinor）
```

---

## 快速开始

### 1. 克隆项目

```bash
git clone git@github.com:lhfzys/AP-Scaffold.git
cd AP-Scaffold
```

### 2. 打开解决方案

用 Visual Studio 2022 打开 `AP-Automation.Platform.slnx`

### 3. 配置运行模式

在 `platform/hosts/AP.Host.Desktop/Configuration/` 目录下，编辑 `appsettings.json`：

```json
{
  "AppRole": "Standalone"  // 单机模式（默认）
}
```

支持的运行模式：
- `Standalone` - 单机模式（PLC + UI 在同一台机器，**当前唯一受支持**）
- `Server` ❄ - 服务端模式（仅连接 PLC，提供 gRPC 服务；冻结，未支持）
- `Client` ❄ - 客户端模式（仅 UI，通过 gRPC 连接服务端；冻结，未支持）

也可以用命令行参数覆盖：`AP.Host.Desktop.exe --role=Server`（❄ 冻结角色请勿使用）

### 4. 配置数据库

默认使用 SQLite，无需额外配置（`appsettings.Standalone.json` 中为 `local_data.db`）。**当前仅支持 SQLite**；PostgreSQL 配置保留但属于 ❄ 冻结能力（未维护、未验证），如需启用请自行验证：

```json
{
  "Database": {
    "Provider": "PostgreSQL",
    "PostgreSQL": {
      "ConnectionString": "Host=localhost;Port=5432;Database=automation_db;Username=postgres;Password=your_password"
    }
  }
}
```

> 数据库表不会自动创建（`UseAutoSyncStructure` 已关闭），由各模块初始化器在启动时显式建表并写入种子数据。

### 5. 运行项目

将 `AP.Host.Desktop` 设为启动项目，按 F5 运行。

默认 `Security:Enabled=false`，免登录直接进入主界面（注入匿名身份，拥有全部权限，菜单按白名单过滤）。如需登录与权限控制，将 `Security:Enabled` 设为 `true`，启动时会弹出登录窗口：使用 `admin / admin123` 登录，**首次登录强制修改密码**。

---

## 创建业务插件

### 1. 创建插件项目

在 `platform/plugins/business/` 目录下创建新的类库项目：

```bash
cd platform/plugins/business
dotnet new classlib -n AP.Plugin.YourFeature
cd AP.Plugin.YourFeature
```

> `Directory.Build.props` 约定：项目名以 `AP.Plugin` 开头即视为插件项目，构建输出自动定向到 `bin/$(Configuration)/plugins/{插件名}/`，并自动清理与宿主重复的共享 DLL。**无需配置任何构建后事件。**

### 2. 添加项目引用

编辑 `AP.Plugin.YourFeature.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\shared\AP.Shared.PluginSDK\AP.Shared.PluginSDK.csproj" />
    <ProjectReference Include="..\..\..\shared\AP.Shared.UI\AP.Shared.UI.csproj" />
    <ProjectReference Include="..\..\..\contracts\AP.Contracts.Core\AP.Contracts.Core.csproj" />
    <!-- 如需报表功能，添加以下引用 -->
    <!-- <ProjectReference Include="..\..\..\infra\AP.Infra.Report\AP.Infra.Report.csproj" /> -->
  </ItemGroup>

</Project>
```

### 3. 创建插件主类

创建 `YourFeaturePlugin.cs`。如需出现在侧边栏菜单，同时实现 `INavigationContributor`：

```csharp
using AP.Core.Enums;
using AP.Core.PluginFramework.Attributes;
using AP.Shared.PluginSDK.Base;
using AP.Shared.PluginSDK.Navigation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

namespace AP.Plugin.YourFeature;

[PluginMetadata(
    "AP.Plugin.YourFeature",           // 插件 ID（必须与目录名和 DLL 名一致）
    Name = "我的功能插件",              // 显示名称
    Version = "1.0.0",
    SupportedRoles = AppRole.Standalone | AppRole.Server,  // 支持的角色
    Priority = 100                      // 加载优先级（数值越小越先加载）
)]
public class YourFeaturePlugin : PluginBase, INavigationContributor
{
    public YourFeaturePlugin(ILogger<YourFeaturePlugin> logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        // 注册你的服务
        services.AddTransient<YourFeatureView>();
        services.AddTransient<YourFeatureViewModel>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        // 将视图挂载到主窗口的内容区域（可按权限条件门控）
        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        regionManager.RegisterViewWithRegion("ContentRegion", typeof(YourFeatureView));
    }

    // 声明式菜单：Sidebar 自动收集、排序、按权限过滤
    public IEnumerable<NavigationMenuItem> GetMenuItems()
    {
        yield return new NavigationMenuItem
        {
            Label = "我的功能",
            IconKind = "Puzzle",                 // Material Design 图标名
            NavigationTarget = "YourFeatureView",
            Order = 1500,                        // 参考现有菜单 Order 选择位置
            Permission = null                    // 需要权限时填权限码，如 "yourfeature.view"
        };
    }

    public override async Task StopAsync(CancellationToken ct = default)
    {
        Logger.LogInformation("我的功能插件正在停止...");
        await base.StopAsync(ct);
    }
}
```

> 菜单 Order 参考值：仪表板 100、系统配置 1000、配方管理 2000、报表中心 3000、用户管理 4000、角色管理 4100、审计日志 4200。插件之间留出间隔便于插入。

### 4. 创建 View 和 ViewModel

**YourFeatureView.xaml**（普通 UserControl，无需 `AutoWireViewModel`）:

```xml
<UserControl x:Class="AP.Plugin.YourFeature.YourFeatureView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d">
    <Grid Margin="20">
        <StackPanel>
            <TextBlock Text="我的功能插件" Style="{StaticResource TextStyle.Headline}" />
            <Button Content="执行操作" Command="{Binding ExecuteCommand}" Margin="0,20,0,0" />
            <TextBlock Text="{Binding StatusText}" Margin="0,10,0,0" />
        </StackPanel>
    </Grid>
</UserControl>
```

**YourFeatureView.xaml.cs**（全仓库约定：构造函数注入 ViewModel）:

```csharp
using System.Windows.Controls;

namespace AP.Plugin.YourFeature;

public partial class YourFeatureView : UserControl
{
    public YourFeatureView(YourFeatureViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

**YourFeatureViewModel.cs**:

```csharp
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AP.Plugin.YourFeature;

public partial class YourFeatureViewModel : ViewModelBase
{
    private readonly ILogger<YourFeatureViewModel> _logger;

    [ObservableProperty]
    private string _statusText = "就绪";

    public YourFeatureViewModel(ILogger<YourFeatureViewModel> logger)
    {
        _logger = logger;
    }

    [RelayCommand]
    private void Execute()
    {
        _logger.LogInformation("执行操作...");
        StatusText = "操作已执行";
    }
}
```

### 5. 添加到解决方案

```bash
cd ../../..
dotnet sln add platform/plugins/business/AP.Plugin.YourFeature/AP.Plugin.YourFeature.csproj
```

构建并运行宿主，插件会被自动发现、加载并出现在侧边栏。

### 6. 可选：为插件添加配置页

如需在"系统配置"中提供配置页，实现 `ISettingsContributor`（参考 `AP.Plugin.DeviceConfiguration` 的 `ScannerSettingsContributor`）：

```csharp
public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    base.ConfigureServices(services, configuration);
    services.AddSingleton<ISettingsContributor, YourSettingsContributor>();
}
```

编辑器 ViewModel 实现 `ISettingsEditorViewModel`（`LoadFromConfiguration` / `Validate` / `GetConfigurationValue` / `RequiresRestart`），并在 `InitializeAsync` 中把 `编辑器VM → 编辑器View` 的 DataTemplate 注册到 `Application.Current.Resources`。保存时框架统一校验、备份 appsettings 并写回。

---

## 配置说明

### 配置文件位置

所有配置文件位于 `platform/hosts/AP.Host.Desktop/Configuration/`：

```
Configuration/
├── appsettings.json              # 基础配置（所有模式共用）
├── appsettings.Standalone.json   # 单机模式专属配置（当前唯一受支持）
├── appsettings.server.json       # 服务端模式专属配置 ❄ 冻结（注意：文件名小写）
└── appsettings.Client.json       # 客户端模式专属配置 ❄ 冻结
```

### 完整配置示例

```json
{
  // 应用基础配置
  "AppConfiguration": {
    "MachineId": "Station-01",
    "MachineName": "本机",
    "CompanyName": "自动化系统",
    "SoftwareName": "自动化监控系统",
    "LayoutMode": "Standard",
    "DefaultNavigationTarget": "DashboardView",
    "NavigationWhenSecurityDisabled": ["DashboardView", "SettingsShellView", "RecipeListView", "ReportListView"]
  },

  // 运行角色
  "AppRole": "Standalone",

  // 日志配置
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    },
    "RetainedFileCount": 90,    // 日志保留天数
    "MaxFileSizeMb": 50         // 单个日志文件最大 MB
  },

  // Serilog 配置
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  },

  // 数据库配置（注意是嵌套结构；当前仅 SQLite 受支持，PostgreSQL ❄ 冻结）
  "Database": {
    "Provider": "SQLite",
    "SQLite": {
      "ConnectionString": "Data Source=local_data.db;Version=3;"
    },
    "PostgreSQL": {
      "ConnectionString": "Host=localhost;Database=automation_db;Username=postgres;Password=your_password"
    }
  },

  // 安全模块
  "Security": {
    "Enabled": true,
    "Audit": { "Enabled": true }
  },

  // gRPC 配置 ❄ 冻结（服务端用 ServerPort；客户端用 ServerUrl/ClientName）
  "Grpc": {
    "ServerPort": 5000,
    "ServerUrl": "http://192.168.1.100:5000",
    "ClientName": "Station-01"
  },

  // 容错策略配置（扁平键）
  "Resilience": {
    "DatabaseRetryCount": 3,
    "PlcRetryCount": 5,
    "GrpcCircuitBreakerThreshold": 5,
    "CircuitBreakerDurationSeconds": 30
  },

  // 报表配置
  "Report": {
    "Storage": {
      "RootPath": "reports",
      "PathFormat": "{year}/{month}/{date}_{type}.xlsx"
    },
    "Archive": {
      "Enabled": true,
      "Time": "02:00"
    },
    "Retention": {
      "Enabled": true,
      "Days": 180,
      "DeleteFiles": true
    },
    "Cleanup": {
      "Enabled": true,
      "DryRun": false
    }
  },

  // PLC 配置（统一节，切换 DriverType 即可更换品牌）
  "Plc": {
    "DriverType": "Mitsubishi",
    "IpAddress": "192.168.1.10",
    "Port": 6000,
    "Timeout": 1000,
    "Model": "Qna_3E",
    "HeartbeatAddress": "D0.0"
  },

  // 插件配置
  "Plugins": {
    "Configuration": {
      "AP.Plugin.Scanner": {
        "PortName": "COM10",
        "BaudRate": 9600,
        "DataBits": 8,
        "Parity": "None",
        "StopBits": "One",
        "NewLine": "\r"
      }
    }
  }
}
```

---

## 报表框架使用

### 1. 注册报表数据提供者

报表框架由宿主统一注册（`AddReportFramework`），业务插件只需注册自己的 Provider：

```csharp
public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    base.ConfigureServices(services, configuration);

    // 注册本插件的报表数据提供者（IReportDataProvider 在契约层，宿主侧统一收集）
    services.AddSingleton<IReportDataProvider, YourReportProvider>();
}
```

### 2. 实现报表数据提供者

创建 `YourReportProvider.cs`（`IReportDataProvider` 定义在 `AP.Contracts.Report`）：

```csharp
using AP.Contracts.Report.Abstractions;
using AP.Contracts.Report.Models;

namespace AP.Plugin.YourFeature.Reports;

public class YourReportProvider : IReportDataProvider
{
    private readonly IYourDataRepository _repository;

    public YourReportProvider(IYourDataRepository repository)
    {
        _repository = repository;
    }

    // 报表类型标识（英文，用于文件命名）
    public string ReportType => "YourReport";

    // 报表显示名称
    public string ReportName => "我的报表";

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
            Headers = ["序号", "产品编号", "检测结果", "检测值", "检测时间", "操作员"],
            // 数据行
            Rows = records.Select((r, i) => new List<object>
            {
                i + 1, r.ProductCode, r.Result, r.Value, r.TestTime, r.Operator
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

### 3. 手动导出报表

在 ViewModel 中注入 `ReportService`：

```csharp
public partial class ReportViewModel : ViewModelBase
{
    private readonly ReportService _reportService;

    public ReportViewModel(ReportService reportService)
    {
        _reportService = reportService;
    }

    [RelayCommand]
    private async Task ExportTodayReport()
    {
        try
        {
            var path = await _reportService.GenerateReportAsync("YourReport", DateTime.Today);
            MessageBox.Show($"报表已导出到: {path}");

            // 打开文件所在目录
            Process.Start("explorer.exe", Path.GetDirectoryName(path)!);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RegenerateReport(DateTime date)
    {
        // 补档：重新生成指定日期的报表
        var path = await _reportService.RegenerateReportAsync("YourReport", date);
        MessageBox.Show($"报表已重新生成: {path}");
    }
}
```

> 报表中心插件（`AP.Plugin.ReportCenter`）已提供归档查询/生成/打开/导出的完整 UI，通常无需自己写导出界面。

### 4. 报表文件输出示例

```
reports/
└── 2026/
    └── 01/
        ├── 2026-01-10_YourReport.xlsx
        ├── 2026-01-11_YourReport.xlsx
        └── 2026-01-12_YourReport.xlsx
```

> 注意：`ReportScheduler` / `ReportCleanupService` 以 `IHostedService` 注册，宿主默认不会自动启动它们（详见 `AGENTS.md` 5.6）。接入定时归档前请先确认宿主的启动方式。

---

## PLC 通信使用

### 1. 配置 PLC 连接

在 `appsettings.json` 的统一 `Plc` 节中配置：

```json
{
  "Plc": {
    "DriverType": "Mitsubishi",
    "IpAddress": "192.168.1.10",
    "Port": 6000,
    "Timeout": 1000,
    "Model": "Qna_3E",
    "HeartbeatAddress": "D0.0"
  }
}
```

切换到西门子 PLC 时，只需修改配置（或在应用的 系统配置 → 硬件 → PLC 配置 中修改，保存后重启）：

```json
{
  "Plc": {
    "DriverType": "Siemens",
    "IpAddress": "192.168.1.10",
    "Port": 102,
    "Timeout": 1000,
    "Model": "S7_1200",
    "HeartbeatAddress": "DB1.0.0"
  }
}
```

### 2. 地址格式速查（西门子 / 三菱 / 欧姆龙）

点表 `tags.json` 与心跳地址里的协议地址写法因品牌而异。**实际项目中通常与 PLC 工程师事先约定地址分配表**（哪些地址存什么、读写方向），照表配点即可；需要自行规划地址时遵循以下编址规则，避免重叠。

**西门子 S7**（按字节编址，多字节大端）

- 区域：`I`（输入）/ `Q`（输出）/ `M`（位存储）/ `DBn`（数据块）
- 位地址：`DB1.0.0`（DB1 字节 0 第 0 位）；字节偏移：`DB1.0` = DBW0
- 多字节类型按跨度留间隔：Int16 相邻 +2（`DB1.0`、`DB1.2`），Int32/Float +4，Int64/Double +8
- 重叠实例：Int16 的 `DB1.0` 与 `DB1.1` 共享字节 1，写 `DB1.1` 会清掉 DBW0 的低字节使其读为 0（2026-08-06 实测踩坑）

**三菱 MC（Qna_3E，按字编址）**

- `D` 区为字（16 位）寄存器：`D0`、`D1` 相邻即独立，天然不重叠
- Int32/Float 占连续两字：`D0` 的 Int32 = D0+D1，下一点从 `D2` 起
- 位软元件：`M0`、`X10`、`Y20`（M 十进制编号；X/Y **八进制**——`X10` 之后是 `X20`）

**欧姆龙 FINS**（按字编址）

- `D0`、`D1` 数据区字（同三菱）；另有 `CIO`/`W`/`H` 等工作区；位地址：`D0.0`（字.位）
- 字节序默认 **CDAB**（与西门子/三菱均不同），跨品牌移植点表时 Float/Int32 值异常先查字节序
- 驱动差异（IoTClient 限制）：字符串读写未实现；批量写入退化为逐条写入

**一句话记忆**：西门子按字节算偏移（需自留间隔），三菱/欧姆龙按字算编号（相邻编号天然不重叠，多字类型往后留）。

### 3. 在 ViewModel 中使用 PLC 服务

> **推荐方式**：业务代码通过 `ITagService` 按逻辑点名读写（点表 `tags.json` 配置，见"点表配置"菜单），而不是直接注入 `IPlcService` 传协议地址——后者为存量内部通道，分层防线见 `docs/conventions/LAYERING.md`。

业务代码只依赖统一的 `IPlcService`（由 `ActivePlcService` 按 `DriverType` 转发到真实驱动）：

```csharp
public partial class PlcViewModel : ViewModelBase
{
    private readonly IPlcService _plcService;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private short _currentValue;

    public PlcViewModel(IPlcService plcService)
    {
        _plcService = plcService;
    }

    [RelayCommand]
    private async Task Connect()
    {
        await _plcService.ConnectAsync();
        IsConnected = await _plcService.IsConnectedAsync();
    }

    [RelayCommand]
    private async Task ReadData()
    {
        try
        {
            CurrentValue = await _plcService.ReadAsync<short>("D100");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"读取失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task WriteData()
    {
        try
        {
            await _plcService.WriteAsync("D100", (short)123);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"写入失败: {ex.Message}");
        }
    }
}
```

> 注意：各品牌地址格式不同（如三菱 `D100` / 西门子 `DB1.DBW0` / 欧姆龙 `D100`（位地址 `D100.0`）），业务插件中的地址应通过配置传入，不要硬编码。

### 4. 订阅 PLC 连接事件

```csharp
public class PlcConnectionHandler : INotificationHandler<DeviceConnectedEvent>
{
    private readonly ILogger<PlcConnectionHandler> _logger;

    public PlcConnectionHandler(ILogger<PlcConnectionHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(DeviceConnectedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("PLC 已连接: {DeviceName}", notification.DeviceName);
        return Task.CompletedTask;
    }
}
```

事件处理器所在的程序集会被 MediatR 自动扫描注册，无需手动注册 Handler。

---

## 常见问题

### Q: 插件没有被加载

**检查项**:
1. 插件 DLL 是否在 `plugins/AP.Plugin.YourFeature/` 目录下（构建后自动输出，检查项目名是否以 `AP.Plugin` 开头）
2. `[PluginMetadata]` 的 ID 是否与目录名一致
3. `SupportedRoles` 是否包含当前运行角色
4. 查看日志文件 `logs/log-*.txt` 中的错误信息

### Q: 菜单里没有我的页面

**检查项**:
1. 插件主类是否实现了 `INavigationContributor` 并返回正确的 `NavigationTarget`
2. `NavigationTarget` 是否与 `RegisterViewWithRegion("ContentRegion", ...)` 注册的视图名一致
3. 若设置了 `Permission`，当前用户是否拥有该权限（admin 默认拥有全部权限）
4. `Security:Enabled=false` 时，Target 是否在 `AppConfiguration:NavigationWhenSecurityDisabled` 白名单中

### Q: 报表没有生成

**检查项**:
1. 是否注册了 `IReportDataProvider` 实现（`services.AddSingleton<IReportDataProvider, T>()`）
2. 检查 `Report` 配置节中的 `Archive.Enabled` 是否为 `true`
3. 定时归档依赖 `ReportScheduler`（IHostedService），确认宿主已显式启动
4. 查看日志中是否有报表相关的错误

### Q: PLC 连接失败

**检查项**:
1. `Plc:DriverType` 是否与现场 PLC 品牌匹配，对应品牌的插件是否在加载
2. PLC IP 地址和端口是否正确（三菱默认 6000，西门子默认 102，欧姆龙默认 9600）
3. 网络是否可达（`ping 192.168.1.10`）
4. PLC 是否已开机并处于运行状态
5. 查看日志中的连接错误信息（看门狗会自动重连）

### Q: 数据库备份失败

**可能原因**:
1. 数据库文件被其他进程占用
2. 没有写入权限
3. 磁盘空间不足

**解决方案**:
- 备份失败不会阻断启动，仅记录警告日志
- 确保应用有写入 `local_data.db.bak` 的权限

### Q: 日志文件占用磁盘空间过大

**解决方案**:
1. 在 `appsettings.json` 中调整 `Logging:RetainedFileCount`（默认 90 天）
2. 调整 `Logging:MaxFileSizeMb`（默认 50MB）
3. 启动时会自动清理过期日志（`LogCleanupHelper`）

### Q: 应用崩溃没有任何提示

崩溃信息会写入 `logs/crash-yyyyMMdd.log`（全局异常处理器，不弹窗）。致命异常会 `Environment.Exit(1)`。

---

## 项目结构说明

```
AP-Scaffold/
├── platform/
│   ├── hosts/                         # 启动宿主
│   │   └── AP.Host.Desktop/           # WPF 桌面端启动项目
│   │       ├── Bootstrapping/         # 启动器（插件加载、gRPC 启动）
│   │       ├── Configuration/         # 配置文件
│   │       ├── ViewModels/            # 主窗口 ViewModel
│   │       └── Views/                 # 主窗口/Splash XAML
│   │
│   ├── core/                          # 核心框架
│   │   └── AP.Core/
│   │       ├── PluginFramework/       # 插件加载、隔离上下文
│   │       ├── Lifecycle/             # 插件生命周期管理
│   │       ├── StateMachine/          # 插件状态机
│   │       └── EventBus/              # MediatR 事件总线
│   │
│   ├── contracts/                     # 接口契约层
│   │   ├── AP.Contracts.Core/         # OperationResult、错误模型、应用事件
│   │   ├── AP.Contracts.Hardware/     # 硬件服务接口、设备事件
│   │   ├── AP.Contracts.Communication/ # gRPC proto 契约
│   │   ├── AP.Contracts.System/       # 系统服务接口
│   │   ├── AP.Contracts.Security/     # 安全/权限/审计日志契约
│   │   ├── AP.Contracts.Recipe/       # 配方管理契约
│   │   └── AP.Contracts.Report/       # 报表中心契约
│   │
│   ├── infra/                         # 基础设施层
│   │   ├── AP.Infra.Database/         # 数据访问（FreeSql）
│   │   ├── AP.Infra.Grpc/             # gRPC 服务端/客户端
│   │   ├── AP.Infra.Hardware/         # PLC 驱动注册表与统一激活服务
│   │   ├── AP.Infra.Logging/          # Serilog 日志
│   │   ├── AP.Infra.Report/           # 报表框架
│   │   ├── AP.Infra.Resilience/       # Polly 容错策略
│   │   ├── AP.Infra.Security/         # 安全/权限/审计日志
│   │   └── AP.Infra.Recipe/           # 配方管理
│   │
│   ├── plugins/                       # 插件集
│   │   ├── hardware/                  # 硬件驱动插件
│   │   │   ├── AP.Plugin.Plc.Mitsubishi/ # 三菱 PLC
│   │   │   ├── AP.Plugin.Plc.Siemens/    # 西门子 PLC
│   │   │   ├── AP.Plugin.Plc.Omron/      # 欧姆龙 PLC (FINS/TCP)
│   │   │   └── AP.Plugin.Scanner/        # 串口扫码枪
│   │   ├── business/                  # 业务功能插件
│   │   │   └── AP.Plugin.DeviceConfiguration/
│   │   └── system/                    # 系统功能插件
│   │       ├── AP.Plugin.Layout/      # 布局/Sidebar/仪表盘
│   │       ├── AP.Plugin.Login/       # 登录认证
│   │       ├── AP.Plugin.SystemSettings/ # 系统配置中心
│   │       ├── AP.Plugin.UserManagement/ # 用户管理
│   │       ├── AP.Plugin.RoleManagement/ # 角色管理/权限分配
│   │       ├── AP.Plugin.AuditLog/    # 审计日志查看
│   │       ├── AP.Plugin.RecipeManagement/ # 配方管理
│   │       └── AP.Plugin.ReportCenter/ # 报表中心
│   │
│   └── shared/                        # 共享库
│       ├── AP.Shared.PluginSDK/       # 插件 SDK（PluginBase/导航/设置贡献者）
│       ├── AP.Shared.UI/              # UI 控件库（浅色主题/对话框/权限行为）
│       └── AP.Shared.Utilities/       # 通用工具
│
├── installer/                         # Inno Setup 安装包脚本
├── docs/                              # 文档
├── Directory.Build.props              # 全局编译属性（含插件输出规则）
├── Directory.Packages.props           # 中央包版本管理
└── AP-Automation.Platform.slnx        # 解决方案文件
```

---

## 技术支持

如有问题，请提交 Issue 到 [GitHub](https://github.com/lhfzys/AP-Scaffold/issues)

---

**最后更新**: 2026-07-21
