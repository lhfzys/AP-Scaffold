# AP-Scaffold 使用指南

本文档介绍如何使用 AP-Scaffold 框架快速开发工业上位机应用。

---

## 目录

- [环境准备](#环境准备)
- [快速开始](#快速开始)
- [创建业务插件](#创建业务插件)
- [配置说明](#配置说明)
- [报表框架使用](#报表框架使用)
- [PLC 通信使用](#plc-通信使用)
- [常见问题](#常见问题)

---

## 环境准备

### 必需工具

- **Visual Studio 2022**（任意版本，推荐 Professional 或 Enterprise）
- **.NET 8 SDK**（[下载地址](https://dotnet.microsoft.com/download/dotnet/8.0)）
- **Windows 10/11**（WPF 应用仅支持 Windows）

### 验证安装

```bash
# 检查 .NET SDK 版本
dotnet --version
# 应输出 8.0.x
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
- `Standalone` - 单机模式（PLC + UI 在同一台机器）
- `Server` - 服务端模式（仅连接 PLC，提供 gRPC 服务）
- `Client` - 客户端模式（仅 UI，通过 gRPC 连接服务端）

### 4. 配置数据库

默认使用 SQLite，无需额外配置。如需使用 PostgreSQL：

```json
{
  "Database": {
    "Provider": "PostgreSQL",
    "PostgreSqlConnection": "Host=localhost;Port=5432;Database=ap_platform;Username=postgres;Password=your_password"
  }
}
```

### 5. 运行项目

将 `AP.Host.Desktop` 设为启动项目，按 F5 运行。

---

## 创建业务插件

### 1. 创建插件项目

在 `platform/plugins/business/` 目录下创建新的类库项目：

```bash
cd platform/plugins/business
dotnet new classlib -n AP.Plugin.YourFeature
cd AP.Plugin.YourFeature
```

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
    <ProjectReference Include="..\..\..\infra\AP.Infra.Report\AP.Infra.Report.csproj" />
  </ItemGroup>

</Project>
```

### 3. 创建插件主类

创建 `YourFeaturePlugin.cs`：

```csharp
using AP.Core.Enums;
using AP.Core.PluginFramework.Attributes;
using AP.Shared.PluginSDK.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows;
using Prism.Navigation.Regions;

namespace AP.Plugin.YourFeature;

[PluginMetadata(
    "AP.Plugin.YourFeature",           // 插件 ID（必须与目录名和 DLL 名一致）
    Name = "我的功能插件",              // 显示名称
    Version = "1.0.0",
    SupportedRoles = AppRole.Standalone | AppRole.Server,  // 支持的角色
    Priority = 100                      // 加载优先级（数值越小越先加载）
)]
public class YourFeaturePlugin : PluginBase
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
        
        // 注册 ViewModel 映射
        ViewModelLocationProvider.Register(typeof(YourFeatureView).ToString(), typeof(YourFeatureViewModel));
        
        // 将视图挂载到主窗口的内容区域
        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            regionManager.RegisterViewWithRegion("ContentRegion", typeof(YourFeatureView));
        });
    }

    public override async Task StartAsync(CancellationToken ct = default)
    {
        await base.StartAsync(ct);
        Logger.LogInformation("我的功能插件已启动");
    }

    public override async Task StopAsync(CancellationToken ct = default)
    {
        Logger.LogInformation("我的功能插件正在停止...");
        await base.StopAsync(ct);
    }
}
```

### 4. 创建 View 和 ViewModel

**YourFeatureView.xaml**:

```xml
<UserControl x:Class="AP.Plugin.YourFeature.YourFeatureView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d">
    <Grid Margin="20">
        <StackPanel>
            <TextBlock Text="我的功能插件" FontSize="24" FontWeight="Bold" />
            <Button Content="执行操作" Command="{Binding ExecuteCommand}" Margin="0,20,0,0" />
            <TextBlock Text="{Binding StatusText}" Margin="0,10,0,0" />
        </StackPanel>
    </Grid>
</UserControl>
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

### 5. 配置构建后事件

在 `.csproj` 中添加构建后事件，自动复制插件到输出目录：

```xml
<Target Name="CopyPluginToOutput" AfterTargets="Build">
  <PropertyGroup>
    <PluginOutputPath>$(SolutionDir)bin\$(Configuration)\plugins\$(TargetName)\</PluginOutputPath>
  </PropertyGroup>
  <MakeDir Directories="$(PluginOutputPath)" />
  <Copy SourceFiles="$(TargetPath)" DestinationFolder="$(PluginOutputPath)" SkipUnchangedFiles="true" />
  <Copy SourceFiles="$(TargetDir)$(TargetName).pdb" DestinationFolder="$(PluginOutputPath)" SkipUnchangedFiles="true" Condition="Exists('$(TargetDir)$(TargetName).pdb')" />
</Target>
```

### 6. 添加到解决方案

```bash
cd ../../..
dotnet sln add platform/plugins/business/AP.Plugin.YourFeature/AP.Plugin.YourFeature.csproj
```

---

## 配置说明

### 配置文件位置

所有配置文件位于 `platform/hosts/AP.Host.Desktop/Configuration/`：

```
Configuration/
├── appsettings.json              # 基础配置（所有模式共用）
├── appsettings.Standalone.json   # 单机模式专属配置
├── appsettings.Server.json       # 服务端模式专属配置
└── appsettings.Client.json       # 客户端模式专属配置
```

### 完整配置示例

```json
{
  // 应用基础配置
  "AppConfiguration": {
    "MachineId": "Station-01",
    "MachineName": "一号工位电脑"
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

  // 数据库配置
  "Database": {
    "Provider": "SQLite",       // 或 "PostgreSQL"
    "SqliteConnection": "Data Source=data.db;Version=3;",
    "PostgreSqlConnection": "Host=localhost;Database=ap_platform;Username=postgres;Password=your_password"
  },

  // gRPC 配置
  "Grpc": {
    "ServerPort": 5000,
    "ClientAddress": "https://192.168.1.100:5000"
  },

  // 容错策略配置
  "Resilience": {
    "Pipelines": {
      "PLC-Retry": {
        "MaxRetryAttempts": 5,
        "RetryDelaySeconds": 3
      }
    }
  },

  // 报表配置
  "Report": {
    "Storage": {
      "RootPath": "reports",
      "PathFormat": "{year}/{month}/{date}_{type}.xlsx"
    },
    "Archive": {
      "Enabled": true,
      "Time": "02:00"           // 每天凌晨 2 点归档
    },
    "Retention": {
      "Enabled": true,
      "Days": 180,              // 保留 180 天
      "DeleteFiles": true
    },
    "Cleanup": {
      "Enabled": true,
      "DryRun": false
    }
  },

  // PLC 配置（统一配置，切换 DriverType 即可更换品牌）
  "Plc": {
    "DriverType": "Mitsubishi",
    "IpAddress": "192.168.1.10",
    "Port": 6000,
    "Timeout": 3000,
    "Model": "Qna_3E",
    "HeartbeatAddress": "M0"
  },

  // 插件配置
  "Plugins": {
    "Configuration": {
      "AP.Plugin.Scanner": {
        "PortName": "COM3",
        "BaudRate": 9600
      }
    }
  }
}
```

---

## 报表框架使用

### 1. 在插件中注册报表框架

在插件的 `ConfigureServices` 方法中：

```csharp
public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    base.ConfigureServices(services, configuration);
    
    // 注册报表框架（如果宿主未注册）
    services.AddReportFramework(configuration);
    
    // 注册本插件的报表数据提供者
    services.AddReportDataProvider<YourReportProvider>();
}
```

### 2. 实现报表数据提供者

创建 `YourReportProvider.cs`：

```csharp
using AP.Infra.Report.Abstractions;
using AP.Infra.Report.Entities;

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
            Process.Start("explorer.exe", Path.GetDirectoryName(path));
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

### 4. 报表文件输出示例

```
reports/
└── 2026/
    └── 01/
        ├── 2026-01-10_YourReport.xlsx
        ├── 2026-01-11_YourReport.xlsx
        └── 2026-01-12_YourReport.xlsx
```

---

## PLC 通信使用

### 1. 配置 PLC 连接

在 `appsettings.json` 中配置：

```json
{
  "Plc": {
    "DriverType": "Mitsubishi",
    "IpAddress": "192.168.1.10",
    "Port": 6000,
    "Timeout": 3000,
    "Model": "Qna_3E",
    "HeartbeatAddress": "M0"
  }
}
```

切换到西门子 PLC 时，只需修改配置：

```json
{
  "Plc": {
    "DriverType": "Siemens",
    "IpAddress": "192.168.1.10",
    "Port": 102,
    "Timeout": 3000,
    "Model": "S7_1200",
    "HeartbeatAddress": "DB1.0.0"
  }
}
```

### 2. 在 ViewModel 中使用 PLC 服务

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

### 3. 订阅 PLC 连接事件

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

---

## 常见问题

### Q: 插件没有被加载

**检查项**:
1. 插件 DLL 是否在 `plugins/AP.Plugin.YourFeature/` 目录下
2. `[PluginMetadata]` 的 ID 是否与目录名一致
3. `SupportedRoles` 是否包含当前运行角色
4. 查看日志文件 `logs/log-*.txt` 中的错误信息

### Q: 报表没有生成

**检查项**:
1. 是否调用了 `services.AddReportFramework(configuration)`
2. 是否注册了 `IReportDataProvider` 实现
3. 检查 `Report` 配置节中的 `Archive.Enabled` 是否为 `true`
4. 查看日志中是否有报表相关的错误

### Q: PLC 连接失败

**检查项**:
1. PLC IP 地址和端口是否正确
2. 网络是否可达（`ping 192.168.1.10`）
3. PLC 是否已开机并处于运行状态
4. 查看日志中的连接错误信息

### Q: 数据库备份失败

**可能原因**:
1. 数据库文件被其他进程占用
2. 没有写入权限
3. 磁盘空间不足

**解决方案**:
- 备份失败不会阻断启动，仅记录警告日志
- 确保应用有写入 `data.db.bak` 的权限

### Q: 日志文件占用磁盘空间过大

**解决方案**:
1. 在 `appsettings.json` 中调整 `Logging:RetainedFileCount`（默认 90 天）
2. 调整 `Logging:MaxFileSizeMb`（默认 50MB）
3. 启动时会自动清理过期日志

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
│   │       └── Views/                 # 主窗口 XAML
│   │
│   ├── core/                          # 核心框架
│   │   └── AP.Core/
│   │       ├── PluginFramework/       # 插件加载、隔离上下文
│   │       ├── Lifecycle/             # 插件生命周期管理
│   │       ├── StateMachine/          # 插件状态机
│   │       └── EventBus/              # MediatR 事件总线
│   │
│   ├── contracts/                     # 接口契约层
│   │   ├── AP.Contracts.Core/         # 核心事件、错误模型
│   │   ├── AP.Contracts.Hardware/     # 硬件服务接口
│   │   ├── AP.Contracts.Communication/ # gRPC 契约
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
│   │   │   └── AP.Plugin.Scanner/        # 串口扫码枪
│   │   ├── business/                  # 业务功能插件
│   │   │   ├── AP.Plugin.AirtightnessCheck/
│   │   │   └── AP.Plugin.DeviceConfiguration/
│   │   └── system/                    # 系统功能插件
│   │       ├── AP.Plugin.Layout/      # 布局/Sidebar
│   │       ├── AP.Plugin.Login/       # 登录认证
│   │       ├── AP.Plugin.SystemSettings/ # 系统配置中心
│   │       ├── AP.Plugin.UserManagement/ # 用户管理
│   │       ├── AP.Plugin.RoleManagement/ # 角色管理/权限分配
│   │       ├── AP.Plugin.AuditLog/    # 审计日志查看
│   │       ├── AP.Plugin.RecipeManagement/ # 配方管理
│   │       └── AP.Plugin.ReportCenter/ # 报表中心
│   │
│   └── shared/                        # 共享库
│       ├── AP.Shared.PluginSDK/       # 插件开发 SDK
│       ├── AP.Shared.UI/              # UI 控件库
│       └── AP.Shared.Utilities/       # 通用工具
│
├── Directory.Build.props              # 全局编译属性
├── Directory.Packages.props           # 中央包版本管理
└── AP-Automation.Platform.slnx        # 解决方案文件
```

---

## 技术支持

如有问题，请提交 Issue 到 [GitHub](https://github.com/lhfzys/AP-Scaffold/issues)

---

**最后更新**: 2026-07-14