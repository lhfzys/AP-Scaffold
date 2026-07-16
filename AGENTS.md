# AP-Scaffold — AI 协作交接文档

> 本文档面向 Kimi / Cursor / Copilot 等 AI 编码助手。当你在一个新 Session 中接手本项目时，请先完整阅读本文件，再继续处理用户请求。

---

## 1. 项目速览

**AP-Scaffold** 是一个工业自动化上位机平台脚手架，采用 .NET 8 WPF + Prism + 插件化架构。

- **工作目录**: `D:\workspace\AP-Scaffold`
- **解决方案**: `AP-Automation.Platform.slnx`
- **启动项目**: `platform/hosts/AP.Host.Desktop/AP.Host.Desktop.csproj`
- **目标框架**: .NET 8（构建 SDK 为 .NET 10.0.102，见 `global.json`）
- **输出目录**: `bin/Release/`（插件输出到 `bin/Release/plugins/{PluginName}/`）

---

## 2. 当前上下文

### 2.1 分支与提交

- **当前分支**: `main`
- **最近提交主题**: 报表中心骨架、配方管理骨架、审计日志可视化、角色权限管理、报表缺失程序集修复
- **已修改未提交文件**（请在开始新任务前确认）:
  - `platform/hosts/AP.Host.Desktop/AP.Host.Desktop.csproj`
  - `platform/hosts/AP.Host.Desktop/Bootstrapping/Bootstrapper.cs`
  - `platform/infra/AP.Infra.Report/Extensions/ServiceCollectionExtensions.cs`
  - `platform/plugins/system/AP.Plugin.RoleManagement/Views/RoleListView.xaml`
  - `platform/plugins/system/AP.Plugin.UserManagement/Views/UserListView.xaml`

### 2.2 已完成功能

- 插件化核心框架（加载、生命周期、状态机、事件总线）
- 三菱 / 西门子 PLC 驱动与统一切换机制
- 串口扫码枪硬件驱动
- gRPC Server/Client 分布式通信
- 安全模块：本地用户/角色/权限、登录、改密、审计日志
- 用户管理、角色管理（按 `user.manage` / `role.manage` 权限条件加载）
- 审计日志可视化（按 `audit.view` 显示）
- 配方管理骨架（按 `recipe.view` / `recipe.edit` / `recipe.switch` 控制）
- 报表中心骨架（按 `report.view` 显示，已修复 `report_archives` 表缺失问题）

### 2.3 活跃问题 / 待办

- [ ] 报表中心接入真实业务数据提供者
- [ ] 配方管理完善校验与版本历史
- [ ] 审计日志更多业务事件接入
- [x] 西门子 PLC 协议支持
- [ ] 欧姆龙 PLC 协议支持
- [ ] OpenTelemetry 可观测性

---

## 3. 目录结构速查

```
AP-Scaffold/
├── platform/
│   ├── core/AP.Core                      # 插件框架、生命周期、状态机、事件总线
│   ├── contracts/                        # 接口与事件契约
│   │   ├── AP.Contracts.Core
│   │   ├── AP.Contracts.Hardware
│   │   ├── AP.Contracts.Communication
│   │   ├── AP.Contracts.System
│   │   ├── AP.Contracts.Security         # 安全/权限/审计日志契约
│   │   ├── AP.Contracts.Recipe           # 配方管理契约
│   │   └── AP.Contracts.Report           # 报表中心契约
│   ├── infra/                            # 基础设施实现
│   │   ├── AP.Infra.Database             # FreeSql (SQLite/PostgreSQL)
│   │   ├── AP.Infra.Grpc                 # gRPC Server/Client
│   │   ├── AP.Infra.Hardware             # PLC 驱动注册表与统一激活服务
│   │   ├── AP.Infra.Logging              # Serilog
│   │   ├── AP.Infra.Resilience           # Polly
│   │   ├── AP.Infra.Security             # 安全/权限实现
│   │   ├── AP.Infra.Recipe               # 配方管理实现
│   │   └── AP.Infra.Report               # 报表框架
│   ├── shared/                           # 共享库
│   │   ├── AP.Shared.PluginSDK           # PluginBase
│   │   ├── AP.Shared.UI                  # UI 控件、主题、权限行为
│   │   └── AP.Shared.Utilities           # 工具类
│   ├── plugins/                          # 插件
│   │   ├── hardware/
│   │   │   ├── AP.Plugin.Plc.Mitsubishi
│   │   │   ├── AP.Plugin.Plc.Siemens
│   │   │   └── AP.Plugin.Scanner
│   │   ├── business/
│   │   │   ├── AP.Plugin.AirtightnessCheck
│   │   │   └── AP.Plugin.DeviceConfiguration
│   │   └── system/
│   │       ├── AP.Plugin.Layout          # 布局/Sidebar
│   │       ├── AP.Plugin.Login           # 登录
│   │       ├── AP.Plugin.SystemSettings  # 系统设置
│   │       ├── AP.Plugin.UserManagement  # 用户管理
│   │       ├── AP.Plugin.RoleManagement  # 角色管理
│   │       ├── AP.Plugin.AuditLog        # 审计日志
│   │       ├── AP.Plugin.RecipeManagement# 配方管理
│   │       └── AP.Plugin.ReportCenter    # 报表中心
│   ├── hosts/AP.Host.Desktop             # WPF 启动宿主
│   └── tests/                            # xUnit 测试
│       ├── AP.Core.Tests
│       ├── AP.Shared.Tests
│       └── AP.Infra.Tests
├── docs/
│   ├── ARCHITECTURE.md                   # 架构设计文档
│   ├── GETTING_STARTED.md                # 使用指南
│   ├── TESTING.md                        # 测试指南
│   └── PROJECT_STATUS.md                 # 项目状态与工作计划
├── AGENTS.md                             # 本文件
├── README.md
├── CHANGELOG.md
├── Directory.Build.props
├── Directory.Packages.props
└── AP-Automation.Platform.slnx
```

---

## 4. 关键命令

```bash
# 构建整个解决方案
dotnet build AP-Automation.Platform.slnx -c Release

# 运行所有测试
dotnet test platform/tests/AP.Core.Tests/AP.Core.Tests.csproj -c Release --no-build -v quiet
dotnet test platform/tests/AP.Shared.Tests/AP.Shared.Tests.csproj -c Release --no-build -v quiet
dotnet test platform/tests/AP.Infra.Tests/AP.Infra.Tests.csproj -c Release --no-build -v quiet

# 运行桌面宿主（需要 Windows + 人工登录）
bin/Release/AP.Host.Desktop.exe
```

---

## 5. 核心约定与常见坑点

### 5.1 插件开发约定

- 插件主类继承 `AP.Shared.PluginSDK.Base.PluginBase`，标注 `[PluginMetadata]`。
- `ConfigureServices` 注册 View/ViewModel；`InitializeAsync` 中按权限条件注册 Region 视图。
- **View 的 DataContext 注入模式**: 当前项目统一采用**构造函数注入 ViewModel**（`public UserListView(UserListViewModel vm)`），不再使用 `prism:ViewModelLocator.AutoWireViewModel="True"`。
- 插件之间不允许互相引用，通信通过 `Contracts` 事件 + MediatR。

### 5.2 数据库建表

- `AddPlatformDatabase` 设置了 `.UseAutoSyncStructure(false)`，**不会自动建表**。
- 新增实体必须在对应模块的初始化器中调用 `_freeSql.CodeFirst.SyncStructure<TEntity>()`。
- 当前手动调用的初始化器：
  - `SecurityDbInitializer`（`ISecurityDbInitializer`）
  - `RecipeDbInitializer`（`IRecipeDbInitializer`）
  - `ReportDatabaseInitializer`（在 `Bootstrapper.OnInitialized` 中直接解析调用）

### 5.3 PLC 品牌切换

- 通过 `Plc:DriverType` 配置切换三菱/西门子/欧姆龙。
- 各 PLC 插件注册 `IPlcDriverFactory`，`AP.Infra.Hardware.ActivePlcService` 根据配置转发到真实驱动。
- 新增品牌只需实现 `IPlcDriverFactory` 并注册到 DI，业务代码无需修改。
- 西门子地址格式与三菱不同，业务插件中的地址应通过配置或参数传入，避免硬编码。

### 5.4 IHostedService 不自动启动

- `AP.Host.Desktop` 不启动 `IHost`，因此 `AddHostedService<T>()` 注册的服务**不会自动运行**。
- 若需要在启动时执行，必须在 `Bootstrapper.OnInitialized` 中显式解析并调用。

### 5.5 契约程序集必须被 Host 直接引用

- 插件隔离加载，但共享契约 DLL 必须出现在 Host 输出目录。
- 若 MediatR 扫描时报告 `ReflectionTypeLoadException: Could not load file or assembly 'AP.Contracts.Xxx'`，请在 `AP.Host.Desktop.csproj` 中直接引用该项目。
- 参考：已添加 `AP.Infra.Report` 和 `AP.Contracts.Report` 到 Host 引用。

### 5.6 权限字符串

当前已使用的权限字符串（新增功能时请保持统一）：

| 权限 | 用途 |
|------|------|
| `user.manage` | 用户管理 |
| `role.manage` | 角色管理 |
| `audit.view` | 审计日志查看 |
| `recipe.view` | 配方查看 |
| `recipe.edit` | 配方编辑 |
| `recipe.switch` | 配方切换 |
| `report.view` | 报表查看 |

---

## 6. 文档索引

| 文档 | 用途 |
|------|------|
| `README.md` | 项目概览、快速开始、技术栈 |
| `docs/ARCHITECTURE.md` | 分层架构、设计模式、数据流 |
| `docs/GETTING_STARTED.md` | 环境准备、配置、插件开发示例 |
| `docs/TESTING.md` | 测试规范与运行方式 |
| `docs/PROJECT_STATUS.md` | 当前框架内容、成熟度、工作计划 |
| `CHANGELOG.md` | 版本变更日志 |

---

## 7. 如何继续工作

1. 先 `dotnet build AP-Automation.Platform.slnx -c Release` 确认当前代码可编译。
2. 运行三个测试项目确认测试通过。
3. 查看 `docs/PROJECT_STATUS.md` 了解当前进度和待办。
4. 处理用户请求前，先通过 `git status` 确认当前工作区状态。
5. 涉及多文件修改时，优先使用最小改动，保持与现有代码风格一致。

---

**最后更新**: 2026-07-14
