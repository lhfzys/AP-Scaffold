# AP-Scaffold 架构演进文档

> **本文档是长期演进的主控文档**。目标：把当前"PLC 通信封装"形态的项目，持续演进为可维护 5~10 年的**工业设备管理框架**。
> 每次演进任务完成后，必须回到本文档更新对应任务的完成状态与日期，并同步更新 `AGENTS.md` / `docs/PROJECT_STATUS.md` 中受影响的内容。

---

## 0. 演进总则

### 0.1 范围声明

- **仅考虑 Standalone 单机模式**。Server/Client（gRPC 分布式）与 PostgreSQL/SQL Server 为封存能力：代码保留、不维护、不验证、不投入改进；未来需要时再解冻。
- 设计决策（如 Device 抽象）在模型上为多设备/分布式**预留空间但不实现**，避免过度设计。

### 0.2 目标形态（北极星）

业务层永远不依赖具体通信库（IoTClient / HslCommunication / S7NetPlus / 官方 SDK 均可替换）；业务代码不直接调用 PLC 地址；UI 不直接访问 PLC；所有通信库都是可替换插件。

```
UI 插件 → 应用服务（业务语义） → Tag 服务（逻辑点名，带质量戳/时间戳）
       → 点表映射（配置：点名 → 设备 + 地址） → 设备管理（Device/状态机/监督）
       → 协议驱动插件（地址解析 + 通信库，唯一允许碰通信库的地方）
```

### 0.3 任务执行铁律（每次改动必须遵守）

1. **一次只完成一个 Task**，不允许跨 Task 修改。
2. **不允许顺手优化其它代码**——发现其它问题记录到本文档待办，另行立项。
3. **不允许修改公开 API，除非必须**；新增接口/类不受此限。必须修改时在任务说明中写明理由与兼容策略。
4. 每次改动尽可能小，保证**项目可编译、已有测试全绿、已有业务行为不变**。
5. 每个 Task **单独提交一个 Git Commit**（提交信息标注任务编号，如 `refactor(device): T1.3 三菱驱动接入 ConnectionSupervisor`），可单独 `git revert` 回滚。
6. 提交前确认：全量构建 0 错误 + 三个测试项目全通过。
7. 提交后更新本文档任务状态（勾选 + 日期 + commit hash）。

---

## 1. 当前项目总体架构

分层（自上而下）：

```
┌────────────────────────────────────────────────────────┐
│ AP.Host.Desktop（启动宿主：Bootstrapper/Splash/托盘/崩溃处理）│
├────────────────────────────────────────────────────────┤
│ Plugins 插件层（12 个）                                  │
│  hardware: 三菱/西门子/欧姆龙 PLC、扫码枪                 │
│  business: 设备配置（设置贡献者）                          │
│  system:   布局/登录/系统设置/用户/角色/审计/配方/报表     │
├──────────────────────┬─────────────────────────────────┤
│ Contracts 契约层(7个) │ Infra 基础设施层(8个)             │
│  接口/事件/DTO/错误码  │  DB/gRPC❄/硬件/日志/韧性/安全/配方/报表│
├──────────────────────┴─────────────────────────────────┤
│ AP.Core 核心框架（插件加载/生命周期/14态状态机/事件总线）    │
├────────────────────────────────────────────────────────┤
│ Shared 共享层（PluginSDK / UI / Utilities）              │
└────────────────────────────────────────────────────────┘
```

- 运行时：.NET 8 + WPF + Prism 9 + DryIoc；消息：MediatR（桥接 Prism EventAggregator）；ORM：FreeSql(SQLite)；容错：Polly v8；日志：Serilog。
- 插件通过 `PluginLoadContext` 隔离加载，宿主 `Bootstrapper` 手动构建容器（IHostedService 不自动启动）。
- 测试：xUnit + NSubstitute + FluentAssertions，3 个测试项目 / 237 个测试。

## 2. 每个模块职责

### 2.1 核心与共享

| 模块 | 职责 |
|------|------|
| `AP.Core` | 插件框架：`IPlugin`/元数据特性、`PluginLoader`、`PluginLoadContext` 隔离、生命周期管理、插件状态机（14 态）、`PluginCapabilities` 声明（无运行时强制）、MediatR 事件总线封装、`AppRole` 枚举 |
| `AP.Shared.PluginSDK` | 插件开发 SDK：`PluginBase`、声明式导航（`INavigationContributor`/`NavigationMenuItemBuilder`）、设置贡献者（`ISettingsContributor`/`ISettingsEditorViewModel`） |
| `AP.Shared.UI` | UI 基础设施：`ViewModelBase`、`LoadingSpinner`、`ICustomDialogService`、转换器、`PermissionBehavior`、浅色 MD3 主题 |
| `AP.Shared.Utilities` | `ConfigurationHelper`（appsettings 原子写回）、`SerializationHelper`、常量 |

### 2.2 契约层（7 个）

| 模块 | 职责 |
|------|------|
| `AP.Contracts.Core` | `OperationResult<T>`、`ErrorCode`（仅 9 码）、`PlatformException`、`AppInitializedEvent` |
| `AP.Contracts.Hardware` | `IPlcService`/`IPlcBatchReadWrite`（**裸地址字符串 API**）、`IPlcDriverFactory`、`IScannerService`、`PlcOptions`、设备连接事件、`PlcValue` |
| `AP.Contracts.Communication` | gRPC proto 契约 ❄ 封存 |
| `AP.Contracts.System` | `ILoginService`、`ISettingsDialogService`、`ISystemMonitorService` |
| `AP.Contracts.Security` | 身份/用户/角色/权限/审计接口与模型 |
| `AP.Contracts.Recipe` | `IRecipeManager`、配方模型 |
| `AP.Contracts.Report` | `IReportCenterService`、`IReportDataProvider`、报表 DTO |

### 2.3 基础设施层（8 个）

| 模块 | 职责 |
|------|------|
| `AP.Infra.Database` | FreeSql 配置（SQLite、WAL、启动备份）、`IRepository<T>`/`FreeSqlRepository`（已接 Database-Retry 管道） |
| `AP.Infra.Hardware` | `PlcDriverRegistry`（按 DriverType 索引工厂）、`ActivePlcService`（单实例懒代理）、`AuditingPlcServiceDecorator`（写操作审计） |
| `AP.Infra.Resilience` | Polly 三条管道（Database-Retry / PLC-Retry / Grpc-CircuitBreaker）与配置 |
| `AP.Infra.Logging` | Serilog 配置、增强器、日志清理 |
| `AP.Infra.Security` | 用户/角色/权限/审计实现、PBKDF2、种子数据初始化 |
| `AP.Infra.Recipe` | 配方 CRUD/版本/默认配方（`SwitchAsync` 事件 TODO） |
| `AP.Infra.Report` | 报表归档/导出/调度/清理（数据提供者仅示例） |
| `AP.Infra.Grpc` | gRPC Server/Client/广播 ❄ 封存 |

### 2.4 插件层（12 个）

| 插件 | 职责 |
|------|------|
| `AP.Plugin.Plc.Mitsubishi/Siemens/Omron` | PLC 驱动：实现 `IPlcDriverFactory`，封装 IoTClient 各协议客户端；**各自复制同一套看门狗/重连代码**；连接事件发布 |
| `AP.Plugin.Scanner` | 串口扫码枪：`SerialPort` + Channel + 独立重连监控，产出 `ScanCompletedEvent` |
| `AP.Plugin.DeviceConfiguration` | 扫码枪设置页（`ISettingsContributor`） |
| `AP.Plugin.Layout` | 布局/Sidebar/仪表盘（Dashboard 数据为硬编码占位） |
| `AP.Plugin.Login` | 登录窗口/强制改密 |
| `AP.Plugin.SystemSettings` | 系统配置中心（收集设置贡献者、统一保存/备份/审计） |
| `AP.Plugin.UserManagement/RoleManagement/AuditLog` | 用户/角色/审计日志管理 UI |
| `AP.Plugin.RecipeManagement/ReportCenter` | 配方/报表 UI（骨架） |

### 2.5 宿主

`AP.Host.Desktop`：`Bootstrapper`（配置→日志→Infra 注册→插件发现→两阶段容器→DryIoc 桥接→登录→DB 初始化→插件启动）、`MainWindow`、`SplashWindow`、`TrayIconManager`、`GlobalExceptionHandler`、`MediatRToPrismBridge`。

## 3. 模块之间依赖关系

编译期引用（来自 csproj 实测）：

```
Hosts → Core / Shared.* / Contracts.{Hardware,Report,System} / 全部 Infra
Plugins → Contracts.*（按域）+ Shared.PluginSDK (+ Shared.UI)
        ※ 例外：3 个 PLC 插件直接引用 AP.Infra.Resilience 和 AP.Shared.UI
Contracts.Core → AP.Core            ※ 契约层反向依赖核心框架
Contracts.{Recipe,Report,Security} → AP.Shared.Utilities
Core → AP.Shared.Utilities
Infra.Database → Core + Infra.Resilience
Infra.Hardware → Contracts.{Hardware,Security} + Infra.Resilience
Infra.{Security,Recipe} → Infra.Database
Shared.UI → Contracts.{Core,Security}
Shared.PluginSDK → Core
Tests → 被测项目
```

运行时依赖方向（注入关系）：插件 → 契约接口 → Infra 实现；插件间零引用，经 MediatR 事件通信。

**观察**：
- 大方向健康（插件只依赖契约/共享，宿主兜底组装），但存在四处"层间渗漏"，详见第 4、6 节。
- `Contracts.Core → AP.Core` 是最值得警惕的一条：契约本应是依赖图的最底层，现在反过来依赖核心框架（为复用 `AppRole` 等枚举）。这会迫使所有引用契约的项目传递引用 Core。

---

## 4. 哪些地方耦合过高

| # | 位置 | 问题 |
|---|------|------|
| C1 | `IPlcService.ReadAsync<T>(string address)` | **地址语法与上层耦合**：`"D100"`/`"DB1.0.0"` 品牌相关语法作为一级 API，上层传地址即隐含绑定品牌。业务与 PLC 品牌的解耦目前只做到"库可换"，没做到"代码不用改" |
| C2 | 三个 PLC 驱动插件 | **看门狗/重连/连接逻辑逐行复制三份**（方法名、日志文案、参数全同），改一处要改三处，扫码枪还有第四套独立重连 |
| C3 | PLC 插件 → `AP.Infra.Resilience`、`AP.Shared.UI` | 硬件插件直接引用 Infra 实现项目和 UI 项目，越过契约层（Resilience 尚可用"取管道"辩解，UI 引用对硬件插件而言原则上是多余耦合） |
| C4 | `Contracts.Core → AP.Core`、`Contracts.* → Shared.Utilities` | 契约层不是依赖图最底层，契约的"纯接口/DTO"定位被稀释 |
| C5 | `PlcOptions` | 单 PLC 假设：一个配置节只描述一台设备，多设备场景无法表达；且 `HeartbeatAddress` 等协议细节混入公共契约 |
| C6 | `ActivePlcService` 单例代理 | 与 C5 一体两面：全局一台 PLC 的假设写死在 DI 结构里 |
| C7 | Dashboard ↔ 占位数据 | UI 层无数据获取通道可言（硬编码），反映"采集→订阅→UI"链路整体缺失，而非单个类的问题 |

## 5. 哪些地方违反 SOLID

| # | 原则 | 位置与问题 |
|---|------|-----------|
| S1 | 单一职责 | 三个 `XxxPlcService`（各 400+ 行）同时承担：连接管理、心跳看门狗、重连退避、监督循环、读写协议适配、批量退化策略、事件发布、日志。至少 4 种职责应拆出 |
| S2 | 开闭原则 | 新增 PLC 品牌目前要**复制**整套看门狗代码——扩展靠抄而非靠组合；批量语义差异（真批量/循环/退化）靠各驱动自行解释，无能力声明约束 |
| S3 | 里氏替换 | `IPlcBatchReadWrite`：三家实现语义不一致（三菱全按 short 循环读、西门子类型硬编码 Int16、欧姆龙写退化逐条），调用方无法以同一假设使用——名义可替换，语义不可替换 |
| S4 | 接口隔离 | `IPlcService` 把连接管理 + 读写揉在一个接口；`ConnectDeviceCommand` 只有三菱插件有 handler，契约层却面向所有品牌声明 |
| S5 | 依赖倒置 | 见第 6 节（SOLID 的 D 单列） |
| S6 | （横切） | `OperationResult`/`ErrorCode`/`PlatformException` 定义了但全仓库几乎不用，错误处理规范缺失等于没有抽象——`throw new Exception("读取失败...")` 裸字符串是典型坏味道 |

## 6. 哪些地方违反依赖倒置

| # | 位置 | 问题 |
|---|------|------|
| D1 | 业务/UI → `IPlcService`（地址字符串） | 最严重的倒置缺口：**业务语义层依赖了协议细节**（地址语法）。抽象没有挡住细节泄漏，等于上层依赖了下层 |
| D2 | PLC 插件 → IoTClient 具体类 | 驱动插件直接 `new MitsubishiClient(...)` 等。这在"驱动即适配器"定位下是可接受的（总要有地方碰具体库），**但必须是唯一允许的地方**——目前没有机制防止通信库类型泄出驱动边界（如异常类型、枚举类型出现在签名里） |
| D3 | PLC 插件 → `AP.Infra.Resilience` | 插件依赖 Infra 具体项目而非抽象。韧性管道应由宿主注入，驱动只面向 `ResiliencePipeline` 抽象 |
| D4 | `Contracts.Core → AP.Core` | 抽象（契约）依赖了实现（核心框架），方向颠倒 |
| D5 | `Infra.Database → Core`、`Infra.Hardware → Infra.Resilience` | Infra 之间的横向依赖。目前影响有限，但框架长期演化后会成为循环依赖的温床，需立规矩：Infra 只允许依赖 Contracts/Shared |

## 7. 哪些地方以后最难维护

按"5 年后改动的痛苦程度"排序：

1. **裸地址 API（C1/D1）**：业务代码每多写一处 `ReadAsync<short>("D100")`，将来换品牌/换库的成本就翻一倍。这是最会"利滚利"的债务。
2. **三份复制的看门狗（C2/S1）**：现在改重连逻辑要同步改 3 处且无法测试；第 4、5 个驱动接入时会彻底失控。
3. **`bool _currentConnectionState`**：无状态机的连接管理，加"重连中/故障/停用"任何一个状态都要重写分支逻辑，且并发下标志位无保护。
4. **批量读写语义分裂（S3）**：无人使用所以没有爆雷，一旦上层开始依赖就会以三种不同方式出错。
5. **Bootstrapper 巨型流程**：宿主启动编排已很长（配置→日志→Infra→插件→桥接→登录→DB→启动），每加一个基础设施都要动它，且 IHostedService 不自动启动的坑要靠纪律规避。
6. **错误处理无规范（S6）**：没有 Result/异常的统一约定，每个新服务都在随机发明返回风格，5 年后全仓库会是 N 种风格的博物馆。

## 8. 哪些地方最值得抽象

按"抽象一次、长期受益"排序：

1. **Device 抽象**（`IDevice`/`DeviceInfo`/设备类型/生命周期/状态）：把 PLC、扫码枪统一为"设备"，是设备管理框架的本体。
2. **Tag 系统**（`TagDefinition`/`TagValue{Value,Quality,Timestamp}`/点表/`ITagService`）：业务按逻辑点名读写，彻底切断 D1。点表配置化（JSON 起步，未来可入库）。
3. **连接监督组件**（`ConnectionSupervisor` + `DeviceConnectionState` 状态机）：一次实现，所有设备类型复用；心跳/退避/监督参数配置化。
4. **地址解析器**（每驱动内部 `IAddressParser`，internal 不外泄）：地址语法收敛在驱动边界内，校验提前、错误信息可读，为 Tag 映射打底。
5. **采集引擎**（周期轮询 + 变化检测 + 最新值表 + Tag 变化事件）：轮询/订阅/缓存/批量四个需求统一在这一个组件里落地，不单独抽象。
6. **错误处理规范 + 扩充 ErrorCode**：先立规范（哪层返回 Result、哪层抛异常、通信失败算哪种），再逐步落地；通信层失败是常态，应返回带质量戳的结果而非抛异常。

## 9. 哪些地方不用改

以下经过评估，**保持现状**（除非有新需求触发）：

- **插件框架本体**（加载/隔离/生命周期/14 态状态机）：完善且有 130 个测试覆盖。
- **声明式导航贡献者 + 设置贡献者模式**：扩展成本已足够低，是新功能接入的正轨。
- **配置写回机制**（原子写入 + 备份 + 审计 + RequiresRestart）：已达标。仅后续随 Device/Tag 扩展配置**模型**，机制不动。
- **Serilog 日志基础设施**：配置/滚动/清理/增强器齐全。缺的只是使用规范（文档即可解决）。
- **Polly 韧性管道**：三条管道定义合理，DB/PLC 已接线；仅把 Grpc 管道标记为封存即可，不删。
- **安全模块**（身份/权限/审计 + 可开关）：满足单机场景；审计装饰器模式是正确的扩展点，继续沿用。
- **SQLite 数据访问层**（FreeSql + Repository + 手动 SyncStructure 约定）：简单可控，符合单机定位。
- **UI 主题/控件体系**：浅色主题、`LoadingSpinner`、`PermissionBehavior` 等已收敛，与通信层演进正交。
- **报表/配方骨架**：与设备层演进正交，等 Tag 系统落地后再接真实数据源。
- **扫码枪的 Channel + 事件产出模式**：设计正确，仅重连部分未来并入 ConnectionSupervisor。

---

## 10. 改造优先级

> 原则：**先规范、再收敛、后抽象、最后迁移**。每一步都 additive（新增为主、旧路径保留），确保任何一步回滚都不影响业务。
> 验收基线（每个 Task 提交前必过）：`dotnet build AP-Automation.Platform.slnx -c Release` 0 错误 + 三个测试项目 237+ 个测试全绿。

### 阶段 0：规范先行（纯文档 + 文案，零风险）

| 编号 | 任务 | 改动范围 | 验收 |
|------|------|----------|------|
| T0.1 | 制定《错误处理与 Result 使用规范》：明确驱动层/设备层返回 Result（通信失败是常态）、业务服务层允许领域异常、UI 统一兜底；规划 ErrorCode 扩充清单 | 新增 `docs/conventions/ERROR_HANDLING.md` | 文档评审通过 |
| T0.2 | 制定《日志使用规范》：消息模板、级别约定、禁止 emoji、通信层必带字段（设备/地址/耗时） | 新增 `docs/conventions/LOGGING.md` | 文档评审通过 |
| T0.3 | 三菱驱动日志文案按 T0.2 规范清理（仅文案与级别，不动逻辑） | `AP.Plugin.Plc.Mitsubishi` | 编译+测试通过；行为不变 |
| T0.4 | 西门子驱动日志文案清理 | `AP.Plugin.Plc.Siemens` | 同上 |
| T0.5 | 欧姆龙驱动日志文案清理 | `AP.Plugin.Plc.Omron` | 同上 |
| T0.6 | 封存代码显式标注：gRPC/Server/Client 相关类加注释标记 ❄（不删不改行为） | `AP.Infra.Grpc`、`AP.Contracts.Communication`、Host 中角色分支 | 编译通过；行为不变 |

### 阶段 1：连接监督收敛（不改公开 API，外部行为不变）

| 编号 | 任务 | 改动范围 | 验收 |
|------|------|----------|------|
| T1.1 | 新增 `DeviceConnectionState` 枚举 + 连接状态机组件（新增文件，不接线；复用 AP.Core 状态机模式） | `AP.Infra.Hardware`（或新 Shared 项目，定稿时评审） | 单元测试覆盖状态迁移 |
| T1.2 | 新增 `ConnectionSupervisor`：心跳周期/重连退避/监督重启，参数注入可配（默认值 = 现有硬编码值 2s/5s/5s）；**不接线** | 同上 | 单元测试覆盖 |
| T1.3 | 三菱驱动接入 ConnectionSupervisor，删除其内部看门狗/重连/监督复制代码；`IPlcService` 签名、事件、配置键全部不变 | `AP.Plugin.Plc.Mitsubishi` | 编译+全测试；手工核对事件序列与旧版一致 |
| T1.4 | 西门子驱动接入 | `AP.Plugin.Plc.Siemens` | 同上 |
| T1.5 | 欧姆龙驱动接入 | `AP.Plugin.Plc.Omron` | 同上 |
| T1.6 | 扫码枪重连接入评估：差异大则保留并记录原因，不强行统一 | `AP.Plugin.Scanner` | 结论记录到本文档 |
| T1.7 | 看门狗/重连参数配置化（`Plc` 节新增可选键，缺省=现值；设置页补充） | `PlcOptions`、`PlcConfigurationContributor` | 旧配置文件不写新键行为不变 |

### 阶段 2：地址模型收敛到驱动内部

| 编号 | 任务 | 改动范围 | 验收 |
|------|------|----------|------|
| T2.1 | 三菱地址解析器（internal）：解析/校验 MC 地址，读写前预检，错误信息可读 | `AP.Plugin.Plc.Mitsubishi` 内部 | 解析器单测；非法地址报错信息改善 |
| T2.2 | 西门子地址解析器 | `AP.Plugin.Plc.Siemens` 内部 | 同上 |
| T2.3 | 欧姆龙地址解析器 | `AP.Plugin.Plc.Omron` 内部 | 同上 |

### 阶段 3：Device 抽象（新增层，旧 `IPlcService` 路径原样保留）

| 编号 | 任务 | 改动范围 | 验收 |
|------|------|----------|------|
| T3.1 | 契约层新增设备抽象：`IDevice`/`DeviceInfo`/`DeviceType`/设备状态（复用 T1.1 状态机）；**只新增不修改** | `AP.Contracts.Hardware`（新文件） | 编译；契约评审 |
| T3.2 | 设备注册表/管理器 `IDeviceRegistry`：单机单设备起步，模型预留多设备；宿主注册 | `AP.Infra.Hardware` | 单测 |
| T3.3 | PLC 以 Device 身份接入注册表（适配 ActivePlcService，薄封装） | `AP.Infra.Hardware` | 旧 API 路径行为不变 |
| T3.4 | Scanner 以 Device 身份接入（**含重连机制评估迁入 ConnectionSupervisor、emoji 日志清理**，依据 T1.6 结论） | `AP.Plugin.Scanner` | 同上 |
| T3.5 | 设备状态事件统一：新事件 + 旧事件桥接（旧事件继续发布，不删） | `AP.Contracts.Hardware`、`AP.Infra.Hardware` | 旧消费者不受影响 |

### 阶段 4：Tag 系统（框架核心交付）

| 编号 | 任务 | 改动范围 | 验收 |
|------|------|----------|------|
| T4.1 | Tag 模型契约：`TagDefinition`（点名/设备/地址/类型/方向/采集参数）、`TagValue{Value,Quality,Timestamp}`、`Quality` 枚举 | `AP.Contracts.Hardware`（新文件） | 契约评审 |
| T4.2 | 点表加载与校验：JSON 配置起步，启动时校验（引用设备存在/地址经驱动解析器验证） | `AP.Infra.Hardware` | 单测（含坏点表用例） |
| T4.3 | `ITagService` 按点名读写：点名→设备+地址映射→驱动；返回 `TagValue`；失败返回 Quality=Bad 而非抛异常（按 T0.1 规范） | `AP.Contracts.Hardware` + `AP.Infra.Hardware` | 单测 |
| T4.4 | 采集引擎：按点表周期轮询、同设备合并批量读（此时重新定位 `IPlcBatchReadWrite` 语义）、变化检测、最新值表 | `AP.Infra.Hardware` | 单测 |
| T4.5 | Tag 变化事件与订阅：变化才发（MediatR + Prism 桥接）；订阅者读最新值表 | 同上 | 单测 |
| T4.6 | Dashboard 改造为第一个真实消费方（在线设备数、Tag 实时值），替换硬编码占位 | `AP.Plugin.Layout` | 端到端人工验证 |

### 阶段 5：业务迁移与防线（此时才动存量调用方）

| 编号 | 任务 | 改动范围 | 验收 |
|------|------|----------|------|
| T5.1 | 分层防线文档：UI→应用服务→Tag 服务；明确"禁止 UI 注入 IPlcService/ITagService 以外的设备接口" | 文档 | 评审通过 |
| T5.2 | 存量业务调用点迁移到 ITagService（届时按实际调用点逐个列子任务） | 视存量而定 | 逐点迁移逐点验收 |
| T5.3 | `IPlcService` 直读路径标记 `[Obsolete]`（仅标记提示，不删除；新代码走 Tag） | `AP.Contracts.Hardware` | 编译 0 新警告（旧调用加 pragma 抑制并注明迁移计划） |
| T5.4 | `Contracts.Core → AP.Core` 依赖解除评估：将 `AppRole` 等枚举下移至 Contracts（**公开 API 变更**，届时专项评审兼容方案） | 两项目 | 专项评审后执行 |

### 阶段 6：收尾（低优先）

| 编号 | 任务 | 改动范围 | 验收 |
|------|------|----------|------|
| T6.1 | PLC 插件对 `AP.Shared.UI` 引用核实与清理（确认是否多余） | 3 个 PLC 插件 csproj | 编译通过 |
| T6.2 | Infra 横向依赖立规（仅文档约定 + 评审清单，不强改现有引用） | 文档 | 评审通过 |

### 任务状态记录

> 每完成一个 Task，在此追加一行：`| 编号 | 完成日期 | Commit Hash | 备注 |`

| 编号 | 完成日期 | Commit Hash | 备注 |
|------|----------|-------------|------|
| T0.1 | 2026-07-25 | `734f85b` | 新增 `docs/conventions/ERROR_HANDLING.md`；规范对全部新代码生效，存量差距已登记在第 7 节 |
| T0.2 | 2026-07-25 | `9f95fca` | 新增 `docs/conventions/LOGGING.md`；T0.3~T0.5 的执行依据，存量差距登记在第 8 节 |
| T0.3 | 2026-07-25 | `8446f18` | 三菱驱动 16 处日志清理（emoji/前缀/{Device} 结构化/重连尝试降 Debug）；构建 0 错误 + 237 测试全绿 |
| T0.4 | 2026-07-25 | `3476198` | 西门子驱动 16 处日志清理（同 T0.3 模式）；构建 0 错误 + 237 测试全绿 |
| T0.5 | 2026-07-25 | `ae546bb` | 欧姆龙驱动 16 处日志清理（同 T0.3 模式）；构建 0 错误 + 237 测试全绿 |
| T0.6 | 2026-07-25 | `b0f2f28` | 13 个文件加 ❄ 封存标注（AP.Infra.Grpc 8 个、proto 2 个、Server/ClientBootstrap、Bootstrapper 分支注释）；仅注释，构建 0 错误 + 237 测试全绿。**阶段 0 收官** |
| T1.1 | 2026-07-25 | `92a3e7f` | 按用户定位调整为 **Device Runtime Model 第一组件**：`DeviceConnectionState` 六态枚举（契约层 `DeviceRuntime/`）+ 协议无关状态机（Infra.Hardware/DeviceRuntime，锁内迁移、锁外发事件）+ 13 个单测（含相机场景协议无关性用例）；纯新增不接线，总计 250 测试全绿 |
| T1.2 | 2026-07-25 | `2641cd6` | `ConnectionSupervisor`（纯事件源无日志依赖，`ConnectionAttemptResult` 统一结果、`ConnectAttempted`/`LoopFaulted` 事件）+ `ConnectionSupervisorLogger`（日志作为消费者 Attach）+ 状态机事件更名 `Transitioned`(From/To/Reason/Timestamp)；修复 Disconnected 需经 Connecting 过渡的迁移缺陷；+10 单测，总计 260 全绿，不接线 |
| T1.3 | 2026-07-25 | `7d29c1e` | 三菱接入 Supervisor：删除复制看门狗（净 -100 行），状态唯一来源、ConnectAsync 仅 Start；新增 `TransitionEventBridge`（声明式 Transition→事件映射，不识 MediatR）；Supervisor 接管完整状态流（启动即首扫、Connecting 中断可恢复）；迁移表补 Connecting→Disconnected；事件改沿迁移边触发（有意差异已记录）；总计 267 测试全绿 |
| T1.4 | 2026-07-25 | `8837c4b` | 西门子接入 Supervisor（同 T1.3 模式，心跳 ReadBoolean / 默认地址 DB1.0.0）；服务类 -78 行净减；267 测试全绿 |
| T1.5 | 2026-07-25 | `703373c` | 欧姆龙接入 Supervisor（同 T1.3 模式，心跳 ReadBoolean / 默认地址 D0）；服务类 -78 行净减；267 测试全绿。**三份复制看门狗全部消除** |
| T1.6 | 2026-07-25 | 本行即记录 | **评估结论：保留现状不接入**。① `OpenAsync` 首开失败同步抛出的契约行为与 Supervisor 单一状态源模型冲突，接入需改公开行为或退回混合双状态源；② 探针语义差异大（端口枚举+错误标志 vs 心跳读）；③ 收益小（~60 行单设备）。**转 T3.4 随 Device 抽象统一改造**，届时一并清理 `SerialPortScannerService.cs:208` 的 emoji 日志存量违规 |
| T1.7 | 2026-07-25 | `bc8a258` | `Plc` 节新增 3 个可选键（HeartbeatIntervalSeconds=2 / ReconnectBackoffSeconds=5 / SupervisorRestartDelaySeconds=5，缺省=原硬编码值）；`PlcOptions`/`MitsubishiPlcOptions`+工厂映射、三驱动接线、PLC 设置页 3 个输入框+校验；+2 单测，总计 269 全绿；旧配置不写新键行为不变。**阶段 1 收官** |
| T2.1 | 2026-07-25 | `2aea6b2` | 按用户定位从"校验"升级为 **Address Object**：`McAddress`（解析/规范化/值相等，X/Y/B/W 十六进制偏移）+ `AddressParseError` 结构化错误码 + `MitsubishiAddressException:ArgumentException`；读写预检接入（合法路径不变）；`InternalsVisibleTo`+测试项目 TFM 调整 net8.0-windows；+36 单测，总计 305 全绿；区间合并/运算留 T4.4 |
| T2.2 | 2026-07-25 | `2df5ccd` | 西门子 `S7Address`（I/Q/M/DB 区 + DB 号 + 偏移 + 位号 0-7）同构落地；单读写规范化调用、批量预检保持键名；+32 单测，总计 337 全绿 |
| T2.3 | 2026-07-25 | `f2f5417` | 欧姆龙 `FinsAddress`（D/C/W/H/A/E 区 + E 区体号 + 偏移 + 位号 0-15）同构落地；+32 单测，总计 369 全绿。**阶段 2 收官：三品牌地址全部对象化，业务仍只见字符串、协议语法已全部收敛进驱动内部** |
| T3.1 | 2026-07-25 | `74ff193` | 设备抽象契约（方案 A）：`IDevice`（Info/State/Transitioned/Connect/Disconnect，**无 IsConnected**、不含读写能力）+ `DeviceInfo`（预留 Group/Description 可选元数据）+ `DeviceType` 粗粒度三值（Plc/Scanner/Other，细分归 DriverType）+ 契约层 `DeviceConnectionTransition` record；纯新增不接线，+3 单测总计 372 全绿 |
| T3.2 | 2026-07-25 | `7601b4f` | `IDeviceRegistry` 契约 + `DeviceRegistry` 实现（ConcurrentDictionary、ID 大小写不敏感、重复 ID 抛错、注册事件）；单机单设备起步按多设备预留，未接线；+7 单测总计 379 全绿 |
| T3.3 | 2026-07-25 | `e1799b4` | 三驱动实现 `IDevice`（Info/State/Transitioned 转发，`ConnectAsync`/`DisconnectAsync` 签名天然一致零改动）+ `ActivePlcService.InnerDevice` 探针 + `PlcDeviceAdapter`（惰性订阅转发）+ DI 注册 + Bootstrapper 泛型注册循环；`IPlcService` 解析链不变；+6 单测总计 385 全绿 |
| T3.4 | 2026-07-25 | `0fd2711` | 扫码枪实现 `IDevice` + 重连监控迁入 `ConnectionSupervisor`（净 -24 行；probe=端口枚举+句柄、connect=关残留重开、5s/0s 对齐原语义）；**A 方案首开保留抛出**（注释说明的例外）；`ErrorReceived` 驱动直驱迁移；T1.6 遗留 emoji 清理；DI 注册自动登记；385 测试全绿。**第四套独立重连消除，全部设备统一监督器** |
| T3.5 | 2026-07-25 | `941d189` | 统一事件 `DeviceStateChangedEvent(Info, Transition)` + `DeviceStateEventPublisher`（Bootstrapper 注册循环逐设备 Attach）；旧四事件并行不动（退役评估留 T5.x）；+2 单测总计 387 全绿。**阶段 3 收官** |
| T4.1 | 2026-07-25 | `2f02c8d` | Tag 模型契约（按用户五条调整）：`TagValue(Value, Quality, Timestamp:DateTimeOffset, Version, Error)`（Version 语义=最新值表写入时分配/直连为 0）+ `TagDefinition` 纯配置形状（**无 PollIntervalMs**，采集策略归 T4.4 采集配置；Address 为字符串配置、解析缓存归 Infra）+ `TagDataType` 扩展 11 型（Int64/UInt64/Double/ByteArray 预留非 PLC）+ `TagQuality` 三态/`TagAccess`；纯新增，+3 单测总计 390 全绿 |

### 演进过程中发现的新问题（停车场）

> 执行中发现的、不属于当前 Task 的问题记录在此，另行立项，不顺手修改。

- （暂无）

---

**文档版本**: v1.0（2026-07-25 初版，待评审）
**关联文档**: `AGENTS.md`、`docs/ARCHITECTURE.md`、`docs/PROJECT_STATUS.md`、`docs/IMPROVEMENT_PLAN.md`
