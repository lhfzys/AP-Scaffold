# AP-Scaffold 框架评估：作为单机上位机外包底座的可行性

> 评估对象：`AP-Scaffold`，分支 `review/framework-evaluation`，基线提交 `e1def11`（2026-08-07）。
> 评估日期：2026-11-18。
> 评估方式：文档交叉核对 + 全量源码阅读 + Release 构建 + 三个测试项目 + 发布并启动宿主 + 安装包编译 + NuGet 漏洞审计。
> 范围：Standalone 单机模式 + SQLite；Server/Client（gRPC）与 PostgreSQL 按项目既定决策视为冻结能力，只评估其保留成本。
>
> 本次只做评估与文档，不修改任何代码；分支上的唯一变更是本文件。
>
> **2026-11-18 实施进展**：评估中的 P0 项已落地并合并 `main`（`0523fcf`）：数据根/Program Files 分离、PLC 写路径去重试、A 模式（免登录+审计）、版本化迁移 + `VACUUM INTO` 备份、gRPC 物理移出交付链、CI + 非提权安装冒烟、**全仓 .NET 10 迁移 + 依赖漏洞 0 High**、**DH-9 配置保存 merge/备份恢复**、**DH-10 扫码枪首开重连/有界 Channel**、**DH-13 新项目模板/示例插件**、**DH-8 单容器/初始化超时/插件状态页**、**DH-7 插件档位/启用禁用**、**DH-12 测试连接/通讯日志/CSV 导入**。当前主解决方案 **33 个项目**、测试 **529 通过**、发布目录 0 个 Grpc/AspNetCore 文件。下文“37 个项目 / 34 个解决方案项目 / 485 测试 / 安装包要求 ASP.NET Core / net8 EOL”等数字与判断是**评估时快照**，保留用于对照，不代表当前状态。

---

## 0. 结论摘要

**一句话判断：这是一个技术骨架明显高于平均外包项目、但产品化程度明显不足的单机上位机底座。**

- **技术架构约 8.5/10**：分层、契约、插件贡献者、设备运行时与 Tag 系统是真实资产，不是 PPT 架构。
- **单机外包交付就绪度约 3.5/10**：安装后的数据写入、PLC 写重试、认证边界、数据库迁移、升级保留现场数据等关键环节仍会直接伤害现场。
- **综合约 6.2/10**：值得继续投入，可以作为“底座候选”；但**不建议不加改造直接承接第一个付费项目**。

最需要先解决的五个问题：

| # | 问题 | 性质 |
|---|------|------|
| 1 | 安装到 `Program Files` 后，数据库/日志/配置/报表仍写入安装目录 | 标准用户首次运行即可能失败 |
| 2 | PLC 写操作被通用重试策略重复执行（最多 6 次） | 工业现场安全风险 |
| 3 | 认证与授权只存在于 UI 层，登出重登不重建权限状态，默认还是匿名全权限 | “有登录”但不构成安全边界 |
| 4 | 目标框架 .NET 8 已于 2026-11-10 结束支持，且传递依赖含高危 `System.Text.Json 8.0.0` | 新机部署与合规风险 |
| 5 | 数据库无版本化迁移；备份是三次文件复制；升级会用发布包覆盖 `tags.json` | 升级/断电场景的数据丢失风险 |

**建议的下一步不是继续加功能，而是用 3–4 周做一次“交付加固”，再用 4–5 周做“插件治理与认证收敛”。** 具体路线见第 8 节。

---

## 1. 评估范围与实测结果

### 1.1 规模

| 指标 | 数值 |
|------|------|
| 项目 | 37 个（解决方案 34 + 测试 3） |
| 插件 | 14 个（硬件 4、业务 2、系统 8） |
| C# 源码 | 347 个文件，约 21,093 行 |
| XAML | 32 个文件，约 3,227 行 |
| 文档 | 11 份（含 4 份编码规范） |
| 测试 | 45 个测试文件，485 个用例 |

### 1.2 实测

| 项目 | 结果 |
|------|------|
| `dotnet build AP-Automation.Platform.slnx -c Release` | 0 错误；9 个 NU1701 警告（OpenTK / SkiaSharp.Views.WPF 的 net462 资产） |
| `dotnet test`（Core / Shared / Infra） | 130 + 52 + 303 = **485 通过，0 失败** |
| `dotnet publish`（win-x64，框架依赖） | 243 个文件，约 46.3 MB |
| 发布目录启动 | 主窗口正常渲染；SQLite、审计、采集引擎、14 个插件加载完成 |
| Inno Setup 编译 | 成功，生成约 13.4 MB 安装包 |
| `dotnet list package --vulnerable --include-transitive` | Host / Grpc / Logging 命中 `System.Text.Json 8.0.0` 两个 High 级公告 |

启动验证使用的是**发布目录**，不是 `Program Files` 安装目录。安装后的写入问题属于静态审查结论，原因见 5.1；本次没有触发 UAC 做实机安装，这一点必须如实标注。

### 1.3 评估边界

- 没有连接真实三菱/欧姆龙 PLC；西门子驱动只在既有记录中声称仿真验证过，本次未复验。
- 没有做 7×24 小时稳定性、并发写压力、断电恢复试验。
- 没有做真实客户现场的网络、域账号、杀毒软件、组策略兼容性验证。
- 因此本文把“代码明确成立”的问题与“需要现场验证”的问题分开表述；后者集中在附录 B。

---

## 2. 与“单机上位机外包”的匹配度

### 2.1 匹配矩阵

| 项目类型 | 匹配度 | 判断 |
|---------|--------|------|
| 单机产线 HMI / 设备监控 | ★★★★☆ | 设备、Tag、采集、布局、主题都对口；需先修 5.1/5.2 |
| 单机 MES 客户端（查询 + 报表 + 少量写） | ★★★★☆ | 现有报表/配方/用户模块可复用；认证需收敛 |
| 配方/参数管理为主的项目 | ★★★☆☆ | 配方模块仍是骨架：版本历史、审核、回滚、切换联动都缺 |
| 多 PLC / 多工位单机 | ★★☆☆☆ | Device 抽象已预留，但运行时仍是“单 PLC”假设 |
| 多机分布式 / Server-Client | ★☆☆☆☆ | gRPC 已冻结，且代码仍拖累部署 |
| 强合规、多用户、等保类项目 | ★☆☆☆☆ | 当前认证不是安全边界，不能承诺 |
| 快速 Demo / POC | ★★★★☆ | 免登录即可演示；但 Demo 与交付必须明确区分 |

### 2.2 核心判断

**它更像“设备接入与管理平台底座”，还不是“完整 HMI 底座”。**

已经具备：插件壳、导航/设置贡献者、PLC 三品牌、扫码枪、连接监督、Tag 点表、采集、最新值、基础审计、用户/角色、报表导出、安装包。

仍然缺失（按对 HMI 的重要性排序）：

1. **报警管理**：没有报警定义、报警记录、确认/清除、报警历史。首页“当前告警”只是离线设备数 + Bad 质量点数，不是业务报警。
2. **历史数据**：Tag 值持久化仍是 TODO；没有历史趋势、班次/生产报表的数据基础。
3. **配方闭环**：切换不持久化、不发事件、无版本历史/审核/回滚。
4. **常用协议**：无 Modbus TCP/RTU、OPC UA、MQTT；对多数外包项目，Modbus 的优先级高于第三个 PLC 品牌。
5. **现场诊断**：没有“测试连接”、通讯报文日志、点表批量导入、PLC 地址/类型交叉校验。

这决定了它的最佳定位：**先作为“单机设备接入 + 基础管理框架”交付，报警/历史/配方按项目增量补，而不是宣称开箱即用的完整 HMI。**

### 2.3 复用经济学

这套框架的复用收益不是线性的：

- 第 1 个项目：学习 34 个解决方案项目、7 个契约、双容器、插件加载、贡献者、Tag、安装包，成本高于写一个单体 WPF。
- 第 2–3 个项目：硬件层、布局、主题、安装包开始回本。
- 第 4 个项目以后：才真正体现“平台”的价值。

因此它适合**有持续外包订单、且项目形态相似**的团队；如果一年只做一两个差异很大的项目，这套复杂度可能不划算。真正能提高复用收益的，不是继续加抽象，而是：

- 一个“新项目模板”（只带布局 + 一个硬件插件 + 一个业务插件）；
- 一个“交付档位”（插件清单/菜单/权限/安装包一起生成）；
- 一个“现场加固清单”（数据目录、备份、诊断、升级）。

---

## 3. 值得保留的资产

### 3.1 分层与契约

`Hosts → Plugins → Contracts ← Infra` 的主方向是健康的，插件之间零项目引用，跨插件通信走契约事件。契约层已经基本回到依赖图底部（2026-07-28 删除了 `Contracts.Core → AP.Core` 死引用），这是很多同类项目做不到的。

值得保留的约束：

- 插件只依赖 Contracts / Shared，不直接依赖其它插件；
- 协议地址只允许出现在驱动内部与 `tags.json`；
- 设备访问统一收口到 `ITagService` / `IDeviceRegistry` / 事件。

### 3.2 Device Runtime + Tag

这是全仓质量最高的部分：

- `DeviceConnectionStateMachine` 六态，作为连接状态唯一事实来源；
- `ConnectionSupervisor` 把心跳、重连、监督重启收敛成一套，替代了四套复制看门狗；
- 三品牌各自 internal Address Object，协议语法不外泄；
- `TagValue` 带 Quality/Timestamp/Version，通信失败返回 Bad 而不是抛异常；
- 采集引擎支持按周期分组、批量读、三级降级、变化事件；
- 点表支持启动快速失败与保存热重载。

这套模型比“直接包一层 IoTClient”高一个层次，是框架真正的差异化。**后续优化不要动它的主结构，只修接线问题（写重试、单 PLC 路由、配置校验）。**

### 3.3 贡献者模式

`INavigationContributor` + `NavigationMenuItemBuilder` 与 `ISettingsContributor` 让新页面/新配置页的接入成本可控。Builder 有单元测试，去重、排序、权限、白名单、默认项逻辑完整。这是“新项目加页面”最应该保留的入口。

### 3.4 工程基础

- 485 个测试，设备运行时/地址对象/状态机覆盖较好；
- Release 构建 0 错误、可重复，发布/安装包脚本可重复；
- Serilog、Polly、全局异常、单实例、托盘、崩溃日志都有；
- 文档密度高，交接意愿强。

### 3.5 明确的“冻结”决策

把 Server/Client 与 PostgreSQL 标为冻结，是对“绝大多数外包是单机”的正确战略收敛。问题不在决策，而在**冻结代码仍留在交付链里**（见 5.6）。

---

## 4. 架构层面的“过度设计”与“欠设计”

这套代码的复杂度预算分配不太均衡：**在设备抽象上很舍得，在交付工程上很省。**

### 4.1 过度设计（对当前单机定位收益为负）

| 项 | 现状 | 判断 |
|----|------|------|
| 14 态插件状态机 | 实际只使用 11 态（含 Failed）；`Degraded/Frozen/Deprecated` 无写入点 | 5–6 态足够；保留状态日志即可 |
| `PluginCapabilities` 14 位能力声明 | 仅 2 个插件标注，无运行时读取/强制 | 要么落地，要么删掉，避免“看起来有沙箱” |
| `IEventBus` / `AddPlatformCore` / `IApplicationLifecycle` | 无生产消费者 | 死抽象，删除或接线 |
| 三套 DI 容器 | 临时 MS 容器 + 最终 MS 容器 + DryIoc 容器 | 最大的一处“架构税”，见 7.1 |
| 收集式 ALC | `base(true)` 但成功路径从不 `Unload()` | 既不回收，又承担跨 ALC 类型风险；要么真做，要么改非收集 |
| 全部 14 个插件默认随包 | 没有插件清单/启用开关/交付档位 | 每个客户都继承登录、用户、角色、审计、配方、报表菜单 |
| gRPC 冻结代码留在主启动链 | 仍引用 `Microsoft.AspNetCore.App`，安装包要求 ASP.NET Core 运行时 | 单机零收益，纯部署负担 |

### 4.2 欠设计（真正影响交付）

| 项 | 现状 | 后果 |
|----|------|------|
| 数据目录 | 全相对路径，落在安装目录 | 标准用户不可写；日志随 CWD 漂移 |
| 数据库迁移 | 各模块自己 `SyncStructure`，无版本表 | 升级/降级不可控，缺列/改列运行期才炸 |
| 认证/授权 | UI 层检查，服务/仓储层零强制 | 插件或旧会话可绕过 |
| 插件治理 | 无 manifest、无版本约束、无启用开关 | 现场残留旧插件、无法按项目裁剪 |
| 交付流水线 | 零 CI、零安装冒烟 | 安装问题只能到客户现场发现 |
| 现场诊断 | 无测试连接、无报文日志、无点表批量导入 | 调试靠人肉和日志文件 |

**结论：不是“抽象太多”本身有错，而是抽象都集中在设备层，交付层几乎没有抽象。** 下一步应把一部分复杂度预算从“更多设备能力”移到“可安装、可升级、可授权、可诊断”。

---

## 5. 五个产品化缺口（按严重度）

### 5.1 P0：安装到 Program Files 后，运行数据仍写安装目录

**证据**

- 安装脚本把程序装到 `{autopf}`（通常是 `C:\Program Files\...`）：`installer/setup.iss:25`，且 `PrivilegesRequired=admin`（`:32`）。
- SQLite 连接串是相对路径 `Data Source=local_data.db`：`platform/hosts/AP.Host.Desktop/Configuration/appsettings.Standalone.json:20`；数据库路径按进程当前目录解析：`platform/infra/AP.Infra.Database/Configuration/DatabaseServiceExtensions.cs:41`。备份却按 `AppContext.BaseDirectory` 解析（`:153`），两者基准不一致。
- 日志路径是相对路径 `logs/log-.txt`：`platform/infra/AP.Infra.Logging/Configuration/SerilogConfiguration.cs:40`。
- 报表根目录默认 `reports`：`platform/infra/AP.Infra.Report/Configuration/ReportOptions.cs:42`。
- 配置写回固定 `{BaseDirectory}/Configuration`：`platform/shared/AP.Shared.Utilities/Helpers/ConfigurationHelper.cs:73`。
- 程序没有 app.manifest 提权声明，安装后的快捷方式以普通用户启动；`Program Files` 默认 ACL 对普通用户是读/执行，不允许创建/修改文件。

**影响**

- 标准用户首次启动时，SQLite 可能在 `Program Files` 下创建数据库失败；即使以管理员首次运行成功，后续操作员也无法写数据库。
- 日志、报表、配置保存会在不同位置失败；`PRAGMA` 失败只记 Warning（`DatabaseServiceExtensions.cs:72-76`），故障会延后到第一次查询/保存才暴露。
- 备份路径与数据库路径基准不同，可能备份错文件或静默跳过。
- 这会让“安装成功”在客户现场变成“能打开、不能用”。

**修复（P0，约 2–3 人日）**

1. 引入统一 `DataRoot`：`%ProgramData%\AP-Scaffold\`（机器级）或 `%LocalAppData%\AP-Scaffold\`（用户级）；数据库、日志、报表、配置备份全部走它。
2. 安装程序只把程序文件放 `Program Files`；首次启动创建 `DataRoot` 并做可写自检，失败时给出明确错误并退出，而不是 Warning 后继续。
3. 配置文件分两层：`Program Files\Configuration` 只读默认值；`DataRoot\Configuration` 覆盖值；写回只写 DataRoot。
4. `setup.iss` 增加 `[Dirs]` 权限（若坚持数据放安装目录，至少给 Users Modify），但更推荐前者。
5. 在 CI 的安装冒烟测试中，用标准用户启动一次，验证 DB/日志/配置/报表四条写路径。

### 5.2 P0：PLC 写操作被通用重试策略重复执行

**证据**

- `PlcRetryPolicy` 对任意 `Exception` 做固定 500ms、可配置次数的重试：`platform/infra/AP.Infra.Resilience/Policies/PlcRetryPolicy.cs:15-20`。
- 三菱 `WriteAsync` 把实际写入包在 `_pipeline.ExecuteAsync` 内：`platform/plugins/hardware/AP.Plugin.Plc.Mitsubishi/Services/MitsubishiPlcService.cs:303-332`；西门子、欧姆龙同构。
- 三菱 `WriteBatchAsync` 在管道内再逐条调用 `WriteAsync`：同文件 `:386-389`，形成嵌套重试。
- 写入后没有回读校验，也没有幂等键/去重键。

**影响**

“超时”不代表“没执行”。如果 PLC 已执行但响应丢失，重试会把启动位、配方号、触发点、清零命令再执行 5 次。对工业现场，这是**安全级缺陷**，优先级高于任何 UI 问题。

**修复（P0，约 1–2 人日）**

1. 读写分离韧性：读/连接可用 `PLC-Retry`；**写路径默认不重试**，或最多 1 次且必须由调用方显式选择。
2. 写入后可选回读校验；失败返回 Bad/失败审计，由业务决定是否人工重试。
3. 批量写在管道外编排，禁止管道内再调 `WriteAsync`。
4. 为写操作增加 `operationId`/幂等键（可选），至少提供“安全写/快速写”两种语义。

### 5.3 P0：认证与授权不是安全边界

详见第 6 节。这里只给结论：

- 默认 `Security:Enabled=false`，匿名身份拥有 `["*"]` 权限；
- 服务层/仓储层没有权限校验；
- 登出重登不重建菜单、视图、权限行为；
- 密码重置会把用户角色清空；
- 审计是业务代码手工调用，失败可被吞掉。

对“无人值守、可信操作员、物理隔离”的单机场景，可以接受“UI 门禁”作为**防误操作**手段；但一旦客户把它当登录/权限/审计卖点，它就是**假安全**。

### 5.4 P0：.NET 8 已结束支持，且依赖含高危漏洞

**证据**

- 目标框架 `net8.0` / `net8.0-windows`：`Directory.Build.props:9`、`platform/hosts/AP.Host.Desktop/AP.Host.Desktop.csproj:6`。
- .NET 8 于 **2026-11-10** 结束支持；当前已是 2026-11-18。
- `dotnet list package --vulnerable --include-transitive` 显示 Host / Infra.Grpc / Infra.Logging 的传递依赖 `System.Text.Json 8.0.0` 命中两个 High 级公告（`GHSA-hh2w-p6rv-4g7w`、`GHSA-8g4q-xg66-9fp4`），来源链为 `Serilog.Settings.Configuration 8.0.0 → Microsoft.Extensions.DependencyModel 8.0.0 → System.Text.Json 8.0.0`。
- 安装包仍检测 .NET 8 桌面 + ASP.NET Core 8 运行时：`installer/setup.iss:99-105`。

**影响**

- 新机器只装 .NET 10 时，net8 应用默认不会跨大版本 roll-forward，安装包会拦或应用起不来。
- 继续分发 EOL 运行时，安全补丁与合规都不可持续。
- 高危 STJ 是实际可达的依赖，不是“仅测试包”。

> **迁移前必须做客户 OS 盘点（2026-11-18 补充）**：.NET 10 官方支持矩阵把 Windows 10 限定为 **LTSC/Enterprise（1607/1809/21H2）**，Windows 11 与 Server 2012+ 也支持；**Windows 10 Home/Pro 22H2 不在官方支持列表**，Windows 7/8.1 完全不支持（见 [Microsoft .NET Windows 安装文档](https://learn.microsoft.com/en-us/dotnet/core/install/windows)）。如果客户大量使用 Win10 Pro 22H2，.NET 10 可能“能跑但不受支持”；如果是 Win7/8.1，当前 net8 也已经不在官方支持范围。此时不应全仓硬迁，而应：① 先盘点客户 OS；② 新项目/新客户在 Win10 LTSC/11/Server 上试点 .NET 10；③ 老客户暂留 net8 并单独修补 `System.Text.Json`，或做 net8/net10 双构建。迁移决策应以客户 OS 矩阵为准，不以“最新 LTS”单独决定。

**修复（P0，约 3–5 人日）**

1. 全仓升级到 `net10.0` / `net10.0-windows`，同步升级 `Microsoft.Extensions.*` 到 10.x；Prism 9 / MaterialDesign 5.3 / FreeSql 3.5 / LiveCharts 2.0.4 先做兼容性回归。**前提是客户 OS 在 .NET 10 官方支持矩阵内（Win10 LTSC/Enterprise、Win11、Server 2012+）；否则先走双构建/暂留 net8 并修补依赖。**
2. 中央包管理增加 `System.Text.Json` 的显式安全版本（迁移到 .NET 10 后通常随框架解决，但仍要跑漏洞审计）。
3. 安装包只检测 .NET 10 桌面运行时；如果同时移出 gRPC，则不再需要 ASP.NET Core 运行时。
4. CI 增加 `dotnet list package --vulnerable` 门禁。

### 5.5 P0/P1：数据库无迁移、备份不原子、升级覆盖点表

**证据**

- `UseAutoSyncStructure(false)`：`platform/infra/AP.Infra.Database/Configuration/DatabaseServiceExtensions.cs:50`；建表由 `SecurityDbInitializer.cs:42`、`RecipeDbInitializer.cs:24`、`ReportDatabaseInitializer.cs:27`、`AuditService.cs:19` 各自执行，没有版本表/迁移顺序。
- 备份是三次 `File.Copy`（db、-wal、-shm）：`DatabaseServiceExtensions.cs:113-121`；失败只 Warning（`:127`）；单代 `.bak` 每次启动覆盖；全仓没有恢复入口。
- `PRAGMA` 只执行一次，没有 `busy_timeout`：同文件 `:65-70`；WAL 下并发写仍可能 `SQLITE_BUSY`。
- 安装升级只保护 `Configuration\appsettings*.json`：`installer/setup.iss:47-49`；`Configuration\tags.json` 会被新发布包覆盖，现场点表丢失。
- 安装脚本不清理旧插件目录，源码删除的插件仍会被加载（AGENTS.md 也记录了这个坑）。

**影响**

- 升级时加列/改列没有数据迁移，可能运行期才报错；降级无法识别新 schema。
- 断电/崩溃时 `.bak` 可能缺少最新 WAL 事务，不能作为可靠恢复点。
- 现场花时间调好的点表，一次升级就回到出厂示例。

**修复（P0/P1，约 5–7 人日）**

1. 引入 `PRAGMA user_version` + 有序迁移脚本；升级/降级双向校验；迁移前自动备份，迁移失败阻断启动。
2. 备份改为 SQLite Online Backup 或 `VACUUM INTO`，生成带时间戳的多代备份，并做 `quick_check`；提供“从备份恢复”入口。
3. 设置 `busy_timeout`（如 5000ms），写操作使用事务/单写者队列。
4. 安装脚本把 `tags.json` 当现场数据（`onlyifdoesntexist` 或迁移到 DataRoot），升级时保留；清理旧插件目录。
5. 增加升级冒烟测试：旧版 DB + 旧版 tags.json → 安装新版 → 启动 → 数据/点表仍在。

### 5.6 P1：gRPC 冻结了，但仍在交付链里收费

**证据**

- Host 直接引用 `Microsoft.AspNetCore.App`：`AP.Host.Desktop.csproj:27`；publish 包含整套 Grpc 程序集；安装包要求 ASP.NET Core 8 运行时（`setup.iss:99-105`）。
- `Bootstrapper` 仍保留 Server/Client 分支与 Kestrel 启动：`Bootstrapper.cs:193-200,440-452,492-523`；`AP.Infra.Grpc`、`AP.Contracts.Communication` 仍在解决方案中。
- `Grpc-CircuitBreaker` 注册但零调用点（除测试）。

**影响**

- 每个单机客户都被迫安装 ASP.NET Core 运行时；如果移出 gRPC，很多现场只需要桌面运行时。
- 攻击面、发布体积、启动分支、维护认知都增加。
- “冻结”目前是文档状态，不是构建状态。

**修复（P1，约 2–3 人日）**

1. 把 Server/Client 代码迁到独立分支/独立仓库/`frozen/` 目录，主解决方案不再引用。
2. Host 删除 `FrameworkReference Microsoft.AspNetCore.App` 与 gRPC 分支；安装包只检测桌面运行时。
3. 保留 proto 与 ADR，写清解冻条件；不要用 `#if` 把死代码留在主链。

---

## 6. 专题：为什么说 Auth “治标不治本”

### 6.1 先定义威胁模型

单机外包的典型现场：

- 一台工控机，一个共享 Windows 账号，操作员三班倒；
- 没有外网，物理接触可控但并非不可达；
- 客户可能要求“登录、权限、审计”，但不一定做等保；
- 本地管理员总能改配置、读数据库、替换程序；**单机应用不可能防住本地管理员**。

在这个模型下，认证的目标通常是：

1. 防误操作（不是防恶意攻击）；
2. 关键操作可追责；
3. 不同角色看到不同功能；
4. 客户审计时能拿出记录。

当前实现连第 2、3 条都不稳：**登出重登不重建权限、重置密码清空角色、审计可被业务吞掉。**

### 6.2 当前结构

```
登录窗口 → IdentityService.CurrentUser（进程内可变单例）
                     │
        ┌────────────┴─────────────┐
        │                          │
   UI 菜单/视图/控件          服务/仓储/命令
   HasPermission 检查         没有任何检查
        │                          │
        └──── 权限只是“可见性” ────┘
```

关键事实：

- `IdentityService.CurrentUser` 是单例上的一个属性（`IdentityService.cs:20`），不是 scoped session，没有过期、锁屏、会话 ID。
- `HasPermission` 只在 UI 插件与 `PermissionBehavior` 中调用，Security/Recipe/Report/User 的服务实现内没有任何检查。
- 插件通过 `PluginBase.ServiceProvider` 持有根容器（`PluginBase.cs:37-41`），可以解析 `IFreeSql`、`IUserRepository`、`IRecipeManager`；能力声明 `[RequiresCapabilities]` 没有运行时读取者。
- 匿名身份 `Permissions=["*"]`、`HasPermission=>true`（`AnonymousIdentityService.cs:12-35`），默认配置 `Security:Enabled=false`（`appsettings.json:41-46`）。

### 6.3 具体缺陷

| 级别 | 缺陷 | 证据 | 影响 |
|------|------|------|------|
| Critical | 默认关闭安全 = 匿名全权限 | `appsettings.json:41-46`；`AnonymousIdentityService.cs:12-35`；`SecurityServiceExtensions.cs:21-37` | 能开机就能改 PLC/点表/配方/配置；审计记 `anonymous` |
| High | 授权只在 UI，服务/仓储零校验 | `UserRepository.cs:11-99`、`RecipeManager.cs:11-19`、`ReportService.cs:14-23` 无身份依赖；`PermissionBehavior.cs:60-88` 只改可见性 | 任何插件/旧会话可绕过菜单直接调服务 |
| High | 登出重登不重建菜单/视图/权限 | `LayoutViewModel.cs:139-160` 只隐藏窗口并刷新头部；`SidebarViewModel.cs:52-85` 只构造时算一次；`PermissionBehavior.cs:39-58` 只在 Loaded 求值 | 管理员退出后操作员登录，仍见管理员菜单/已加载页面，可读用户列表/审计/配方 |
| High | 权限模型不覆盖数据边界 | `AuditDailyReportProvider.cs:21-29` 用 `IAuditService` 生成审计日报；`ReportListViewModel.cs:112-140` 只需 `report.export` | 有报表导出权限即可导出包含全部审计的记录，不需要 `audit.view` |
| High | 无失败锁定/口令策略 | `IdentityService.cs:22-40` 无计数；`User.cs` 无锁定字段；`ChangePasswordViewModel.cs:55-71` 只校验非空+一致 | 可无限爆破；可把密码改成 `1` |
| High | 重置密码固定 `admin123` 且清空角色 | `UserListViewModel.cs:171-193` 新建 `UserInfo` 不含 `Roles`；`UserRepository.cs:78-80` 先删全部角色再按空集合绑定 | 重置后用户失去全部权限；重置唯一管理员会永久失去管理入口 |
| High | 强制改密只在启动检查 | `Bootstrapper.cs:96-106`；`LayoutViewModel.cs:151-155` 重登不检查 | `MustChangePassword` 可通过“登出→重登”绕过 |
| Medium | 种子权限每次启动回填 | `SecurityDbInitializer.cs:64-89,119-135` | 管理员在 UI 撤销的种子权限，重启后复活 |
| Medium | `admin` 按用户名识别 | `SecurityDbInitializer.cs:91-106`；用户名可编辑 `UserEditViewModel.cs:149-161` | 改名/删除 admin 后，下次启动重建 `admin/admin123` |
| Medium | 审计可篡改/可绕过 | `AuditService.cs:13-20` 与业务同库；无哈希/签名；调用方 catch 后静默（`LayoutViewModel.cs:196-199`） | 本地用户/插件可改删；审计失败操作照常 |
| Medium | SQLite/备份明文、配置可关安全 | `appsettings.Standalone.json:17-22`；`DatabaseServiceExtensions.cs:103-121`；`appsettings.json` 可写 | 可拷走哈希离线爆破；改一行配置即关闭认证 |

再举一个具体例子：`SystemSettingsPlugin` 把 `SettingsShellView` 注册到 `ContentRegion` 时没有检查 `system.settings`（`SystemSettingsPlugin.cs:57-68`），权限只存在于 Sidebar 菜单层。只要通过旧会话、快捷入口或代码触发导航，配置页就会加载；而 `SettingsService.SaveSettingsAsync` 也没有权限校验。

### 6.4 根因

不是“少一个锁定字段”，而是四个结构性问题：

1. **身份是全局可变单例，不是会话。** 没有 session 生命周期、没有身份变更事件，UI 无法在登录/登出时重建。
2. **授权被当成 UI 关注点。** 菜单、视图、按钮都检查，但服务/仓储/命令层不检查；权限边界没有落在领域入口。
3. **仓储直接暴露给插件。** 插件拿到根容器，就能绕过任何应用服务；`RequiresCapabilities` 只是注释。
4. **审计是“调用方记得写”的 best-effort。** 不是拦截器/事务/outbox 保证，失败可以被吞掉。

这四条不解决，只加“密码复杂度/失败锁定”仍然是治标。

### 6.5 三条可选路线

#### 路线 A：轻身份 + 审计（推荐给大多数单机外包）

**定位**：不卖“RBAC 安全”，卖“操作员可追溯 + 防误操作”。

- 登录只保留“操作员选择 + PIN/密码”，取消角色/权限管理 UI；
- 所有关键操作（PLC 写、配方切换、配置/点表修改、报表导出）强制写审计，审计失败即拒绝；
- 空闲锁屏/换班登录；
- 权限只保留“管理员/操作员”两档，或直接取消。

**工作量**：3–5 人日。**收益**：UI/概念大幅简化，审计可信度提升，不再假装有 RBAC。

#### 路线 B：真实 RBAC（客户明确要求多角色/审计时）

- 新增 `ISessionService`（当前用户、登录时间、过期、锁屏、身份变更事件）；
- 新增 `IAuthorizationService`，在应用服务入口/MediatR Behavior 强制 `Demand(permission)`；
- 仓储改为 Infra 内部，不注册到插件可见容器；插件只拿能力受限的 facade；
- 登录/登出发布事件，Shell 重建 Sidebar/Region/PermissionBehavior；
- 密码策略、失败锁定、随机一次性重置、首次登录强制改密下沉到 IdentityService；
- 审计改为拦截器/事务内写入，关键操作失败即拒绝。

**工作量**：10–15 人日。**收益**：可以面对有合规要求的客户；**代价**：复杂度显著上升，必须配套测试。

#### 路线 C：保持现状 + 明确声明（不推荐作为卖点）

- 文档写明“Auth 仅用于防误操作，不是安全边界”；
- 默认关闭安全，Demo 用；
- 交付合同不承诺权限隔离/审计合规。

**只有当客户完全不关心登录/权限时才可选。** 一旦销售拿“用户/角色/权限”当卖点，路线 C 就是技术债。

**建议**：以路线 A 为默认交付形态；只有客户明确要求多角色/审计合规时才启用路线 B。无论哪条，**5.3 的会话重建、重置密码清角色、服务层高危操作校验必须先修**。

---

## 7. 其它重要问题（P1）

### 7.1 双容器与重复实例化

`Bootstrapper` 先 `services.BuildServiceProvider()` 建临时容器（`Bootstrapper.cs:217`），再建最终 MS 容器（`:259`），再把服务 `Populate` 进 DryIoc（`:287`），最后用 DryIoc 做插件 Initialize（`:417`）。插件实例用最终 MS 容器构造（`:267`），运行时却从 DryIoc 解析。后果：

- 插件注册的单例在 MS 容器与 DryIoc 各有一份；插件构造函数拿到的实例与 UI 解析到的实例可能不是同一个。
- 今天没爆，是因为现有插件构造函数基本只收 `ILogger`；一旦某个插件构造函数注入自己的服务，就会出现“两个单例”。
- 同一 `IPlugin` 被 `RegisterInstance` 多次（`:293`）；当前没有单解析消费者，一旦有人注入单个 `IPlugin`，解析语义就不明确。
- 初始化/启动没有超时/取消（`PluginLifecycleManager.cs:65-76` 用默认 `CancellationToken.None`），一个插件卡住会卡死启动。

**修复**：只保留一个容器（推荐 DryIoc，从注册开始就用它）；插件从同一容器解析；按类型注册、用 `IEnumerable` 消费；加 15s 初始化/启动超时与失败回滚。

另外，`StandardLayoutView` 的构造函数同时注入 `SidebarView` 和 `SidebarViewModel`（`StandardLayoutView.xaml.cs:8-13`），而 `SidebarView` 自己又注入一个 `SidebarViewModel`（`SidebarView.xaml.cs:11-15`）。后赋值的 VM 覆盖前者，被覆盖的 VM 已经在构造时订阅了 `ContentRegion.Navigated`（`SidebarViewModel.cs:93-98,107-119`），但没有退订，结果是两个 VM 同时响应导航、其中一个泄漏。修复：只注入 View 或只注入 VM，并在 `IDestructible.Destroy()` 中退订。

### 7.2 配置保存会抹掉导航配置

`AppConfigurationEditorViewModel.GetConfigurationValue()` 返回的 `AppConfigurationOptions` 只有 4 个字段（`AppConfigurationEditorViewModel.cs:51-60`），而 `AppConfigurationOptions` 还有 `LayoutMode`（`AppConfigurationOptions.cs:14`），并且 `appsettings.Standalone.json` 里还有 `DefaultNavigationTarget`、`NavigationWhenSecurityDisabled`。`ConfigurationHelper.UpdateAppSetting` 是整节替换（`ConfigurationHelper.cs:91-93`），所以保存“公司名/软件名”会：

- 把 `LayoutMode` 重置为 `Standard`（单页布局失效）；
- 删除角色文件中自定义的 `DefaultNavigationTarget` / `NavigationWhenSecurityDisabled` 覆盖项，回退到基文件默认值（如果写回目标是基文件，这些键会直接消失）。

这是**配置模型与写回策略不匹配**的典型，属于会在现场升级后突然出现的功能回归。

**修复**：编辑器按已知键做 merge，保留未知键；或配置模型补齐所有字段并显式保留。

### 7.3 启动失败会留下“转圈”界面

`OnInitialized` 的 `Task.Run` 最外层 catch 只关 Splash + MessageBox（`Bootstrapper.cs:470-488`），没有 `Shutdown()`；`MainWindowViewModel.IsLoading` 只在 `AppInitializedEvent` 时置 false（`MainWindowViewModel.cs:18,53-56`）。如果初始化异常发生在事件发布前，主窗口会一直显示 LoadingSpinner，用户只能强杀。

**修复**：失败时发布失败事件/关闭主窗口/退出进程，并提供“重试/查看日志/退出”失败页。

### 7.4 插件治理缺口

- 插件缺 DLL 时只记 Warning（`PluginLoader.cs:38-42,77-81`），不会进入 `_failedPlugins`；Required 插件“整个目录缺失”不会被识别为致命错误。
- `Dependencies` 只做存在性校验（`PluginGraphValidator.cs:48`），没有拓扑排序，启停仍按手写 `Priority`。
- `Metadata.Version` 只进日志（`PluginLoader.cs:116`），没有 host↔plugin 版本约束。
- `AssemblyScanner` 只取第一个 `IPlugin`（`AssemblyScanner.cs:17-21`），一个程序集多个插件会被静默忽略。
- 发布只 Copy 不清理（`Directory.Build.props:43-53`），旧插件目录会继续加载。
- 能力声明无运行时强制；插件拿根容器，等于全信任。

**修复**：插件 manifest（Id/Version/MinHost/Dependencies/Enabled）、拓扑排序、加载报告（UI 可见）、发布前清理、插件签名/哈希白名单（若引入第三方插件）。

### 7.5 报表/配方仍是半成品

- 报表：已存在 Failed 记录时直接返回其路径（`ReportService.cs:64-69`），UI 仍提示“生成成功”；补档无 UI 入口；调度器只补“昨天”，跨天停机不补。
- 配方：`CreateAsync` 忽略 `IsDefault`（`RecipeManager.cs:41-55`），初始化 DEFAULT 配方不会成为默认；`SwitchAsync` 只改内存、不发事件（`:96-104`）；无版本历史/审核/回滚；Code 无唯一约束。
- 这些模块默认随包发布，客户会以为可用。

**修复**：在完成前用插件开关隐藏，或明确标注“骨架”；先把报表状态/补档、配方版本/切换事件做实。

### 7.6 现场诊断不足

- PLC 配置页没有“测试连接”（只校验 IP 格式，`PlcConfigurationEditorViewModel.cs:92-95`）。
- 没有通讯报文日志/每次 IO 的结构化记录。
- 点表只能逐条编辑，无 CSV/Excel 批量导入（TagTableListViewModel）。
- 配置错误会静默回退（Model 解析失败回退默认型号），现场很难定位。
- 文档与实现有三菱地址语法冲突（文档写 X/Y 八进制，`McAddress` 按十六进制解析；西门子 `DB1.DBW0` 与解析器只接受 `DB1.0`）。

**修复**：测试连接、报文开关、批量导入、配置错误显式化、修正文档。这些都是外包现场“少跑一趟”的高 ROI 功能。

### 7.7 工程化缺口

- 无 `.github/`，无 CI；发布全靠手动，安装包没有冒烟测试。
- 无 `.editorconfig`、无分析器、无 `TreatWarningsAsErrors`；9 个 NU1701 长期存在。
- 测试零覆盖：`AP.Infra.Security`、`AP.Host.Desktop`、全部插件 VM/View、`AP.Infra.Recipe`、`AP.Infra.Logging`、安装/升级路径。
- 测试结构偏“数据形状”（选项/实体/特性），安全与宿主集成测试缺失。
- 文档多且互相漂移：`ARCHITECTURE.md` 仍写 2 个 PLC、契约依赖 Core；`AGENTS.md` 写 475 测试（实际 485）；`IMPROVEMENT_PLAN.md` 仍是 2026-07 的快照；`TESTING.md` 的 CI 路径过期。
- 版本号双事实源：`Directory.Build.props:5` 与 `setup.iss:15` 都是 1.0.0。

**修复**：GitHub Actions / Gitea Runner 最小流水线（build + tests + publish + 安装冒烟 + 漏洞审计）；补安全/宿主测试；文档收敛为“当前状态 + 历史快照”。

### 7.8 扫码枪首开失败后不会进入重连

**证据**

- `OpenAsync` 先 `TryOpenPort()`，成功后才 `_supervisor.Start()`；失败时把状态退回 Disconnected 并抛出（`SerialPortScannerService.cs:119-131`）。
- `ScannerPlugin.InitializeAsync` 捕获异常后只记日志（`ScannerPlugin.cs:63-70`），监督器从未启动。

**影响**

现场开机时如果 COM 口未就绪/被占用/扫码枪未上电，本次运行期间再也不会重连；必须重启应用。变更日志曾声称“初始化失败由监控持续重试”，与当前代码路径不符。

**修复**：首开失败也启动监督器，让 `ConnectPortAsync` 按 5s 周期重试；`OpenAsync` 只负责“启动监督器 + 首次尝试”，把失败暴露为设备状态（Reconnecting），而不是抛给插件终止。

---

## 8. 优化路线图

> 估算按 1 名熟悉本项目的工程师，单位为“人日”；不含硬件真机联调与客户现场时间。

### 阶段 P0：交付止血（约 15–20 人日 / 3–4 周）

| ID | 事项 | 关联 | 工作量 | 验收标准 |
|----|------|------|--------|----------|
| O1 | 统一 DataRoot + 安装器权限 + 启动可写自检 | 5.1 | 3 | 标准用户在 Program Files 安装后可正常建库/写日志/存配置/出报表 |
| O2 | PLC 写路径去重试/回读校验/批量写不嵌套 | 5.2 | 2 | 模拟“写成功但响应超时”只执行一次；失败审计如实记录 |
| O3 | .NET 10 迁移 + 依赖漏洞清零 + 安装器运行时检测更新 | 5.4 | 4 | `net10.0-windows` 构建/测试/发布通过；漏洞审计 0 High |
| O4 | 认证会话止血：登出重建 UI、重置密码保留角色、服务层高危操作校验 | 5.3/6 | 4 | admin→operator 重登后菜单/页面/命令全部按新身份；重置密码角色不变 |
| O5 | 数据库版本表 + 迁移 + 原子备份 + tags.json 升级保留 | 5.5 | 5 | 旧版 DB/tags 升级后数据与点表完整；备份可恢复 |
| O6 | CI 最小流水线 + 安装冒烟 | 7.7 | 2 | 每次 push 自动 build/test/publish/安装启动；失败即红 |

**P0 出口标准**：新机标准用户安装即可用；PLC 写不会被重复执行；升级不丢配置/点表/数据；认证会话不再越权；CI 能复现安装。

### 阶段 P1：产品化（约 20–25 人日 / 4–5 周）

| ID | 事项 | 关联 | 工作量 | 验收标准 |
|----|------|------|--------|----------|
| O7 | 插件 manifest + 启用/禁用 + 交付档位（profile） | 4.1/7.4 | 4 | 可只带布局+1 硬件+1 业务插件交付；菜单/服务/安装包一致 |
| O8 | 单容器 + 插件初始化/启动超时 + 加载报告 | 7.1 | 3 | 插件单例全局唯一；卡死插件 15s 失败；UI 可看插件状态 |
| O9 | 配置保存 merge + 备份/恢复修复 + RequiresRestart 按 diff | 7.2 | 2 | 保存公司名不改变布局/默认页/白名单；可按来源文件恢复 |
| O10 | 扫码枪首开失败进入监督器 + 有界 Channel + ReadTimeout | 7.8 | 1 | 开机无扫码枪，插上后自动连接，无需重启 |
| O11 | 审计索引/保留/异步写 + 权限拒绝审计 | 6.3 | 2 | 100 万行查询可接受；审计失败不影响主流程但有告警 |
| O12 | 报表状态/补档 + 配方版本/切换事件 | 7.5 | 4 | Failed 不再假成功；可按日期补档；切换持久化并发事件 |
| O13 | 现场诊断：测试连接、报文日志、点表 CSV 导入 | 7.6 | 4 | 配置页可测连接；点表可批量导入；地址/类型错误保存前拦截 |
| O14 | 新项目模板 + 示例业务插件 | 2.3 | 3 | `dotnet new` 生成可运行的最小项目，含导航/设置/报表/测试 |

### 阶段 P2：能力补齐（季度级）

| ID | 事项 | 说明 |
|----|------|------|
| O15 | 报警管理 | 报警定义/记录/确认/清除/历史；这是 HMI 的核心缺口 |
| O16 | Tag 历史/趋势 | 时序表或文件归档 + 查询/降采样/趋势图 |
| O17 | Modbus TCP/RTU 驱动 | 比继续加 PLC 品牌更高 ROI；复用 Device/Tag 模型 |
| O18 | 多设备路由 | `ITagService` 按 DeviceId 选设备，而不是恒定单 PLC |
| O19 | i18n/DPI/无障碍 | 内销可后置；外销/大客户前必须 |
| O20 | 代码签名/自动更新 | 降低 SmartScreen 与现场升级成本 |

### 不建议现在做

- **插件热卸载/热更新**：当前 ALC 实际不可回收，做完整热更新会引入跨 ALC 类型、DI 注销、Region 清理、事件退订四座大山；单机外包没有这个刚需。
- **能力沙箱**：在进程内用 ALC 做安全边界是伪命题；要么信任插件，要么进程隔离。
- **微服务/解冻 gRPC**：与“绝大多数单机”战略相反。
- **重写 Prism**：Prism 本身不是主要问题，双容器才是；重写成本高、收益低。新项目可评估轻 MVVM，但存量不建议动。
- **自包含发布**：在 ALC/DirectWriteForwarder 问题解决前不要开；先移出 gRPC 降低运行时依赖。

---

## 9. 评分

### 9.1 分维度

| 维度 | 权重 | 得分（10） | 理由 |
|------|------|-----------|------|
| 架构与分层 | 15% | 8.5 | 契约、插件边界、设备运行时清晰；扣分在双容器/死抽象 |
| 设备接入与 Tag | 15% | 8.5 | 全仓最佳；单 PLC 假设与写重试拖后腿 |
| 复用与扩展 | 20% | 7.0 | 贡献者模式好；34 项目/无模板/无 profile 提高门槛 |
| 稳定性 | 15% | 5.5 | 监督器/测试不错；写重试、扫码枪、启动失败、SQLite busy |
| 安全与审计 | 15% | 3.0 | 哈希合格；认证/授权/审计边界不成立 |
| 交付与运维 | 10% | 3.0 | 数据目录、升级、点表覆盖、无 CI/诊断 |
| 工程质量 | 5% | 6.0 | 485 测试；安全/宿主/插件零测试，无 CI/分析器 |
| 文档与交接 | 5% | 6.5 | 密度高；但漂移、重复、缺少“当前事实源” |
| **加权合计** | **100%** | **6.2** | 权重按“单机外包交付”的优先级设定 |

### 9.2 两个总分

- **技术架构分：8.5/10**——“这是一个懂架构的人做的项目。”
- **单机外包交付就绪分：3.5/10**——“这还不是一个能直接交给客户的产品。”

**综合：6.2/10。** 结论是“值得继续投入的底座”，不是“可以立即变现的成品”。

---

## 10. 给项目所有者的决策建议

### 10.1 先回答五个问题

1. **目标客户是谁？** 是“一条产线、一台设备、一个操作员”的小项目，还是“多工位、多用户、要审计”的中大型项目？
   - 前者：走路线 A，砍掉 RBAC，做轻身份+审计。
   - 后者：走路线 B，补服务层授权与会话。
2. **认证是卖点还是防误操作？** 如果销售材料里写“用户/角色/权限/审计”，就必须按安全边界做；否则会交付一个“看起来有、实际能绕过”的系统。
3. **gRPC 还要不要？** 如果 1–2 年内没有多机需求，物理移出交付链；保留分支/文档即可。
4. **每个项目真的需要 14 个插件吗？** 如果不需要，做插件 profile；这比继续加插件更能提高复用率。
5. **.NET 10 什么时候迁？** 建议下一个项目开始前；.NET 8 已 EOL，不要再新签基于 .NET 8 的交付。

### 10.2 推荐的目标形态

```
┌─────────────────────────────────────────────┐
│ 单机交付壳（一个 Host，一个容器，一个 DataRoot） │
├─────────────────────────────────────────────┤
│ 交付档位：Layout + 需要的硬件 + 业务插件       │
│ 认证：轻身份+审计（默认）/ 真实 RBAC（可选）    │
│ 数据：SQLite + 版本化迁移 + 多代备份           │
│ 诊断：测试连接 + 报文日志 + 插件加载报告        │
├─────────────────────────────────────────────┤
│ DeviceRuntime / Tag（保留，不重写）           │
│ Contracts + 贡献者模式（保留）                 │
└─────────────────────────────────────────────┘
```

### 10.3 一句话建议

**把接下来 3–4 周用在“装得上、写得进、升得级、查得到”上，而不是继续做 Dashboard 和插件抽象。** 这套框架的技术底子足够好，缺的是产品化最后 20%——而那 20% 决定外包项目是赚钱还是赔钱。

---

## 附录 A：关键证据索引

| 主题 | 文件 |
|------|------|
| 安装目录/权限 | `installer/setup.iss:25,32,47-49` |
| 数据库路径/备份/PRAGMA | `platform/infra/AP.Infra.Database/Configuration/DatabaseServiceExtensions.cs:41,50,65-70,92-155` |
| 日志路径 | `platform/infra/AP.Infra.Logging/Configuration/SerilogConfiguration.cs:40` |
| 配置写回 | `platform/shared/AP.Shared.Utilities/Helpers/ConfigurationHelper.cs:73,91-101` |
| 报表路径/状态 | `platform/infra/AP.Infra.Report/Configuration/ReportOptions.cs:42`；`Services/ReportService.cs:64-69` |
| PLC 写重试 | `platform/infra/AP.Infra.Resilience/Policies/PlcRetryPolicy.cs:15-20`；`MitsubishiPlcService.cs:303-332,386-389` |
| 扫码枪首开 | `SerialPortScannerService.cs:119-131`；`ScannerPlugin.cs:63-70` |
| 认证/匿名 | `IdentityService.cs:22-40`；`AnonymousIdentityService.cs:12-35`；`appsettings.json:41-46` |
| 授权 UI 化 | `PermissionBehavior.cs:60-88`；`SidebarViewModel.cs:52-85`；`UserRepository.cs:11-99` |
| 会话/登出 | `LayoutViewModel.cs:139-160`；`UserManagementPlugin.cs:47-52` |
| 重置密码清角色 | `UserListViewModel.cs:171-193`；`UserRepository.cs:78-80` |
| 种子/管理员 | `SecurityDbInitializer.cs:64-106` |
| 审计 | `AuditService.cs:13-20`；`AuditingPlcServiceDecorator.cs:46-61` |
| 双容器 | `Bootstrapper.cs:217,259-296,417` |
| 插件加载/依赖 | `PluginLoader.cs:38-42,116`；`PluginGraphValidator.cs:24-60`；`PluginLifecycleManager.cs:65-76` |
| 配置编辑器抹字段 | `AppConfigurationEditorViewModel.cs:51-60`；`AppConfigurationOptions.cs:8-15` |
| 启动失败转圈 | `Bootstrapper.cs:470-488`；`MainWindowViewModel.cs:18,53-56` |
| gRPC 交付负担 | `AP.Host.Desktop.csproj:27`；`Bootstrapper.cs:193-200,440-452,492-523` |
| 测试缺口 | `platform/tests/` 目录（无 Security/Host/UI/Recipe/Logging 测试） |

## 附录 B：未验证项

以下条目需要现场或专项实验确认，本文不把它们当作既成事实：

1. `Program Files` 安装后标准用户的真实失败表现（UAC 未触发，未做实机安装）。
2. DryIoc `Populate` 后 MS DI 与 DryIoc 两侧单例的实际相等性（代码结构可推断，未在本次运行中插桩验证）。
3. FreeSql `SyncStructure` 对改列/删列/类型变更的具体行为。
4. SQLite 连接池复用下 `PRAGMA synchronous/cache_size` 的生效范围；`busy_timeout` 缺失的真实并发表现。
5. 西门子/三菱/欧姆龙驱动在真实 PLC 上的批量键映射、字节序、X/Y 编址、心跳语义。
6. 安装/升级在客户组策略、杀毒软件、域账号下的行为。
7. 485 个测试的真实行覆盖率（未采集；测试数与覆盖率不是一回事）。
8. 报表调度在跨天停机、时区/DST、系统休眠后的补档行为。
9. 长期运行 30 天以上的内存/句柄/数据库增长。
10. 代码签名、SmartScreen、Windows Defender 对安装包的影响。

## 附录 C：新项目交付检查清单

> 每个外包项目开工时复制一份，逐项打勾；这是比“再读一遍架构文档”更有效的防翻车手段。

**项目定义**

- [ ] 明确是单机还是多机；如果是多机，先评估 gRPC 解冻，不要直接开工。
- [ ] 明确认证档位：免登录 Demo / 轻身份+审计 / 真实 RBAC。
- [ ] 明确设备清单：品牌、型号、协议、IP/端口、点表规模、扫码枪型号。
- [ ] 明确数据需求：是否需要历史趋势、报警记录、班次/生产报表。
- [ ] 明确交付边界：哪些模块是骨架、哪些必须完整（配方/报表/审计）。

**工程**

- [ ] 从模板创建项目，只保留需要的插件（Layout + 硬件 + 业务）。
- [ ] DataRoot 指向 `%ProgramData%` 或 `%LocalAppData%`，不写安装目录。
- [ ] PLC 写路径确认不重试；关键写有回读/审计。
- [ ] 点表有版本/备份；升级脚本保留 `tags.json`。
- [ ] 配置保存不会抹掉 `LayoutMode` / `DefaultNavigationTarget` / 白名单。
- [ ] 扫码枪开机失败可自动重连。
- [ ] 日志、崩溃日志、数据库、报表目录可写且有清理策略。

**验证**

- [ ] 标准用户从安装包安装 → 首次启动 → 建库/登录/采集/写 PLC/出报表。
- [ ] 旧版升级 → 配置/点表/数据库/用户数据保留。
- [ ] 断电/强杀 → 重启后数据库可打开、备份可恢复。
- [ ] 登出 → 换低权限用户登录 → 菜单/页面/命令全部按新身份。
- [ ] 拔出 PLC 网线/扫码枪 → 恢复后自动重连，无需重启。
- [ ] 连续运行 24h → 内存/句柄/日志大小可接受。
- [ ] 安装包在干净 Windows 虚拟机 + 目标运行时上可安装/卸载。

**交付**

- [ ] 版本号单一来源（构建注入安装包）。
- [ ] 安装包签名（至少自签名并告知客户）。
- [ ] 交付《现场部署说明》《备份恢复说明》《故障排查说明》。
- [ ] 明确“不支持的能力”（多机、PostgreSQL、报警/历史等），避免销售误承诺。

---

**文档版本**：v1.0（2026-11-18）
**评估分支**：`review/framework-evaluation`
**建议下一步**：先评审第 8 节 P0 清单，确定路线 A/B 与 gRPC 去留，再开实施分支。
