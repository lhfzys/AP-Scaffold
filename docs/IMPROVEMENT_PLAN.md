# AP-Scaffold 差距分析与改进计划

> 本文档围绕脚手架的五大目标诉求——**稳定、复用、安全、可持续、通用**——对当前代码做全面差距核查，并给出分阶段的改进计划。
> 核查基准：`main @ bac70ff`（2026-07-21）。每条发现附证据（文件:行号）；无法静态确认的运行时行为统一列入文末「未验证项清单」，不做猜测性结论。
> **范围决策（2026-07-21）**：当前阶段**仅聚焦 Standalone 单机模式 + SQLite 数据库**，Server/Client（gRPC）与 PostgreSQL/SQL Server 相关事项已冻结，见 [1.3 范围界定](#13-范围界定2026-07-21-战略决策) 与第八章末「冻结事项清单」。

---

## 目录

- [一、背景与评估方法](#一背景与评估方法)
- [二、总体结论](#二总体结论)
- [三、差距明细：安全](#三差距明细安全)
- [四、差距明细：稳定](#四差距明细稳定)
- [五、差距明细：复用](#五差距明细复用)
- [六、差距明细：可持续](#六差距明细可持续)
- [七、差距明细：通用](#七差距明细通用)
- [八、改进计划（三阶段路线图）](#八改进计划三阶段路线图)
- [九、附录：未验证项清单](#九附录未验证项清单)

---

## 一、背景与评估方法

### 1.1 五维度定义

| 维度 | 含义 | 对脚手架的具体要求 |
|------|------|-------------------|
| **稳定** | 现场长时间运行不出错、出错可自愈 | 硬件断线重连、插件故障隔离、启动/退出健壮、无静默失效 |
| **复用** | 新项目低成本接入、机制可迁移 | 插件机制成熟、契约分层正确、扩展点清晰、无硬编码耦合 |
| **安全** | 数据与操作可控、可追责 | 认证授权、权限强制、审计覆盖、密钥管理（单机场景不含传输加密） |
| **可持续** | 长期演进成本低、质量可守护 | 测试覆盖、CI/CD、代码质量基线、依赖治理、发布升级可靠 |
| **通用** | 业务无关、场景普适 | 无业务残留、可换肤、可本地化 |

### 1.2 评估方法

- 静态代码审查：按四个方向（安全/稳定/架构复用/可持续性）对全部 36 个项目逐一核查，证据精确到 文件:行号。
- 构建实测：`dotnet build AP-Automation.Platform.slnx -c Release`（0 错误，5 个唯一警告）；`dotnet test`（213 个测试全部通过）。
- 文档交叉核对：与刚重写的 `AGENTS.md` / `docs/*` 互证，已确认的既有结论（如 IHostedService 不启动）直接引用代码复核。

### 1.3 范围界定（2026-07-21 战略决策）

经项目所有者确认，当前阶段的建设范围收敛为：

- **仅 Standalone 单机模式**：Server/Client 分布式模式（gRPC 技术栈）**冻结**——相关代码保留但不维护、不验证、不投入改进；`AppRole.Server` / `AppRole.Client` 枚举保留但在文档中标注"未支持"。
- **仅 SQLite 数据库**：PostgreSQL（及未来 SQL Server）支持**冻结**——代码路径保留，但不验证、不投入；多数据库需求出现时再议。

对本文档的影响：

- 与冻结范围相关的发现（S1 gRPC 传输安全、G3 PostgreSQL 验证等）标记为 **❄ 冻结**，不进入三阶段计划，集中列于第八章末「冻结事项清单」。
- 其余发现与计划全部面向 **Standalone + SQLite** 场景重新排布。
- 解冻条件：出现多机部署或集中存储需求时，优先解冻对应清单项并重新评估优先级。

---

## 二、总体结论

| 维度 | 现状评级 | 核心短板（一句话） |
|------|---------|-------------------|
| 稳定 | ★★★☆☆ | 骨架健壮（故障隔离、看门狗、全局异常齐备），但存在 **4 个"静默失效/卡死"级缺陷**：报表定时任务实际不运行、Required 语义未执行、启动异常卡死 Splash、配置写失败报成功 |
| 复用 | ★★★☆☆ | 贡献者模式与驱动抽象设计良好，但 `IReportDataProvider` 放错层可能使插件报表机制整体失效；依赖声明与 ID 去重缺校验 |
| 安全 | ★★☆☆☆ | Standalone 范围下风险已收敛（gRPC 冻结），剩余短板：权限只在 UI 层、口令策略薄弱（无锁定/无复杂度）、PLC 写操作零审计、数据库与备份明文 |
| 可持续 | ★★☆☆☆ | 零 CI/CD、安全关键模块零测试、安装包升级会覆盖现场配置、数据库无迁移策略 |
| 通用 | ★★★☆☆ | 业务残留明显（气密插件/标题硬编码）但清理成本低；无 i18n |

**总体判断**：项目已具备优秀的架构骨架（插件化、贡献者模式、驱动抽象、统一主题），达成"快速复用"的潜力很高。范围收敛为 Standalone + SQLite 后，分布式与多数据库的重资产问题（gRPC 安全、PG 验证）整体移出，剩余最大的风险集中在**落地缺口**：若干宣称的机制实际未生效或半生效（定时归档、Required、Dependencies、韧性管道、能力声明），安全停留在 UI 层，质量保障体系（CI/测试/发布）尚未建立。建议按第八章三阶段路线推进：**先排雷（恢复宣称能力）→ 再加固（安全与稳定核心）→ 后演进（可持续与通用）**。

---

## 三、差距明细：安全

### S1 [高] gRPC 传输全明文、无认证 ❄ 冻结

- **证据**：
  - Kestrel 仅 HTTP/2 明文、监听全部网卡：`GrpcServerExtensions.cs:43`（`ListenAnyIP(port)`，无 `UseHttps`）
  - 客户端默认明文地址 `http://localhost:5000`：`GrpcClientWorker.cs:60`；`GrpcChannelFactory.cs:30-43` 无任何凭据配置
  - 服务端仅凭客户端自报 `ClientId` 即注册订阅：`GrpcGateService.cs:37-48`，`Topics={"All"}` 时接收全部 PLC 数据流
  - `EnableDetailedErrors = true` 常开（`GrpcServerExtensions.cs:26`），对外泄露内部异常细节
- **影响**：Server/Client 部署模式下，任何能连通端口的机器都可接收全部产线数据；无机密性、无完整性、无身份验证。
- **处置**：❄ **冻结**——Standalone 单机模式不启动 gRPC Server/Client，该风险在当前范围内不成立；冻结期间文档（README/AGENTS）应将 Server/Client 标注为"未支持"，防止误用。解冻后此项恢复为最高优先级。

### S2 [高] 登录无失败锁定，口令策略薄弱

- **证据**：
  - 登录流程无失败计数/锁定：`IdentityService.cs:22-40`；`User` 实体无 FailedAttempts/Lockout 字段（`Entities/User.cs:10-44`）
  - 改密仅校验非空+两次一致：`ChangePasswordViewModel.cs:55-71`（新密码可设为 `1`）
  - 重置密码固定为 `admin123` 并明文弹窗告知：`UserListViewModel.cs:171-191`
  - 强制改密可绕过性：Bootstrapper 强制改密流程（`Bootstrapper.cs:97-104`）本身有效，但因无复杂度策略，可改为弱口令
- **影响**：本地登录可无限暴力尝试；口令体系形同虚设。
- **严重度**：高（好的一面：PBKDF2-SHA256 / 10 万迭代 / 128-bit 盐 / 定时间比较，哈希本身合格——`PasswordHasher.cs:12-41`）

### S3 [高] 权限强制只在 UI 层，服务层零校验

- **证据**：全仓库 `HasPermission` 调用点全部位于 UI 层（视图注册门控、VM CanExecute、Sidebar 菜单、`PermissionBehavior`）；服务层无校验——`RecipeManager.cs:11-19` 仅依赖 `IFreeSql`，`UserRepository`、`ReportCenterService` 均不注入 `IIdentityService`。
- **影响**：任何拿到服务引用的代码（尤其是插件——`Bootstrapper.cs:343` 把根容器传给每个插件的 `InitializeAsync`）可完全绕过权限体系直接读写用户、配方、报表数据。`PermissionBehavior` 仅是控件隐藏/禁用，不是安全边界。
- **严重度**：高

### S4 [高] 审计覆盖面不足且接入方式脆弱

- **证据**：
  - 已有审计（ViewModel 层手工调用）：登录/登出/改密、用户与角色管理、配方管理、报表操作
  - **PLC 写操作零审计**：`ActivePlcService.cs:49-50` `WriteAsync` 直接透传——工业现场最敏感的操作无痕
  - 服务层审计缺失：`RecipeManager.SwitchAsync`（`RecipeManager.cs:96`）无审计无事件；`RecipeDbInitializer` 启动自动切换不留痕
  - 系统配置修改无审计：`SettingsService.cs:68-78` 仅写 ILogger
  - 审计靠"各处手工 try/catch 调用"，无拦截器/管道保证，新业务极易漏接
- **影响**：追责链不完整；事故溯源缺失最关键一环（谁写了 PLC）。
- **严重度**：高

### S5 [中] 明文密钥随仓库分发

- **证据**：`appsettings.server.json:5` PostgreSQL 连接串含 `Password=password`，随 Git 分发；配置体系无加密/环境变量/用户机密机制（`Bootstrapper.cs:136-139` 仅加载 JSON）。
- **处置**：严重度随范围决策下调（高→中）：Server 配置已冻结，PG 密码不再指向任何真实环境；仍建议阶段二将仓库中的示例连接串改为占位符（成本极低，避免形成"仓库可放真实密码"的坏先例）。Standalone 使用的 SQLite 无口令问题。

### S6 [中] 配置写回无原子与并发保护

- **证据**：`ConfigurationHelper.cs:28,48` `ReadAllText→WriteAllText` 非原子、无锁；备份由调用方先复制（`SettingsService.cs:102-110`），时间戳精确到秒，同秒两次保存互覆备份；文件不存在时静默 return（`ConfigurationHelper.cs:26`）。
- **严重度**：中（与「稳定」维度 T4 关联）

### S7 [中] 匿名身份是全权限单点开关

- **证据**：`AnonymousIdentityService.cs:12-35`——`Permissions=["*"]`、`HasPermission` 恒 true、`LoginAsync` 恒成功。`Security:Enabled=false` 时若审计开启，操作人全部记为 "anonymous"，追责失效。
- **严重度**：中

### S8 [中] 能力声明（RequiresCapabilities）无运行时强制

- **证据**：`RequiresCapabilitiesAttribute` 除两个插件标注与单元测试外无任何运行时读取者；插件通过根容器可解析 `IFreeSql`/`IUserRepository` 等任意服务。`AssemblyLoadContext` 隔离只解决依赖冲突，不是安全边界。
- **严重度**：中

### S9 [中] SQLite 数据库明文无访问控制

- **证据**：连接串无密钥（`appsettings.Standalone.json:19`）；`sys_users` 密码哈希、`sys_audit_logs` 任何本地用户可明文打开；`.bak`/`-wal`/`-shm` 备份同样明文；审计表无防篡改机制。
- **严重度**：中（工业单机常见接受度，但应在文档中明确假设）

---

## 四、差距明细：稳定

### T1 [高] 报表定时归档/清理完全失效

- **证据**：`ReportScheduler`、`ReportCleanupService` 仅以 `AddHostedService` 注册（`AP.Infra.Report/Extensions/ServiceCollectionExtensions.cs:40-41,74-75`）；宿主是 Prism 手动容器、无 `IHost`；`Bootstrapper.OnInitialized` 只手动启动了 `ReportDatabaseInitializer` 和 `GrpcClientWorker`（`Bootstrapper.cs:331-357`），全仓库无其他启动点。
- **影响**：`Report:Archive:Enabled=true` 配置不会产生任何定时归档与过期清理——宣称的"定时归档/定期清理"能力实际不存在。
- **严重度**：高（功能缺失级）

### T2 [高] 启动异常可致 Topmost Splash 永久卡死

- **证据**：`OnInitialized` 的 `Task.Run` 最外层 catch 只 `Log.Fatal`（`Bootstrapper.cs:378-381`）；异常发生在 `CloseSplashWindow()` 之前时，`Topmost=True` 的 Splash（`SplashWindow.xaml:12`）永久遮挡主窗口；`AppInitializedEvent` 不发布，主窗口初始化逻辑被跳过。
- **影响**：Security 禁用模式下，任何初始化异常都表现为"界面卡死"，现场只能强杀进程。
- **严重度**：高

### T3 [高] `Required=true` 语义未执行

- **证据**：特性注释声称"必需插件加载失败会导致应用退出"（`PluginMetadataAttribute.cs:41-44`），且**默认值即 true**；但全仓库（除测试外）无任何代码读取 `Required`——失败仅记日志并入 `_failedPlugins`（`Bootstrapper.cs:212`）。
- **影响**：登录、布局等关键插件失败时应用照常运行，语义与文档/预期完全相反。
- **严重度**：高

### T4 [高] 配置写回失败被静默吞掉且上报成功

- **证据**：`ConfigurationHelper.UpdateAppSetting` catch 所有异常仅 `Console.WriteLine`（`ConfigurationHelper.cs:50-54`）——WPF 无控制台，磁盘满/占用/权限失败完全不可见；`SettingsService.SaveSettings` 仍返回 `Success=true`（`SettingsService.cs:80-86`）。另：写入非原子（`File.WriteAllText` 直接覆盖），写一半断电即损坏 appsettings.json，启动 `AddJsonFile` 遇坏 JSON 直接抛异常，无自动从备份恢复；备份文件无清理策略、无限增长。
- **严重度**：高

### T5 [中] PLC 重连无熔断，看门狗退出后无监督重启

- **证据**：重连为固定节奏（2s 心跳 + 失败固定 5s 延迟，`MitsubishiPlcService.cs:212,234`、`SiemensPlcService.cs:171,187`）；看门狗循环最外层 catch 记录后 `_isWatchdogRunning=false` 直接退出（`MitsubishiPlcService.cs:284-292`），无监督者重启——一旦退出，自动重连永久失效，只能重启应用。
- **严重度**：中

### T6 [中] Scanner 串口断开完全不重连

- **证据**：仅订阅 `DataReceived`（`SerialPortScannerService.cs:54`），无 `ErrorReceived`、无断开检测；USB 拔插后 `IsOpen` 仍为 true，服务永久失能直到重启应用；初始化失败仅记日志无重试（`ScannerPlugin.cs:49-52`）。
- **严重度**：中

### T7 [中] 韧性管道声明后未接线

- **证据**：`Database-Retry` 注册后全仓库无任何 `GetPipeline(Keys.Database)` 调用（仅 `Keys.Plc` 被两个驱动使用）；另 `AddTransient<ResiliencePipeline>` 注册了一个 Empty 管道，直接注入 `ResiliencePipeline` 会拿到空管道（`ResilienceServiceExtensions.cs:30-35`），属于误导性注册。
- **严重度**：中（原 gRPC 部分——`Grpc-CircuitBreaker` 接线、客户端重连退避——随 Server/Client 模式 ❄ 冻结）

### T8 [中] 托盘重启无单实例保护

- **证据**：`RestartApplication` 先 `Process.Start` 再 `Shutdown()`（`TrayIconManager.cs:70-78`），全仓库无 Mutex/单实例检查。新旧进程并存窗口内：SQLite 备份 `File.Copy` 可能撞锁（降级为警告）；双击 exe 也会产生双实例。
- **严重度**：中（Standalone 不启动 gRPC，原"gRPC 端口 5000 被旧进程占用"的冲突在当前范围不成立；SQLite/双实例问题仍在）

### T9 [中] 数据库无降级与忙等待

- **证据**：未设 `busy_timeout`，WAL 下多写者竞争直接 `SQLITE_BUSY`（`DatabaseServiceExtensions.cs:65-70` 仅配置了 journal/synchronous 等 PRAGMA）；运行时库不可用无降级路径。
- **严重度**：中（原"PostgreSQL 缺连接串抛异常"项随 PG 冻结移出范围）

### T10 [低] 其他稳定性瑕疵

| 项 | 证据 | 说明 |
|----|------|------|
| Scanner Channel 无界 | `SerialPortScannerService.cs:67` | 设备故障狂发数据时内存无上限；建议有界 + DropOldest（StreamBroadcaster 已有先例） |
| UI 可恢复异常静默 | `GlobalExceptionHandler.cs:113-116` | `e.Handled=true` 后无任何用户可见反馈，现场排障只能翻日志 |
| 数据库备份非原子 | `DatabaseServiceExtensions.cs:113-121` | 三个文件分次拷贝，与旧进程退出重叠时 `.bak` 可能不一致 |
| 三菱 float 写入竞态 | `MitsubishiPlcService.cs:378` | 用字段 `_client` 而非方法内捕获的局部变量，重连瞬间存在旧实例写入窗口 |
| StopWatchdog 卡 UI | `MitsubishiPlcService.cs:304` | `Thread.Sleep(3s)` 在关停路径，UI 线程执行时卡界面约 3 秒 |

---

## 五、差距明细：复用

### R1 [高] `IReportDataProvider` 放错层，插件报表机制存在断点风险（✅ 2026-07-22 已解决）

- **证据**：接口定义在 `AP.Infra.Report/Abstractions/IReportDataProvider.cs:9`；`PluginLoadContext` 的共享程序集前缀不含 `AP.Infra.*`（`PluginLoadContext.cs:13-78`），`Directory.Build.props` 的插件瘦身删除清单也不含。
- **影响**：业务插件实现该接口须引用 `AP.Infra.Report`，插件会在隔离 ALC 里加载自己的程序集副本，导致接口类型标识与 Host 不一致，Host 侧 `IEnumerable<IReportDataProvider>` 可能永远收集不到插件实现。当前仅 Infra 内置 `SampleReportDataProvider`，问题未暴露——但**这正是"报表接入真实业务数据"待办的前置断点**。（机制推断，列入未验证项）
- **建议**：移至 `AP.Contracts.Report`（契约程序集由 Host 直接引用，默认上下文加载）。
- **严重度**：高
- **解决**：2026-07-22，`IReportDataProvider` 与 `ReportData` 已移至 `AP.Contracts.Report`（命名空间 `AP.Contracts.Report.Abstractions` / `AP.Contracts.Report.Models`）；`PluginLoadContext` 对 `AP.Contracts` 前缀强制共享，跨 ALC 类型标识一致；插件经 `services.AddSingleton<IReportDataProvider, T>()` 注册即可被 Host 收集。

### R2 [高] 插件依赖与 ID 均无校验

- **证据**：`PluginMetadataAttribute.Dependencies` 生产代码从未读取；`PluginLoader.DiscoverPlugins` 只按 `Priority` 排序（`PluginLoader.cs:52`），不做依赖解析/拓扑排序/缺失报错；无重复插件 ID 检测——两个目录放同 ID 插件会双双加载，`PluginLifecycleManager.RegisterPlugins` 中状态机字典静默覆盖前者（`PluginLifecycleManager.cs:41`）。`Metadata.Version` 仅用于日志，无版本兼容性校验。
- **严重度**：高（校验成本低，价值高）

### R3 [中] View/VM 全 Transient，页面生命周期脆弱

- **证据**：13 个插件清一色 `AddTransient`；`RequestNavigate` 每次解析新实例 → 页面状态丢失、重复查库；`DashboardViewModel` 的 `DispatcherTimer` 依赖 `Destroy()` 释放，靠 Prism 的 `IDestructible` 调用链兜底。插件还会被实例化两次（临时容器 + 最终容器，`Bootstrapper.cs:189,229`），构造函数副作用执行两次。
- **严重度**：中

### R4 [中] 硬编码耦合点

- **证据**：
  - **`AP.Shared.UI/Controls/LoadingSpinner/LoadingSpinner.xaml:12` 以 `StaticResource` 引用 `Brush.Overlay.Background`，全仓无该键定义**——静态资源查找失败会在使用处抛 XamlParseException（运行时崩溃点）
  - Region 名字面量残留三处：`LayoutPlugin.cs:55`、`AirtightnessPlugin.cs:20`（私有重复定义常量）、`MainWindow.xaml:32`（与 `StandardLayoutView.xaml:31` 的 `x:Static` 用法风格不一）
  - 导航目标 stringly-typed：`NavigationTarget = "UserListView"` 与 `appsettings.json` 的 `DefaultNavigationTarget` 靠字符串约定耦合
  - 4 处插件 XAML 硬编码色值（`DashboardView.xaml:229`、`SidebarView.xaml:137`、`SinglePageLayoutView.xaml:17`、`StandardLayoutView.xaml:25`）
- **严重度**：中（LoadingSpinner 键缺失为高）

### R5 [中] 依赖包许可与维护风险

| 包 | 现状 | 风险 |
|----|------|------|
| MediatR 12.4.1 | 12.x 仍免费 | 13+ 转商业双许可（2025-07 起），**升级即触许可**；钉版安全但需明确策略 |
| FluentAssertions 6.12.1 | 6.x 免费 | 8+ 转 Xceed 商业许可，测试升级同样受限 |
| IoTClient 1.0.42 | MIT | 维护近乎停滞；所幸 `IPlcDriverFactory` 抽象已兜底，替换成本可控 |
| LiveChartsCore 2.0.0-rc6.1 | 预发布版 | **全仓无任何 csproj 引用它**——纯死条目，删除即可 |
| .NET 8 目标框架 | — | **2026-11 停止支持**；SDK 已是 .NET 10（`global.json`），迁移窗口临近 |
| 其余（Polly/Grpc/FreeSql/Prism 等） | 未逐一比对最新版 | 列入未验证项（Grpc 系列随模式冻结，不再比对） |

- **严重度**：中

### R6 [中] 双容器的已知限制

- **证据**：`IRepository<>` 注册为 Scoped（`DatabaseServiceExtensions.cs:82`）但全仓无消费者（死注册）；DryIoc `Populate` 桥接后 Scoped 语义与 MS-DI 不同（未验证具体表现）；`IHostedService` 不自动跑（见 T1）。
- **严重度**：中

---

## 六、差距明细：可持续

### C1 [高] 测试盲区：安全关键模块零测试

- **证据**：测试仅 3 个项目 / 17 文件 / 213 用例，被测对象只有 AP.Core、AP.Shared、AP.Infra 的 Report/Resilience/Hardware 三个子集。**完全零测试**：`AP.Infra.Security`（密码哈希、权限判定、审计——安全关键）、`AP.Infra.Recipe`、`AP.Infra.Database`、`AP.Infra.Grpc`（随模式冻结，权重下调）、`AP.Infra.Logging`、全部 13 个插件、`AP.Host.Desktop`、`AP.Shared.UI`。
- **结构性问题**：选项/实体/特性/序列化类"数据形状"测试约占 51%（89/174 个标记）；UI/ViewModel 层零测试；`docs/TESTING.md` 宣称的覆盖率目标无任何自动化采集支撑（未验证声明）。
- **严重度**：高

### C2 [高] 零 CI/CD

- **证据**：无 `.github/` 目录、无任何 CI 配置；发布全手动（手动 `dotnet publish` + 手动 ISCC 编译）；`docs/TESTING.md` 中的 Actions 示例路径有误（slnx 在仓库根而非 platform/ 下）。
- **严重度**：高

### C3 [高] 安装包升级=全量覆盖，配置被重置

- **证据**：`setup.iss:37` `Flags: ignoreversion recursesubdirs`，publish 内含 `Configuration/appsettings*.json`（`AP.Host.Desktop.csproj:46-57` `CopyToOutputDirectory=Always`）→ **现场改过的配置（PLC 地址、AppRole、Security 开关）升级时被静默覆盖回默认值**。安装器只查注册表父键存在性、**不校验 .NET 主版本**（`setup.iss:54-55`）——只装 .NET 9/10 桌面运行时的机器误判通过，net8 应用默认不跨 major roll-forward。无 `AppMutex`、无版本比较，应用运行中安装、旧版盖新版均无拦截。
- **严重度**：高

### C4 [高] 数据库无版本化迁移策略

- **证据**：schema 演进依赖各 Initializer 手动 `CodeFirst.SyncStructure`（`UseAutoSyncStructure(false)`）——只有增量加表/加列，无版本化迁移脚本、无数据迁移、无回滚；FreeSql 对改列/删列的行为未验证。唯一兜底是启动时 `.db.bak` 文件备份。
- **严重度**：高

### C5 [中] 代码质量基线缺失

- **证据**：5 个唯一构建警告（全为 Nullable 类：`ConfigurationHelper.cs:39,44` CS8602、`StepItem.cs:13` CS8618、`MainWindowViewModel.cs:50` CS8622、`Bootstrapper.cs:266` CS8604）；无 `.editorconfig`、无分析器、无 `TreatWarningsAsErrors`；隐性债务无 TODO 标记（如 `RecipeManager.cs:102` 的无标记注释），无法被工具扫描追踪；日志消息内嵌 emoji 不利于检索。
- **严重度**：中

### C6 [中] 版本号双事实源与发布治理

- **证据**：`Directory.Build.props:5` 与 `setup.iss:8` 各自硬编码 `1.0.0`，升版必漂移；CHANGELOG 维护良好但 7/13 后所有功能全堆在 `[Unreleased]`；无 git tag、无 release 分支；无 Dependabot/Renovate。
- **严重度**：中

### C7 [中] 可观测性空白

- **证据**：无 OpenTelemetry/Meter/ActivitySource/HealthChecks（grep 零匹配）；`ISystemMonitorService` 只有接口没有任何实现；无运行时动态调日志级别机制。好的一面：关键路径（插件加载失败、PLC 重连链路）日志记录达标。
- **严重度**：中（工业现场远程诊断刚需）

---

## 七、差距明细：通用

### G1 [高] 业务残留损害"通用脚手架"定位

- **证据**：
  - 整个 `AP.Plugin.AirtightnessCheck` 气密业务插件随脚手架发布
  - 窗口标题/软件名硬编码：`MainWindowViewModel.cs:23-24`（"气密检测监控系统"）、`appsettings.Standalone.json:4`（`"softwareName":"气密检测监控系统"`）、`appsettings.json:4`（"一号工位电脑"）
  - `DashboardViewModel.cs:76-89` 硬编码占位统计数据；`IReportDataProvider.cs:17` 注释示例"气密性检测日报"
- **影响**：每次复用都要人肉排雷，直接损害"通用"定位。残留集中、清理成本低。
- **严重度**：高

### G2 [中] 无国际化机制

- **证据**：无 resx、无本地化抽象；19 个插件 XAML 共 121 处硬编码中文 `Text/Content/Header`；ViewModel 内同样硬编码（问候语、菜单 Label）。
- **严重度**：中（内销场景可接受；预留抽象成本低，后补成本高）

### G3 [中] PostgreSQL 路径从未验证 ❄ 冻结

- **证据**：PG 代码路径存在且 `appsettings.server.json` 默认即 PG；但测试工程无任何 Database 测试，git 历史无相关验证记录；PG 无备份/体检配套（SQLite 有启动备份）。
- **处置**：❄ **冻结**——数据库范围已收敛为 SQLite；PG/SQL Server 支持在出现集中存储需求时再启用并验证。

### G4 [低] 主题单一

- **证据**：仅一套浅色主题（`Industrial.Teal.MD3.xaml`），无换肤机制；品牌色调整需改主题文件。
- **严重度**：低

---

## 八、改进计划（三阶段路线图）

> 原则：**先排雷（让宣称的能力真实生效）→ 再加固（安全与稳定核心）→ 后演进（可持续与通用）**。
> 范围：以下所有事项均面向 **Standalone + SQLite**；冻结事项见本节末清单。
> 工作量按 1 名熟悉本项目的工程师估算，仅供排期参考。

### 阶段一：排雷（P0，约 1–2 周）——修复"实际失效/崩溃点"

| # | 事项 | 关联 | 工作量 | 验收标准 |
|---|------|------|--------|---------|
| 1 | Bootstrapper 显式启动 `ReportScheduler`/`ReportCleanupService` | T1 | 0.5d | 配置 `Archive:Enabled=true` 后定时归档实际产生；清理按 Retention 执行 |
| 2 | 主题补齐 `Brush.Overlay.Background` 键 | R4 | 0.5d | LoadingSpinner 使用处不再抛 XamlParseException |
| 3 | `OnInitialized` catch 兜底关闭 Splash + 错误提示 | T2 | 0.5d | 人为制造初始化异常，主界面可见且报错，无卡死 |
| 4 | `ConfigurationHelper` 写失败改日志 + 返回失败，写回原子化（临时文件+替换） | T4/S6 | 1d | 模拟写入失败时 SettingsService 返回失败并提示；无半写状态文件 |
| 5 | setup.iss：配置文件 `onlyifdoesntexist` + .NET 主版本检测修正 + AppMutex | C3 | 1d | 覆盖安装后现场配置保留；仅装 .NET 9/10 的机器被正确拦截 |
| 6 | `Required`/`Dependencies`/重复 ID 语义落地 | T3/R2 | 2d | Required 插件失败时启动中止并明确提示；缺失依赖拒绝加载并说明；重复 ID 启动报错 |
| 7 | 清理业务残留：标题走 `AppConfiguration`；AirtightnessCheck 标注示例或移出主仓；Dashboard 占位数据标注 | G1 | 1d | 全新克隆构建后无任何"气密"字样默认出现 |
| 8 | 5 个构建警告清零 + 关键债务补 TODO 标记 | C5 | 0.5d | 构建 0 警告；`RecipeManager.cs:102` 等债务可被 grep TODO 追踪 |
| 9 | 文档同步：README/GETTING_STARTED/AGENTS 将 Server/Client 与 PostgreSQL 标注为"未支持（冻结）" | 范围决策 | 0.5d | 文档不再引导用户使用冻结能力 |

**阶段一出口标准**：构建 0 警告、213 测试通过、宣称的定时归档能力恢复、升级安装不丢配置、文档与冻结范围一致。

> **✅ 阶段一已完成（2026-07-22）**：#1–#9 全部落地（提交 `5c98faf`/`b8a95c2`/`16704af`/`b1a4ab9`/`3d8c86c`/`1b68462` + 文档同步）。出口实测：全量 Rebuild 0 警告 0 错误、222 个测试全部通过、报表后台任务显式启动恢复、安装包 `onlyifdoesntexist` 保留现场配置、README/GETTING_STARTED/AGENTS/ARCHITECTURE 均已标注冻结范围。
> 验收待现场确认两项：① setup.iss 需在装有 Inno Setup 6 的机器编译并实测覆盖安装（本机无 ISCC，未编译验证）；② 定时归档需在现场配置 `Archive:Enabled=true` 后观察实际产出。
> 新发现遗留问题（转入阶段二前处理）：运行日志出现 `未找到 PLC 驱动 'Mitsubishi'。已注册的驱动: 无`，`IPlcDriverFactory` 注册链路疑似断裂。

### 阶段二：加固（P1，约 1–2 周）——安全与稳定核心

> **范围修订（2026-07-22）**：部署形态为外包项目单机、无外网、登录使用率低。原 #1（登录安全加固）、#3（服务层权限校验）、#8（Security 测试 + CI）移入本节末「保留项」，其余保留并按"排雷优先"重排。

| # | 事项 | 关联 | 工作量 | 验收标准 |
|---|------|------|--------|---------|
| 1 | ✅ 韧性管道接线：DB 操作接 `Database-Retry`；移除误导性 Empty 注册（gRPC 部分 ❄ 冻结）（2026-07-22 完成） | T7 | 1d | 管道调用点存在且有效 |
| 2 | ✅ `IReportDataProvider` 移至 `AP.Contracts.Report`（2026-07-22 完成；共享前缀保证类型标识，端到端验证随首个真实 Provider） | R1 | 1d | 业务插件 Provider 生成的报表真实出现在报表中心 |
| 3 | PLC 写操作审计 + 配置修改审计 + 审计拦截器化（业务无感接入） | S4 | 3d | PLC WriteAsync 留痕（操作人/地址/值/结果）；配置保存留痕。操作人取值：经 `IIdentityService.CurrentUser`（Security 禁用时恒为 `anonymous`）；后台服务发起的写操作记为 `system` |
| 4 | PLC 看门狗监督重启 + Scanner 断线重连（ErrorReceived + 重开策略） | T5/T6 | 2–3d | 模拟看门狗异常退出后自动恢复；USB 拔插后扫码恢复 |
| 5 | 托盘重启加单实例 Mutex（复用阶段一已引入的命名互斥体） | T8 | 0.5d | 双击 exe/托盘重启不产生双进程 |
| 6 | ✅ 仓库示例连接串改为占位符（2026-07-22 完成，3d0dd97） | S5 | 0.5d | 仓库无明文密码 |

#### 保留项（暂缓，触发条件出现时再启动）

| 事项 | 暂缓原因 | 触发条件 |
|------|---------|---------|
| 登录失败锁定 + 密码复杂度策略 + 重置密码随机化并强制改密（原阶段二 #1，S2） | 外包单机无外网、登录使用率低，现有登录+权限已够 | 出现联网部署 / 等保合规要求 |
| 服务层权限校验入口（原阶段二 #3，S3） | UI 层权限在当前威胁模型（单机操作员）下可接受 | 开放第三方插件接入 / 出现越权操作诉求 |
| `AP.Infra.Security` 单元测试 + CI 最小流水线（原阶段二 #8，C1/C2） | 当前无 CI 环境 | 建立 CI 环境（GitHub Actions / Gitea Runner 等） |

### 阶段三：演进（P2，1–2 个季度）——可持续与通用

| # | 事项 | 关联 | 说明 |
|---|------|------|------|
| 1 | 迁移 .NET 10（net8 于 2026-11 停止支持） | R5 | SDK 已是 .NET 10，改目标框架 + 全量回归 |
| 2 | MediatR / FluentAssertions 升级策略：许可评估（13+/8+ 商业），或评估替代（自研事件总线 / 其他断言库） | R5 | 决策记录（ADR）入 docs |
| 3 | SQLite 数据库版本化迁移机制（版本表 + 迁移脚本 + 回滚） | C4 | 替代手工 SyncStructure 演进 |
| 4 | i18n 抽象预留（文案资源层），新文案一律走抽象 | G2 | 不一次性翻译，先立机制 |
| 5 | 可观测性：OpenTelemetry + `ISystemMonitorService` 落地 + 健康检查端点 | C7 | 现场远程诊断 |
| 6 | 质量基线：.editorconfig + 分析器 + `TreatWarningsAsErrors` + CI 覆盖率门禁；版本号单源化（CI 注入 iss） | C5/C6 | |
| 7 | 插件启用/禁用机制（配置开关 + 菜单/服务联动），替代"热卸载"承诺 | R6 | 热卸载涉及 View/Region/DI 注销，代价大，短期不做 |
| 8 | View 生命周期策略：长驻页面（Dashboard 等）改 Singleton 或导航缓存 | R3 | |
| 9 | 主题可配置化（品牌色/深色模式预留） | G4 | |

### 冻结事项清单（Standalone 范围外，解冻需重新评估）

| # | 冻结事项 | 关联 | 解冻条件 |
|---|---------|------|---------|
| 1 | Server/Client 分布式模式整体（gRPC Server/Client、`StreamBroadcaster`、`GrpcClientWorker`、Kestrel 内嵌）——代码保留，不维护、不验证、不投入 | S1 | 出现多机部署需求 |
| 2 | gRPC 传输加固（TLS、Token 认证、网卡绑定、`EnableDetailedErrors` 开关、客户端重连退避、`Grpc-CircuitBreaker` 接线） | S1/T7 | 随事项 1 解冻 |
| 3 | PostgreSQL / SQL Server 支持（`appsettings.server.json` 连接串、PG 备份体检、PG 路径集成验证、`Database:Provider=PostgreSQL` 缺连接串的启动异常处理） | G3/T9/S5 | 出现集中存储/数据规模需求 |
| 4 | `appsettings.server.json` / `appsettings.Client.json` 维护（冻结期间仅保留占位符，不更新） | 范围决策 | 随事项 1/3 解冻 |

---

## 九、附录：未验证项清单

以下条目为静态分析推断或无法本地确认的运行时行为，实施前建议先验证：

| # | 条目 | 来源 |
|---|------|------|
| 1 | ~~`IReportDataProvider` 跨 ALC 类型不一致（机制推断：插件加载各自 AP.Infra.Report 副本，Host 收集不到插件实现）~~（✅ 2026-07-22 随 R1 解决：接口移至契约层，`AP.Contracts` 前缀强制共享，类型标识一致） | R1 |
| 2 | ~~PostgreSQL 路径真实可用性~~（❄ 已冻结，解冻时再验证） | G3 |
| 3 | DryIoc `Populate` 桥接后 Scoped 服务的运行时行为 | R6 |
| 4 | SQLite 实际并发写压力（是否出现 SQLITE_BUSY） | T9 |
| 5 | FreeSql `SyncStructure` 对改列/删列的具体行为 | C4 |
| 6 | `PermissionBehavior` 在登录态变化后是否重评估（代码中无订阅逻辑，已加载视图可能不刷新） | S3 |
| 7 | Security 禁用模式下是否仍存在弹登录窗的路径（`AnonymousIdentityService.LoginAsync` 恒成功） | S7 |
| 8 | DryIoc Populate 后插件服务是否可覆盖平台安全服务 | S8 |
| 9 | 安装到 Program Files 时数据目录的实际 ACL | S9 |
| 10 | Polly/FreeSql/Prism 等包与最新版差距（Grpc 系列随模式冻结，不再比对） | R5 |

---

**最后更新**: 2026-07-21（按 Standalone + SQLite 范围决策修订）
