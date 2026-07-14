# AP-Scaffold 项目指南（AI 编码代理版）

> 本文件面向 AI 编码代理，汇总项目架构、构建方式、开发约定与测试策略。阅读前默认对项目一无所知。
> 项目主要文档语言为中文，代码注释亦以中文为主，因此本文件使用中文编写。

---

## 1. 项目概览

**AP-Scaffold** 是一个基于 .NET 的工业自动化通用平台脚手架，目标是为上位机、MES 客户端、产线监控系统等工业软件提供可插拔的桌面应用底座。

它不是可直接运行的业务系统，而是一套框架级基础设施：

- 插件化架构：业务模块以独立 DLL 形式存在，运行时动态加载。
- 角色化运行：支持 `Standalone`（单机）、`Server`（服务端）、`Client`（客户端）三种运行模式。
- 工业通信：内置三菱 PLC（MC 协议）、串口扫码枪等硬件驱动。
- 事件驱动：进程内通过 MediatR 解耦插件通信；跨进程通过 gRPC 流式分发数据。
- 工业级容错：基于 Polly 的重试、断路器、超时策略。
- 报表能力：基于 MiniExcel 的日报生成、归档、补档与自动清理。
- UI 底座：WPF + Prism + Material Design 的 MVVM 框架与控件库。

---

## 2. 技术栈与版本（以实际文件为准）

| 类别 | 技术 | 版本 / 说明 |
|------|------|-------------|
| 目标框架 | .NET 8.0 | 类库使用 `net8.0`，WPF 项目使用 `net8.0-windows` |
| SDK 约束 | .NET SDK | `global.json` 指定 `10.0.102`，`rollForward: latestMinor` |
| UI 框架 | WPF + Prism | `UseWPF=true`，Prism 9.0.537，DryIoc 6.2.0 |
| MVVM | CommunityToolkit.Mvvm | 8.4.0，配合 `ObservableProperty`/`RelayCommand` |
| 消息总线 | MediatR | 12.4.1，封装为 `IEventBus` |
| UI 主题 | MaterialDesignThemes | 5.3.0 |
| 图表 | LiveChartsCore.SkiaSharpView.WPF | 2.0.0-rc6.1 |
| 容错 | Polly / Polly.Core / Polly.Extensions | 8.6.5 |
| 日志 | Serilog + Sinks.File/Console + Enrichers | 4.3.0 系列 |
| ORM | FreeSql | 3.5.305，支持 SQLite / PostgreSQL |
| gRPC | Grpc.AspNetCore / Grpc.Net.Client / Grpc.Tools | 2.76.0 |
| PLC 通信 | IoTClient | 1.0.42，三菱 MC 协议 |
| 串口 | System.IO.Ports | 4.6.0 |
| Excel | MiniExcel | 1.42.0 |
| 测试 | xUnit + NSubstitute + FluentAssertions + coverlet | 见测试项目 `PackageReference` |

> 注意：所有 NuGet 包版本集中在 `Directory.Packages.props` 管理，各 `.csproj` 中只写 `PackageReference Include="..."` 而不写版本号。

---

## 3. 关键配置文件

| 文件 | 作用 |
|------|------|
| `global.json` | 指定 SDK 版本 `10.0.102`，`rollForward: latestMinor` |
| `Directory.Build.props` | 全局编译属性：目标框架 `net8.0`、Nullable/ImplicitUsings、文档 XML 生成、输出路径统一、插件项目特殊输出与瘦身清理 |
| `Directory.Packages.props` | 中央包版本管理（CPM） |
| `AP-Automation.Platform.slnx` | 新版 XML 解决方案文件，包含 27 个项目 |
| `platform/hosts/AP.Host.Desktop/Configuration/appsettings.json` | 基础配置，含 `AppRole` |
| `platform/hosts/AP.Host.Desktop/Configuration/appsettings.{Standalone|Server|Client}.json` | 角色专属配置 |

### 3.1 输出路径约定

`Directory.Build.props` 统一了输出目录：

- 非插件项目：`bin/$(Configuration)/`
- 插件项目（名称以 `AP.Plugin` 开头）：`bin/$(Configuration)/plugins/<PluginName>/`
- 插件项目的 `ProjectReference` 默认 `Private=false`，避免共享库重复复制。
- 构建后会执行 `CleanDuplicateLibs` Target，删除插件输出中已由宿主加载的共享库（如 `AP.Core.*`、`AP.Shared.*`、`Prism.*`、`MediatR.*` 等），减小插件体积。
- `Publish` 后会自动将 `bin/$(Configuration)/plugins/` 复制到发布目录。

---

## 4. 代码组织与项目结构

仓库根目录下主要结构：

```
AP-Scaffold/
├── platform/
│   ├── hosts/
│   │   └── AP.Host.Desktop/          # WPF 启动宿主（WinExe）
│   ├── core/
│   │   └── AP.Core/                  # 核心框架：插件加载、生命周期、状态机、事件总线、能力声明
│   ├── contracts/                    # 契约层：接口、事件、模型、.proto
│   │   ├── AP.Contracts.Core/
│   │   ├── AP.Contracts.Hardware/
│   │   ├── AP.Contracts.System/
│   │   ├── AP.Contracts.Communication/
│   │   ├── AP.Contracts.Security/    # 安全契约：用户、角色、权限、审计
│   │   └── AP.Contracts.Recipe/      # 配方契约：工艺参数、配方管理
│   ├── infra/                        # 基础设施层：横切关注点实现
│   │   ├── AP.Infra.Database/        # FreeSql + Repository 模式
│   │   ├── AP.Infra.Grpc/            # gRPC 服务端/客户端/拦截器
│   │   ├── AP.Infra.Logging/         # Serilog 配置与增强器
│   │   ├── AP.Infra.Report/          # 报表框架
│   │   ├── AP.Infra.Resilience/      # Polly 策略工厂
│   │   ├── AP.Infra.Security/        # 用户/角色/权限/审计（单机版）
│   │   └── AP.Infra.Recipe/          # 配方/工艺参数管理
│   ├── plugins/                      # 插件集合（6 个）
│   │   ├── business/
│   │   │   ├── AP.Plugin.AirtightnessCheck/
│   │   │   └── AP.Plugin.DeviceConfiguration/
│   │   ├── hardware/
│   │   │   ├── AP.Plugin.Plc.Mitsubishi/
│   │   │   └── AP.Plugin.Scanner/
│   │   └── system/
│   │       ├── AP.Plugin.Layout/
│   │       └── AP.Plugin.SystemSettings/
│   ├── shared/                       # 共享库
│   │   ├── AP.Shared.PluginSDK/      # PluginBase 基类与配置契约
│   │   ├── AP.Shared.UI/             # WPF 控件、主题、对话框、转换器
│   │   └── AP.Shared.Utilities/      # 通用工具与常量
│   └── tests/                        # 测试项目（3 个，14 个测试文件）
│       ├── AP.Core.Tests/
│       ├── AP.Infra.Tests/
│       └── AP.Shared.Tests/
├── docs/                             # 人工阅读文档
│   ├── ARCHITECTURE.md
│   ├── GETTING_STARTED.md
│   └── TESTING.md
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
└── AP-Automation.Platform.slnx
```

### 4.1 分层依赖规则

```
AP.Host.Desktop
    → Plugins
    → Contracts / Infra / Core / Shared

Plugins
    → AP.Shared.* (PluginSDK / UI / Utilities)
    → AP.Contracts.*
    → AP.Infra.*（按需，如报表、数据库）
    → 插件之间禁止互相引用

AP.Infra.*
    → AP.Contracts.*
    → AP.Core / AP.Shared.Utilities

AP.Contracts.*
    → AP.Core（仅 AP.Contracts.Core 直接引用 AP.Core）

AP.Core
    → AP.Shared.Utilities

AP.Shared.*
    → 不依赖平台其他项目（Utilities 可被所有层引用）
```

> 核心约束：**插件之间不允许直接引用**，只能通过 `AP.Contracts` 中定义的接口和事件通信。

---

## 5. 架构要点

### 5.1 启动宿主（AP.Host.Desktop）

`AP.Host.Desktop` 是唯一的可执行项目（`OutputType=WinExe`），启动流程：

1. `RoleResolver` 解析 `AppRole`：优先读取命令行 `--role=`，其次 `appsettings.json`，默认 `Standalone`。
2. `Bootstrapper` 加载配置、注册基础设施（日志、数据库、gRPC、容错、事件总线、报表框架）。
3. `PluginLoader` 扫描 `bin/$(Configuration)/plugins/*/` 目录，创建独立 `AssemblyLoadContext`，加载匹配 `SupportedRoles` 的插件。
4. `PluginLifecycleManager` 按 `Priority` 升序执行 `InitializeAsync` / `StartAsync`。
5. 根据角色启动 gRPC：`Server` 启动 Kestrel；`Client` 启动 `GrpcClientWorker`。
6. 显示主窗口（`Standalone` / `Client`）。

退出时调用 `ShutdownAsync`，按优先级降序停止插件。

### 5.2 插件框架（AP.Core）

- `IPlugin` / `IConfigurablePlugin`：插件契约。
- `PluginBase`（在 `AP.Shared.PluginSDK`）：插件开发基类，提供日志和虚拟生命周期方法。
- `PluginMetadataAttribute`：声明插件 ID、名称、版本、支持角色、依赖、优先级、是否必需。
- `RequiresCapabilitiesAttribute`：声明所需能力（位标志）。
- `PluginLoader` + `PluginLoadContext`：隔离加载，支持同名不同版本 DLL 共存与理论上热卸载。
- `PluginStateMachine` + `StateTransitionValidator`：管理 13 种状态（`Unloaded` → `Discovered` → `Loading` → `Loaded` → `Initializing` → `Initialized` → `Starting` → `Running` → `Stopping` → `Stopped`，以及 `Failed` / `Degraded` / `Frozen` / `Deprecated`）。
- `PluginLifecycleManager`：编排注册、初始化、启动、停止。

### 5.3 事件总线

- 后端使用 MediatR：`INotification` 事件 + `INotificationHandler<T>` 处理器。
- `IEventBus` 提供更简化的发布/发送 API。
- `AP.Host.Desktop` 中 `MediatRToPrismBridge` 将硬件事件桥接到 Prism `EventAggregator`，供 UI 线程消费。

### 5.4 运行角色

`AppRole` 是位标志枚举：

| 角色 | 说明 | 加载范围 |
|------|------|----------|
| `Standalone` | 单机模式，PLC + UI 在同一进程 | 标记为 `Standalone` 的插件 |
| `Server` | 服务端，连接硬件并提供 gRPC 服务 | 标记为 `Server` 的插件 + Kestrel |
| `Client` | 客户端，通过 gRPC 连接服务端 | 标记为 `Client` 的插件 + gRPC Worker |

### 5.5 报表框架

- 业务插件实现 `IReportDataProvider` 提供 `ReportData`（表头、行、汇总）。
- `ReportService` 协调生成流程：`IReportDataProvider` → `ExcelExporter`（MiniExcel）→ `ReportStorage`。
- `ReportScheduler`：后台服务，每天指定时间（默认 02:00）归档前一天报表。
- `ReportCleanupService`：按保留天数自动清理过期报表，支持 `DryRun` 模式。
- 报表默认输出到 `reports/{year}/{month}/{date}_{type}.xlsx`。

### 5.6 gRPC 通信

- `.proto` 文件位于 `AP.Contracts.Communication/Grpc/`。
- `Server` 模式下启动 Kestrel + `AutomationGate` 服务；`StreamBroadcaster` 使用有界 Channel（`DropOldest` 背压）向客户端推送 PLC 数据。
- `Client` 模式下 `GrpcClientWorker` 保持订阅，收到远程数据后在本地重发 `PlcDataChangedEvent`。

### 5.7 安全模块（AP.Infra.Security）

面向单机外包场景提供本地账号体系：

- `IIdentityService`：登录、登出、修改密码、当前用户、权限/角色检查。
- `IUserRepository` / `IPasswordHasher`：用户仓储与 PBKDF2 + 盐值哈希。
- `IAuditService`：审计日志记录与查询。
- 默认初始化：首次启动自动创建 `Administrator` / `Operator` / `Technician` 三个角色，以及 `admin` / `admin123` 默认账号（首次登录需改密）。

### 5.8 配方管理（AP.Infra.Recipe）

- `IRecipeManager`：配方的增删改查、版本控制、默认配方、配方切换。
- `Recipe` 实体以 JSON 存储工艺参数列表，支持不同业务插件按需扩展。
- 首次启动自动创建 `DEFAULT` 默认配方。

### 5.9 启动与部署

- **启动画面**：`SplashWindow` 显示启动进度，Bootstrapper 在初始化安全/配方/插件/gRPC 各阶段更新状态。
- **全局异常保护**：`GlobalExceptionHandler` 捕获 UI 线程、后台 Task、AppDomain 未处理异常；仅写入崩溃日志（`logs/crash-yyyyMMdd.log`），不弹窗，避免打断自动化流程。
- **系统托盘**：`TrayIconManager` 支持最小化到托盘、托盘菜单（显示主窗口 / 重启 / 退出）。
- **安装包**：`installer/setup.iss` 为 Inno Setup 脚本，发布后可一键生成 Windows 安装程序。

---

## 6. 构建与运行命令

### 6.1 环境要求

- Windows 10/11（WPF 仅限 Windows）。
- .NET SDK：满足 `global.json` 约束（当前环境为 10.0.301）。
- 推荐 IDE：Visual Studio 2022（支持 `.slnx` 新格式）。

### 6.2 常用命令

```bash
# 还原依赖
dotnet restore AP-Automation.Platform.slnx

# 编译整个解决方案
dotnet build AP-Automation.Platform.slnx

# 发布（单文件等配置可在 csproj 或发布配置中调整）
dotnet publish platform/hosts/AP.Host.Desktop/AP.Host.Desktop.csproj -c Release

# 构建安装包（需先安装 Inno Setup 6）
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer/setup.iss

# 以指定角色运行
dotnet run --project platform/hosts/AP.Host.Desktop --role=Server
```

> 插件项目会自动输出到 `bin/$(Configuration)/plugins/<PluginName>/`。若直接在 VS 中调试，请确保 `AP.Host.Desktop` 为启动项目。

### 6.3 配置运行模式

编辑 `platform/hosts/AP.Host.Desktop/Configuration/appsettings.json`：

```json
{
  "AppRole": "Standalone"
}
```

也可通过命令行覆盖：`--role=Server`。

---

## 7. 测试策略

### 7.1 测试技术栈

- xUnit 2.9.2
- NSubstitute 5.3.0
- FluentAssertions 6.12.1
- coverlet.collector 6.0.2

### 7.2 测试项目

| 测试项目 | 被测项目 | 覆盖领域 |
|----------|----------|----------|
| `AP.Core.Tests` | `AP.Core` | 状态机、生命周期、事件总线、插件框架 Attribute/接口 |
| `AP.Shared.Tests` | `AP.Shared.*` | `PluginBase`、序列化、配置辅助 |
| `AP.Infra.Tests` | `AP.Infra.*` | 报表选项/实体、弹性策略选项 |

当前共有 14 个测试文件，详见 `platform/tests/` 各子目录。

### 7.3 运行测试

```bash
# 运行全部测试
dotnet test

# 运行解决方案级全部测试
dotnet test AP-Automation.Platform.slnx

# 运行单个测试项目
dotnet test platform/tests/AP.Core.Tests
dotnet test platform/tests/AP.Shared.Tests
dotnet test platform/tests/AP.Infra.Tests

# 按类名/方法名筛选
dotnet test platform/tests/AP.Core.Tests --filter "FullyQualifiedName~PluginLifecycleManagerTests"
dotnet test platform/tests/AP.Core.Tests --filter "FullyQualifiedName~RegisterPlugins_LoadedPlugin_IsAdded"

# 收集覆盖率（Cobertura 格式）
dotnet test platform/tests/AP.Core.Tests /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

### 7.4 测试编写规范

项目已有明确规范（详见 `docs/TESTING.md`）：

- 命名：`{MethodName}_{Scenario}_{ExpectedResult}`，例如 `RegisterPlugins_LoadedPlugin_IsAdded`。
- 结构：必须遵循 Arrange / Act / Assert 三段式，并用空行分隔。
- 断言：统一使用 FluentAssertions，避免 xUnit 原生 `Assert.*`。
- Mock：使用 NSubstitute，构造函数参数必须全部提供或 Mock。
- 异步测试：返回 `Task`/`ValueTask`，使用 `Task.FromException` 模拟失败。

---

## 8. 代码风格与开发约定

### 8.1 基础风格

- 语言：C# 12（`LangVersion=latest`），启用 `ImplicitUsings` 与 `Nullable`。
- 文档：所有项目默认生成 XML 文档文件；`1591` 警告被抑制（缺少 XML 注释不报错）。
- 命名：遵循 PascalCase 类/方法/属性、`_camelCase` 私有字段、`I` 前缀接口。
- 文件组织：按功能命名空间分层，文件夹结构与命名空间一致。

### 8.2 插件开发约定

1. 插件项目命名必须以 `AP.Plugin.` 开头，并放到 `platform/plugins/{business|hardware|system}/` 下。
2. 必须创建主类继承 `PluginBase`，并标注 `[PluginMetadata(...)]`。
3. `PluginMetadata` 的 ID 必须与目录名、DLL 名一致。
4. `SupportedRoles` 必须显式声明支持的角色。
5. `ConfigureServices` 中注册插件自身服务；`InitializeAsync` 中注册 Region/视图；`StartAsync` 中启动后台任务。
6. 插件间禁止互相引用，通过 MediatR 事件或 `AP.Contracts` 接口通信。
7. 需要报表能力时引用 `AP.Infra.Report`，实现 `IReportDataProvider` 并调用 `services.AddReportDataProvider<T>()`。
8. 需要使用用户/权限/审计能力时引用 `AP.Contracts.Security`，注入 `IIdentityService` / `IAuditService`。
9. 需要使用配方能力时引用 `AP.Contracts.Recipe`，注入 `IRecipeManager`。
10. 插件输出由 `Directory.Build.props` 自动路由到 `plugins/<PluginName>/`，无需手动写 `OutputPath`。

### 8.3 配置与选项模式

- 使用 `Microsoft.Extensions.Options` + `IConfiguration` 绑定。
- 选项类通常位于各 Infra/Plugin 项目的 `Configuration/` 目录。
- 示例：`MitsubishiPlcOptions`、`ReportOptions`、`ResilienceOptions`、`AirtightnessOptions`。

### 8.4 日志

- 使用 `Microsoft.Extensions.Logging.ILogger<T>` 注入。
- Serilog 配置在 `AP.Infra.Logging`，默认输出控制台 + 滚动文件，路径 `logs/log-YYYYMMDD.txt`。
- 启动时自动清理过期日志（默认保留 90 天，单文件 50MB）。

---

## 9. 安全与部署注意事项

### 9.1 当前状态

- 仓库中**未发现** CI/CD 流水线文件、Dockerfile、容器编排文件。
- 身份认证与授权已实现单机本地账号版（`AP.Infra.Security`），出厂默认 `admin/admin123`，首次登录需改密。
- 配方管理已实现（`AP.Infra.Recipe`）。
- 安装包脚本位于 `installer/setup.iss`，使用 Inno Setup 编译。
- OpenTelemetry 可观测性尚未实现。

### 9.2 安全建议

- **配置文件中的敏感信息**：`appsettings.json` 可能包含数据库密码、PLC IP、 gRPC 服务端地址。生产环境应使用环境变量、Azure Key Vault 或 Windows DPAPI 加密，避免将密码提交到版本控制。
- **插件隔离**：虽然使用 `AssemblyLoadContext`，但插件与宿主共享同一进程和同一 `IHost`。恶意插件仍可能通过 DI 容器获取服务，因此应只加载可信来源的插件。
- **gRPC 传输**：当前 proto 未显示 TLS 强制要求。跨网络部署时务必启用 HTTPS/TLS，并验证客户端证书。
- **串口 / PLC 访问**：硬件插件需要管理员或特定权限才能打开串口、连接网络设备。确保运行账户权限最小化。
- **日志脱敏**：避免在日志中记录 PLC 地址对应的敏感工艺参数或个人信息。

### 9.3 部署模式

| 模式 | 部署建议 |
|------|----------|
| `Standalone` | 工控一体机单点部署，插件、UI、硬件驱动在同一进程。 |
| `Server` | 部署在靠近 PLC 的设备上，暴露 gRPC 服务供客户端连接。 |
| `Client` | 部署在办公区或远程监控终端，通过 gRPC 订阅服务端数据。 |

发布时使用 `dotnet publish -c Release`，插件会自动复制到发布目录的 `plugins/` 下。

---

## 10. 给 AI 代理的实操提示

1. **修改前先确认影响范围**：由于插件隔离与 DI 注册分离，修改 `AP.Core` 或 `AP.Contracts` 会影响所有插件；修改 Infra 可能影响数据库/gRPC/报表。
2. **新增插件时**：复制现有插件（如 `AP.Plugin.DeviceConfiguration`）作为模板，确保 `csproj` 以 `AP.Plugin.` 开头以继承正确的构建属性。
3. **新增 NuGet 包时**：在 `Directory.Packages.props` 中统一管理版本，各项目只引用不指定版本。
4. **新增测试时**：保持与被测项目一致的目录结构，使用 xUnit + FluentAssertions + NSubstitute，命名遵循 `{Method}_{Scenario}_{Expected}`。
5. **修改配置模型时**：同步更新 `appsettings.json` 示例、对应 Options 类的绑定属性，以及测试项目中的 Options 测试。
6. **不要假设 README 完全准确**：README 与 CHANGELOG 中部分描述（如 .NET 版本、插件数量）与实际代码存在差异；以 `.csproj`、`global.json`、`Directory.Build.props` 为准。

---

## 11. 参考文档

- `README.md` — 项目概览、技术栈、快速开始。
- `docs/ARCHITECTURE.md` — 详细架构设计、数据流、扩展点。
- `docs/GETTING_STARTED.md` — 环境准备、插件开发、配置说明。
- `docs/TESTING.md` — 测试规范、命令、覆盖率目标。
- `CHANGELOG.md` — 版本变更日志。

---

**最后更新**：2026-07-14（已同步本次优化改动）
