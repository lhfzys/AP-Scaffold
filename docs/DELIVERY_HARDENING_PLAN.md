# AP-Scaffold 交付加固计划（Delivery Hardening Plan）

> 分支：`fix/delivery-hardening`（从 `review/framework-evaluation` 创建，基线 `24e8c42`）
> 创建日期：2026-11-18
> 依据：[FRAMEWORK_EVALUATION.md](FRAMEWORK_EVALUATION.md) 第 5、8、10 节
> 执行原则：一次只做一个 Task、单独提交、可回滚；每个 Task 完成后更新本文件的“状态”和“验证记录”。

---

## 1. 目标与范围

### 1.1 本阶段目标

把框架从“能演示”推进到“能交付第一个单机外包项目”：

1. 标准用户在 `Program Files` 安装后，数据库/日志/配置/报表都能正常写入；
2. PLC 写操作不会被通用重试策略重复执行；
3. 后续再补 .NET 10、认证会话、数据库迁移、CI 等 P0/P1 项。

### 1.2 本阶段不做

- 不做完整 RBAC / 服务层授权重构（Auth 方向已定为“轻身份 + 审计”，放到 DH-4）；
- 不实现插件热卸载、能力沙箱、多设备路由、报警/历史（见后续阶段）；
- 不重写 Prism、不尝试自包含发布；
- 不在本阶段删除 gRPC 代码（DH-6 再做物理移出）。

### 1.3 Auth 方向决策

**采用“轻身份 + 审计”（评估报告路线 A）**，不把 RBAC 作为默认卖点：

- 保留登录/操作员身份，用于审计归属与防误操作；
- 默认交付形态不启用多角色/权限管理；
- 关键操作（PLC 写、配方切换、配置/点表修改、报表导出）必须写审计；
- 空闲锁屏/换班登录、审计索引/保留放到 DH-4；
- 只有在客户明确要求多角色/合规时才升级到路线 B（真实 RBAC）。

---

## 2. 任务总览

| ID | 任务 | 优先级 | 状态 | 预计 |
|----|------|--------|------|------|
| DH-1 | 统一 DataRoot + 安装器权限 + 启动可写自检 | P0 | 进行中 | 3d |
| DH-2 | PLC 写路径去重试 + 批量写不嵌套 | P0 | 待开始 | 2d |
| DH-3 | .NET 10 迁移 + 依赖漏洞清零 | P0 | 待开始 | 4d |
| DH-4 | 轻身份 + 审计闭环（会话/锁屏/审计索引） | P0 | 待开始 | 4d |
| DH-5 | 数据库版本化迁移 + 原子备份 + tags.json 升级保留 | P0 | 待开始 | 5d |
| DH-6 | CI 最小流水线 + 安装冒烟 + gRPC 移出交付链 | P0/P1 | 待开始 | 4d |
| DH-7 | 插件 manifest/启用禁用/交付档位 | P1 | 待开始 | 4d |
| DH-8 | 单容器 + 插件初始化超时 + 加载报告 | P1 | 待开始 | 3d |
| DH-9 | 配置保存 merge + 备份/恢复 | P1 | 待开始 | 2d |
| DH-10 | 扫码枪首开重连 + 有界 Channel | P1 | 待开始 | 1d |
| DH-11 | 报表/配方闭环 | P1 | 待开始 | 4d |
| DH-12 | 现场诊断：测试连接/报文日志/点表导入 | P1 | 待开始 | 4d |
| DH-13 | 新项目模板 + 示例业务插件 | P1 | 待开始 | 3d |

> 状态取值：待开始 / 进行中 / 已完成 / 阻塞。完成时必须填写验证记录。

---

## 3. DH-1：统一 DataRoot（本次实施）

### 3.1 问题

- 安装脚本把程序装到 `{autopf}`（`installer/setup.iss:25`），但 SQLite、日志、报表、配置写回都使用安装目录或相对当前目录的路径。
- `Program Files` 默认 ACL 对标准用户只读；标准用户首次运行可能无法建库/写日志/保存配置。
- 数据库路径按 CWD 解析、备份按 `AppContext.BaseDirectory` 解析，基准不一致。

### 3.2 目标目录布局

默认数据根：

```
%ProgramData%\AP-Scaffold\          # 机器级；安装器创建并授予 Users Modify
├── local_data.db                   # SQLite
├── local_data.db-wal / -shm
├── Configuration\                  # 可写覆盖层
│   ├── appsettings.json
│   └── appsettings.Standalone.json
├── logs\
│   ├── log-yyyyMMdd.txt
│   └── crash-yyyyMMdd.log
├── reports\
└── tags.json                       # 现场点表（首次从安装目录复制）
```

数据根优先级：

1. 环境变量 `AP_SCAFFOLD_DATA_ROOT`（绝对路径，便于现场/POC 指定）；
2. 基础配置 `AppRuntime:DataRoot`（绝对路径；相对路径按程序目录解析）；
3. `%ProgramData%\AP-Scaffold`；
4. 若 3 不可写：`%LocalAppData%\AP-Scaffold`（并记录 Warning）；
5. 显式指定的根不可写时**直接报错**，不静默回退。

配置采用“只读默认 + 可写覆盖”两层：

```
{AppDirectory}\Configuration\appsettings.json            # 只读默认（安装包）
{AppDirectory}\Configuration\appsettings.{Role}.json     # 只读默认（安装包）
{DataRoot}\Configuration\appsettings.json                # 可写覆盖（保存时生成）
{DataRoot}\Configuration\appsettings.{Role}.json         # 可写覆盖（保存时生成）
```

覆盖层在后加载，按键覆盖默认值；保存只写覆盖层。这样升级可以继续更新默认配置，而现场修改不会丢。

### 3.3 代码设计

新增 `platform/shared/AP.Shared.Utilities/Paths/AppPaths.cs`：

- `DefaultDataFolderName = "AP-Scaffold"`；
- `ResolveDataRoot(IConfiguration baseConfig)`：按 3.2 优先级解析，创建目录并做可写探测；显式路径失败抛 `IOException`；
- `Initialize(string root)` / `ResetForTests()`；
- 只读属性：`DataRoot`、`ConfigDirectory`、`LogDirectory`、`ReportDirectory`、`DatabasePath`、`TagTablePath`；
- `ResolvePath(string path)`：相对路径 → DataRoot 下；绝对路径原样；
- `PrepareDataFiles()`：首次运行复制默认 `tags.json`；迁移旧版数据库（db/-wal/-shm）到 DataRoot；不覆盖已有文件；
- 未初始化时回退 `AppContext.BaseDirectory`，保证既有单元测试与工具场景可用。

路径消费点改造：

| 消费点 | 现状 | 改为 |
|--------|------|------|
| `Bootstrapper` 配置加载 | BaseDirectory/Configuration 单层 | 基础层 + DataRoot 覆盖层；注入 `AppRuntime:*` 路径键 |
| `SerilogConfiguration` | 相对 `logs/log-.txt` | `AppPaths.LogDirectory`（可被 `AppRuntime:LogDirectory` 覆盖） |
| `LogCleanupHelper` 调用 | 字面量 `"logs"` | `AppPaths.LogDirectory` |
| `GlobalExceptionHandler` | BaseDirectory/logs | `AppPaths.LogDirectory`（写入时解析，兼容早期启动） |
| `DatabaseServiceExtensions` | 相对连接串 + BaseDirectory 备份 | `AppRuntime:DatabasePath` 绝对路径；备份按同一绝对路径；补 `busy_timeout=5000` |
| `ReportStorage` / `ReportCleanupService` | 相对 `reports` | `AppPaths.ResolvePath(RootPath)` |
| `ConfigurationHelper` | BaseDirectory/Configuration | DataRoot/Configuration；覆盖层文件不存在时创建 |
| `SettingsService` | BaseDirectory/Configuration | DataRoot/Configuration；备份/恢复同目录 |
| `TagTable` / `TagTableFileStore` | BaseDirectory/Configuration/tags.json | `AppPaths.TagTablePath` |
| `installer/setup.iss` | 无数据目录 | 创建 `{commonappdata}\AP-Scaffold` 并 `users-modify`；快捷方式 `WorkingDir={app}` |

### 3.4 启动流程

`Bootstrapper.RegisterTypes` 开头：

1. 用 `BaseDirectory/Configuration` 构建**基础配置**（appsettings.json + 角色文件）；
2. `var dataRoot = AppPaths.ResolveDataRoot(baseConfig)`；
3. `AppPaths.Initialize(dataRoot)`；`AppPaths.PrepareDataFiles()`；
4. 重建最终配置：基础层 + DataRoot 覆盖层 + 环境变量 + `AppRuntime:*` 内存键；
5. 再做 `AddPlatformLogging` / `AddPlatformDatabase` / ...；
6. 若数据根不可写，抛出带路径与建议的异常；`App.OnStartup` 兜底显示错误并退出。

`AppRuntime:*` 键：

- `AppRuntime:DataRoot`
- `AppRuntime:DatabasePath`
- `AppRuntime:LogDirectory`
- `AppRuntime:ReportRoot`（可选）
- `AppRuntime:WritableConfigDirectory`
- `AppRuntime:RoleConfigFile`

### 3.5 安装器

- `[Dirs]` 创建 `{commonappdata}\AP-Scaffold`、`logs`、`reports`、`Configuration`，`Permissions: users-modify`；
- 快捷方式增加 `WorkingDir: "{app}"`；
- `tags.json` 仍随安装包作为默认，但运行期读取 DataRoot 副本；升级不覆盖现场副本；
- 卸载默认保留 DataRoot（现场数据），在 README 说明。

### 3.6 验收标准

- [ ] 标准用户从 `Program Files` 启动：DB/日志/配置/报表四类写入成功；
- [ ] `DataRoot` 不可写时启动中止，提示包含路径与“检查权限/设置 AP_SCAFFOLD_DATA_ROOT”；
- [ ] 修改系统配置后，写入 `%ProgramData%\AP-Scaffold\Configuration`，不改安装目录；
- [ ] 升级后 `%ProgramData%\AP-Scaffold\tags.json` 保留；
- [ ] 旧版 `local_data.db`/`tags.json` 首次启动迁移到 DataRoot（目标不存在时）；
- [ ] 现有 485 个测试通过；新增 `AppPaths` 测试通过。

### 3.7 风险与回滚

- 静态 `AppPaths` 是进程级状态：测试用 `ResetForTests()` 隔离；生产只初始化一次。
- 迁移是复制而非移动：旧文件保留，回滚只需删除 DataRoot（或改 `AP_SCAFFOLD_DATA_ROOT` 指回旧目录）。
- DataRoot 在 `ProgramData` 下对本地用户可读；数据库仍明文。加密/ACL 细化放到 DH-4/DH-5。

---

## 4. DH-2：PLC 写路径去重试（本次实施）

### 4.1 问题

`PlcRetryPolicy` 对任意异常做 5 次重试（`PlcRetryPolicy.cs:15-20`），而三个驱动的 `WriteAsync`/`WriteBatchAsync` 都在 `_pipeline.ExecuteAsync` 内（三菱 `:303-332,386-389`；西门子 `:271-298,363-377`；欧姆龙 `:275-286,376-390`）。写超时但 PLC 已执行时，会重复写 6 次。

### 4.2 设计

- 读/连接/探测继续使用 `PLC-Retry`；
- 写操作**不经过重试管道**：
  - `WriteAsync` 直接调用客户端写，失败立即抛出；
  - `WriteBatchAsync` 在管道外编排，禁止管道内再调 `WriteAsync`；
  - 保留地址预检、客户端快照、`CancellationToken` 检查、错误信息；
- 在三个驱动写入方法上加注释，说明“写不可重试”的现场原因；
- 审计装饰器仍记录成功/失败（一次操作一条审计，不按重试次数记录）；
- 回读校验不在本 Task 做：部分地址是命令/脉冲，读回不具可比性；先保证“不重复写”，回读作为 DH-12 诊断项按项目配置启用。

### 4.3 验收标准

- [ ] 三个 `WriteAsync` 与 `WriteBatchAsync` 内不再出现 `_pipeline.ExecuteAsync`；
- [ ] 批量写不再嵌套单写管道；
- [ ] 现有 485 个测试通过；
- [ ] 代码审查确认：写失败只尝试一次；审计仍记录失败；
- [ ] 真实 PLC 的“写成功但响应超时”场景列入现场验证清单（本次无真机）。

### 4.4 风险与回滚

- 去掉重试后，瞬时网络抖动会导致写失败；这是有意取舍：**宁可失败让业务重试/提示，不可重复执行**。
- 回滚：单独 revert 本 Task 提交即可；读写管道未改动。

---

## 5. 后续任务要点（不在本次实施）

### DH-3 .NET 10 / 依赖

- `net10.0` / `net10.0-windows`；`Microsoft.Extensions.*` 10.x；安装器只检测 .NET 10 桌面运行时；漏洞审计 0 High。

### DH-4 轻身份 + 审计

- 保留操作员登录/审计，取消或隐藏角色/权限管理默认入口；
- 登录/登出发布事件，Shell 重建 Sidebar/Region；
- 重置密码保留角色、随机一次性口令、失败锁定、密码策略；
- 审计索引/保留、关键操作失败即拒绝、权限拒绝记录；
- 空闲锁屏/换班登录。

### DH-5 数据库迁移/备份

- `PRAGMA user_version` + 有序迁移；升级前备份；失败阻断；
- `VACUUM INTO` 多代备份 + `quick_check` + 恢复入口；
- `busy_timeout`、事务、单写者队列。

### DH-6 CI / 安装冒烟 / gRPC

- build + tests + publish + 标准用户安装启动 + 漏洞审计；
- 物理移出 gRPC/Server/Client，Host 删除 `Microsoft.AspNetCore.App`。

### DH-7 ~ DH-13

见评估报告第 8 节；每项单独开 Task、单独提交。

---

## 6. 执行记录

| 日期 | Task | Commit | 验证 |
|------|------|--------|------|
| 2026-11-18 | 计划文档 | 待提交 | 文档评审 |
| 2026-11-18 | DH-1 | 待提交 | 构建 + 测试 + 路径单测 |
| 2026-11-18 | DH-2 | 待提交 | 构建 + 测试 + 写路径审查 |

> 每次提交前必须：`dotnet build AP-Automation.Platform.slnx -c Release` 0 错误 + 三个测试项目全绿。

---

**文档版本**：v1.0（2026-11-18）
**下一步**：完成 DH-1、DH-2 后，评审 DH-3/DH-4 的排期。
