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
| DH-1 | 统一 DataRoot + 安装器权限 + 启动可写自检 | P0 | 已完成 | 3d |
| DH-2 | PLC 写路径去重试 + 批量写不嵌套 | P0 | 已完成 | 2d |
| DH-3 | .NET 10 迁移 + 依赖漏洞清零 | P0 | 已完成（net10 全仓；漏洞 0 High） | 4d |
| DH-4 | 轻身份 + 审计闭环（A 模式：免登录 + 审计） | P0 | 已完成（A 模式） | 4d |
| DH-5 | 数据库版本化迁移 + 原子备份 + tags.json 升级保留 | P0 | 已完成（核心迁移/备份/恢复脚本；事务/单写者待评估） | 5d |
| DH-6 | CI 最小流水线 + 安装冒烟 + gRPC 移出交付链 | P0/P1 | 已完成（CI 绿，已合并 main） | 4d |
| DH-7 | 插件 manifest/启用禁用/交付档位 | P1 | 已完成 | 4d |
| DH-8 | 单容器 + 插件初始化超时 + 加载报告 | P1 | 已完成 | 3d |
| DH-9 | 配置保存 merge + 备份/恢复 | P1 | 已完成 | 2d |
| DH-10 | 扫码枪首开重连 + 有界 Channel | P1 | 已完成 | 1d |
| DH-11 | 报表/配方闭环 | P1 | 待开始 | 4d |
| DH-12 | 现场诊断：测试连接/报文日志/点表导入 | P1 | 进行中（测试连接完成） | 4d |
| DH-13 | 新项目模板 + 示例业务插件 | P1 | 已完成 | 3d |

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

- [x] 发布目录启动写入 DataRoot 成功；正式 `Program Files` 标准用户安装已在 DH-6 实测；
- [x] 显式 DataRoot 可写探测/异常路径已实现（未模拟 ACL 拒绝）；
- [x] 修改系统配置写入 `%ProgramData%\AP-Scaffold\Configuration` 的路径已有单元测试；
- [x] 升级/重装后 `tags.json` 保留：正式安装、卸载、重装后 SHA256 不变；
- [ ] 旧版 `local_data.db` 迁移：代码已实现“目标不存在时复制”，真实旧库迁移实测待补；
- [x] 现有测试通过；新增 `AppPaths` / 配置覆盖层测试通过。

> **验证记录（2026-11-18，commit `43ace41`）**
> - Release 构建 0 错误（9 个既有 NU1701）；
> - 测试 130 + 56 + 303 = **489 通过**（新增 `AppPathsTests` 4 例，含 DataRoot 初始化 + 配置覆盖层写入）；
> - 发布目录实际启动：自动创建 `C:\ProgramData\AP-Scaffold`，数据库/日志/`Configuration\tags.json` 均落在数据根；进程正常进入主窗口；
> - 安装包（Inno Setup 6.7.3）编译成功，`[Dirs]` 的 `users-modify` 与快捷方式 `WorkingDir` 语法通过；
> - 正式安装（2026-11-18，DH-6）：安装到 `C:\Program Files\自动化监控系统`，标准用户启动成功；数据库/日志/点表写入 `C:\ProgramData\AP-Scaffold`；`Program Files` 配置目录对标准用户只读，验证了“程序与数据分离”。详见 DH-6 记录。

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

- [x] 三个 `WriteAsync` 与 `WriteBatchAsync` 内不再出现 `_pipeline.ExecuteAsync`；
- [x] 批量写不再嵌套单写管道；
- [x] 现有测试通过；
- [x] 代码审查确认：写失败只尝试一次；审计仍记录失败；
- [ ] 真实 PLC 的“写成功但响应超时”场景列入现场验证清单（本次无真机）。

> **验证记录（2026-11-18，commit `07e3174`）**
> - 三菱/西门子/欧姆龙写方法已改为直接调用客户端；批量写不再套管道、不再在管道内调 `WriteAsync`；
> - `PlcRetryPolicy` 注释明确“仅连接/读取/探测，写操作禁止使用”；
> - 测试 489 通过；真实 PLC 超时/重复写验证留待现场（无真机，不伪称已验证）。

### 4.4 风险与回滚

- 去掉重试后，瞬时网络抖动会导致写失败；这是有意取舍：**宁可失败让业务重试/提示，不可重复执行**。
- 回滚：单独 revert 本 Task 提交即可；读写管道未改动。

---

## 5. 后续任务要点（不在本次实施）

### DH-3 .NET 10 / 依赖

#### 完成记录（2026-11-18）

- 目标框架：全部主解决方案项目 `net10.0` / `net10.0-windows`；`Microsoft.Extensions.*` 10.0.15、Serilog.Extensions.* 10.0.0、Serilog.Settings.Configuration 10.0.1。
- 漏洞：net8 的 `System.Text.Json 8.0.0` 两个 High 随框架消失；`scripts/check-vulnerabilities.ps1` 审计 0 High，CI 已改为阻断。
- 安装器：只检测 .NET 10 桌面运行时；修复 10.x 版本前缀 `Copy(...,1,3)='10.'`。
- 验证：构建 0 错误；**502 测试 net10 全绿**；发布目录启动、非提权安装冒烟通过；安装包 13.2 MB。
- 说明：包级 STJ 8.0.6 曾让审计归零，但 framework-dependent 发布不带该 DLL、运行时仍用共享框架，因此最终采用全仓 .NET 10 迁移，而不是保留无效补丁。

### DH-4 轻身份 + 审计

**最终形态（2026-11-18 用户确认）：A 模式——免登录 + 审计。**

- 默认 `Security:Enabled=false`：不弹登录、不启用空闲锁屏；
- 审计保持开启（`Security:Audit:Enabled=true`），操作人记为 `anonymous`/`system`；
- 免登录白名单加入 `AuditLogListView`，现场可直接查看审计；
- 角色/权限管理默认隐藏；
- 可选登录（`Security:Enabled=true`）保留给明确要求访问控制的项目；当前不实现“真锁屏”，需要时单独立项（覆盖层密码解锁，不重启）。

#### DH-4 完成记录（2026-11-18）

1. **密码策略**：`PasswordPolicy`（≥8 位 + 字母 + 数字），改密/建用户/服务端改密统一校验。
2. **随机一次性重置**：管理员重置密码改为生成 12 位临时密码，只显示一次；`MustChangePassword=true`。
3. **修复重置清角色**：重置时带回原 `Roles`，不再把用户的 `UserRole` 清空。
4. **失败锁定**：`sys_users` 新增 `FailedLoginCount` / `LockoutUntil`；默认连续 5 次失败锁定 5 分钟，成功登录清零。
5. **角色管理默认隐藏**：`Security:RoleManagement:Enabled=false`（默认），轻身份模式不再出现角色管理入口；需要完整 RBAC 时显式打开。
6. **登录/登出会话重建（可选安全模式）**：登出后以 `--restart` 启动新进程，重新执行登录与插件初始化，避免旧菜单/旧页面/旧权限行为残留。
7. **审计索引**：`sys_audit_logs` 增加 `Timestamp` / `UserName` 索引，查询不再全表扫。
8. **A 模式审计可见**：`Security:Enabled=false` 且 `Security:Audit:Enabled=true` 时仍注册审计视图；`NavigationWhenSecurityDisabled` 加入 `AuditLogListView`。默认交付即可查审计，但操作人匿名。
9. **测试**：新增密码策略/生成器测试、`IdentityServiceTests`、`UserRepositoryTests`；全量 **502** 测试通过。

**已取消**：DH-4b 空闲锁屏（用户确认 A 模式不需要登录/锁屏；相关代码与 `Security:IdleLockMinutes` 配置已移除，避免“无登录却尝试锁屏”的半成品）。

未完成（留到 DH-4c / DH-5）：

- 审计保留策略与归档（当前只建索引，不自动删）；
- 关键操作“审计失败即拒绝”的统一管道（当前审计失败仍不阻断 PLC 写，避免数据库故障导致停产）；
- 权限拒绝审计（依赖服务层授权入口，A 模式下通常不需要）。

### DH-5 数据库迁移/备份

#### 完成记录（2026-11-18）

1. **版本化迁移**：新增 `IDatabaseSchemaContributor` + `DatabaseSchemaMigrator`，维护 `schema_migrations`（name+version 复合主键）与 `PRAGMA user_version`；security/recipe/report 各注册 v1 contributor。`SecurityDbInitializer` / `RecipeDbInitializer` / `ReportDatabaseInitializer` / `AuditService` 的启动期 `SyncStructure` 已移除，初始化器只做种子。
2. **启动顺序**：`Bootstrapper.InitializeShell` 在登录/插件初始化前执行“备份 → 迁移”；备份或迁移失败直接中止启动，不再各模块 catch 后继续。
3. **备份**：`DatabaseBackupService` 用 `VACUUM INTO` 生成 `backups/local_data-yyyyMMdd-HHmmss.db`，`quick_check` 校验；默认保留 10 代（`Database:Backup:RetentionCount`）；空库跳过，避免首次启动生成 4KB 空备份。
4. **恢复入口**：新增 `scripts/restore-db.ps1`（停应用、恢复前快照、覆盖 DB、删除 WAL/SHM）与 `docs/DATABASE_BACKUP_RESTORE.md`。
5. **验证**：
   - 首次启动：0→1 迁移，三模块各执行一次；
   - 第二次启动：无重复迁移，生成 69KB 备份并通过 quick_check；
   - 正式安装升级：旧 ProgramData DB（无 schema_migrations）启动前生成 32KB 备份，随后补记 v1，应用正常；
   - 测试新增 `DatabaseSchemaMigratorTests`（幂等/版本递增）与 `DatabaseBackupServiceTests`（备份/校验/保留 2 代）；全量 **505** 测试通过。
6. **tags.json**：已在 DH-1 落到 DataRoot，本次未改动；重装后 SHA256 不变（DH-6 验证）。

**未完成**：写事务/单写者队列（当前依赖 `busy_timeout=5000` + 单进程写）；恢复 UI（当前为脚本）；定时/远程备份（按现场需求再加）。

### DH-6 CI / 安装冒烟 / gRPC

- build + tests + publish + 标准用户安装启动 + 漏洞审计；
- 物理移出 gRPC/Server/Client，Host 删除 `Microsoft.AspNetCore.App`。

#### DH-6 完成记录（2026-11-18）

1. **gRPC 物理移出**：
   - `AP.Contracts.Communication`、`AP.Infra.Grpc` 从 `AP-Automation.Platform.slnx` 移除（主解决方案 34 → 32 个项目）；
   - Host 删除 `FrameworkReference Microsoft.AspNetCore.App`、两个 gRPC ProjectReference、`ServerBootstrap`/`ClientBootstrap`、Kestrel/gRPC 启动/关闭分支；
   - `RoleResolver` 显式拒绝 Server/Client，避免“以为在跑服务端、实际跑单机 UI”；
   - `AP.Infra.Resilience` 删除 Grpc-CircuitBreaker 管道/选项/测试；
   - 源文件与 proto 保留，`FROZEN.md` 说明冻结；发布目录 0 个 Grpc/Protobuf/AspNetCore 文件。
2. **安装包运行时**：`setup.iss` 只检测 .NET 8 桌面运行时，不再要求 ASP.NET Core；发布目录 239 文件/45.3MB，安装包 13.18MB。
3. **CI**：新增 `.github/workflows/ci.yml`（windows-latest）：build → 三个测试项目 → publish → 校验无 gRPC → 安装 Inno Setup → 编译安装包 → `scripts/install-smoke.ps1` 非提权安装/启动/数据根/卸载 → 上传安装包；NuGet 漏洞报告暂为 informational（STJ 8.0.0 已知，待 DH-3a 处理）。
4. **安装冒烟脚本**：新增 `scripts/install-smoke.ps1`，本机实际运行通过（`INSTALL_SMOKE_OK`）：安装到 `%LocalAppData%\AP-Scaffold-CI-Smoke`、启动、验证 DB/日志/点表、卸载、数据根保留。
5. **正式安装**：DH-6 之前已用 UAC 验证 x64 Program Files 安装、标准用户启动、ProgramData 写入；本次 gRPC 移出后重新编译发布/安装包。
6. **升级残留清理（实测发现）**：gRPC-free 包首次覆盖安装后，Program Files 仍残留 10 个旧 gRPC/Protobuf 文件（Inno 只覆盖新文件，不删除已移除文件）。已在 `setup.iss` 增加 `[InstallDelete]`：清理 `AP.Infra.Grpc.*`、`AP.Contracts.Communication.*`、`Grpc.*`、`Google.Protobuf.*`，并重建 `plugins\AP.Plugin.*` 目录。重新覆盖安装后残留 = 0、插件 14 个，应用正常启动。
7. **CI 实测与合并**：CI 首次失败暴露 `Backup/` 被 `.gitignore` 忽略、源码未入库；改为 `DatabaseBackup/` 后修复。第二次失败为 Chocolatey 无 Inno 6.7.3 包，固定 6.7.1 后通过。最终 CI：<https://github.com/lhfzys/AP-Scaffold/actions/runs/44264640846>；`fix/delivery-hardening` 快进合并 `main`（`0523fcf`）。

**未完成**：NuGet 漏洞门禁待 STJ 补丁后再设为阻断；CI 目前只跑 Windows 构建/测试/安装冒烟，无真机 PLC 测试。

### DH-9 配置保存 merge / 备份恢复

#### 完成记录（2026-11-19）

- **保存不再整节替换**：`ConfigurationHelper.UpdateAppSetting` 改为递归合并 JSON 对象；已有未知键、同级 `LayoutMode`/`DefaultNavigationTarget`/`NavigationWhenSecurityDisabled` 不再被保存“公司名/软件名”抹掉；数组整体替换而不是按索引合并。
- **编辑器字段补齐**：`AppConfigurationOptions` 与 `AppConfigurationEditorViewModel` 补齐 `LayoutMode`、`DefaultNavigationTarget`、`NavigationWhenSecurityDisabled`，加载后原样回写。
- **备份/恢复**：配置备份时间戳改为毫秒级，避免同秒覆盖；恢复限制在配置目录内，配置中心底部新增“配置备份 + 恢复备份”入口。
- **验证**：新增 `ConfigurationHelperTests`（未知键保留/数组替换）、`AppConfigurationEditorTests`（编辑器字段保留）、`SettingsServiceTests`（保存 merge + 备份恢复）；全量 **506** 测试通过。

### DH-10 扫码枪首开重连 / 有界 Channel

#### 完成记录（2026-11-19）

- **首开失败不再终止**：`OpenAsync` 打开失败时状态转 `Reconnecting`，随后启动 `ConnectionSupervisor`；监督器按 5 秒周期重试，插上/上电后自动恢复，无需重启应用。
- **有界 Channel**：数据通道由无界改为有界，容量 `Plugins:Configuration:AP.Plugin.Scanner:BarcodeChannelCapacity`（默认 1000）；满时 `DropWrite` 并告警，不阻塞串口事件线程。
- **ReadTimeout**：新增 `ReadTimeoutMilliseconds`（默认 1000），避免无换行数据长期占用 `DataReceived` 线程；超时不记错误。
- **配置**：`appsettings.Standalone.json` 增加上述两个键；`SerialPortOptions` 提供默认值。
- **验证**：新增首开失败不抛出/进入监督器测试、选项默认值测试；全量 **508** 测试通过。

### DH-13 新项目模板 / 示例业务插件

#### 完成记录（2026-11-19）

- **示例业务插件**：新增 `AP.Plugin.Sample`（导航页 + 设置页 + `IReportDataProvider` + 视图/VM），加入主解决方案。
- **dotnet new 模板**：`templates/ap-plugin`，短名称 `ap-plugin`，生成后包含 `Plugin.cs`、View/ViewModel、设置贡献者、报表提供者和 `Tests/*.Tests.csproj`；`sourceName` 自动替换 `AP.Plugin.Sample`。
- **验证**：模板安装/生成/独立构建/测试项目运行通过（3 个测试）；生成项目与模板文件均清理；示例插件测试（配置读取/命令/设置回环/报表/导航）通过。
- **测试**：全量 **512** 通过（Core 130 / Shared 65 / Infra 317）。
- 使用说明：`dotnet new install .\templates\ap-plugin` → `dotnet new ap-plugin -n AP.Plugin.MyBusiness -o platform/plugins/business/AP.Plugin.MyBusiness`，再加入 slnx。

### DH-7 插件 manifest / 启用禁用 / 交付档位

#### 完成记录（2026-11-19）

- **插件清单**：新增 `Configuration/plugins.catalog.json`，包含 `Full` / `Minimal` 档位与每个插件的 `enabled` 状态。
- **加载过滤**：`PluginLoader.DiscoverPlugins` 读取档位和 `Plugins:Disabled`；未知档位回退 Full；被档位/禁用/清单排除的插件不加载。
- **交付裁剪**：`scripts/apply-plugin-profile.ps1` 按档位删除 publish 中未包含的插件目录；`build-installer.bat [Full|Minimal]` 支持生成裁剪安装包。
- **验证**：`Minimal` 运行只有 Login/Layout/SystemSettings/Siemens/Sample 五个插件；裁剪脚本测试通过；清单单元测试通过；全量 **515** 测试通过。

### DH-8 单容器 / 超时 / 加载报告

#### 完成记录（2026-11-19）

- **单实例**：去掉临时平台容器和“最终再实例化”第二阶段；插件只构造一次，`ConfigureServices` 注册进入最终 `ServiceCollection`，实例直接注册进 DryIoc。插件构造函数约定只依赖 `ILogger`，业务服务在 `InitializeAsync(serviceProvider)` 中解析。
- **超时**：`PluginLifecycleManager` 为初始化/启动增加单插件 15 秒超时；超时标记 `Failed`，不阻塞后续插件；Required 插件仍触发启动中止。支持构造参数覆盖（测试用短超时）。
- **加载报告**：新增 `PluginStatusInfo` / `GetPluginStatuses()`；启动日志逐插件打印状态；Layout 新增“插件状态”页（`PluginStatusView`，需 `system.settings` 权限），展示名称/Id/状态/Required/优先级。
- **验证**：全量 **513** 测试通过（含挂起插件超时测试）；单容器真机启动正常，插件状态页注册；安装冒烟通过。

### DH-12 现场诊断（第一阶段：测试连接）

#### 完成记录（2026-11-19）

- **测试连接按钮**：PLC 配置页新增“测试连接”，使用编辑中的临时配置创建独立驱动，不触碰运行中的 `ActivePlcService`。
- **超时/错误**：连接、状态等待、心跳读取均受 `Timeout` 约束；连接失败/超时/读取失败返回明确中文消息，不保存配置。
- **心跳校验**：连接成功后读取 `HeartbeatAddress`（西门子/三菱 Bool，欧姆龙 Int16），只有连接和读取都成功才显示“连接成功”。
- **验证**：单元测试覆盖成功/失败/挂起超时/参数非法；西门子 S7-1200 虚拟服务器（127.0.0.1:102、DB1.0.0=True）实测 UI 按钮返回“连接成功，读取 DB1.0.0 = True”；改端口 65530 返回“连接超时（1000 ms）”；全量 **521** 测试通过。
- **待办**：报文日志、点表 CSV 导入。

### DH-7 ~ DH-13

见评估报告第 8 节；每项单独开 Task、单独提交。

---

## 6. 执行记录

| 日期 | Task | Commit | 验证 |
|------|------|--------|------|
| 2026-11-18 | 计划文档 | `14f4e7e` | 文档评审 |
| 2026-11-18 | DH-1 | `43ace41` | 构建 0 错误；489 测试通过；发布目录启动写入 `C:\ProgramData\AP-Scaffold`；安装包编译通过 |
| 2026-11-18 | DH-2 | `07e3174` | 构建 0 错误；489 测试通过；写路径静态审查通过；真机待现场 |
| 2026-11-18 | DH-6（非提权） | `43ace41` 后验证 | 非提权安装/启动/数据根/卸载冒烟通过 |
| 2026-11-18 | DH-6（正式） | `acb4b9d` | x64 安装到 Program Files；标准用户启动；ProgramData 可写、Program Files 只读；数据保留 |
| 2026-11-18 | DH-6（CI/gRPC） | `0523fcf` | 主解决方案 32 项目；publish 无 Grpc/Aspnet；install-smoke 通过；502 测试通过；升级残留 0；CI 绿 |
| 2026-11-18 | DH-4 | `cfdc58c` | 502 测试通过；安全模式启动完成安全库初始化；登录 UI 自动化未做 |
| 2026-11-18 | DH-4b（取消） | `ec38e0e`（回退） | 用户确认 A 模式：免登录 + 审计；空闲锁屏代码/配置移除，审计视图在免登录白名单可见 |
| 2026-11-18 | A 模式收口 | `6e58c0a` | 免登录启动（无登录框）；日志显示“审计日志插件已加载”；Sidebar/快捷入口出现“审计日志”；502 测试通过 |
| 2026-11-18 | DH-5 | `fbcf354` | 首次/二次启动迁移验证；正式安装旧库升级备份 32KB + quick_check；505 测试通过 |
| 2026-11-18 | DH-3 | `d72acbe` | net10 全仓构建；502 测试通过；漏洞 0 High；安装冒烟通过；CI 绿 |
| 2026-11-19 | DH-9 | `cf85718` | 配置保存 merge；备份/恢复入口；506 测试通过；CI 绿 |
| 2026-11-19 | DH-10 | `d746733` | 首开失败进入监督器；有界 Channel/ReadTimeout；508 测试通过；CI 绿 |
| 2026-11-19 | DH-13 | `37047bf` + `5b862db` | 示例插件 + dotnet new 模板（含测试项目）；模板生成/构建/测试通过；512 测试通过；CI 绿 |
| 2026-11-19 | DH-8 | `42688aa` | 单容器单实例；15s 超时；插件状态页；513 测试通过；CI 绿 |
| 2026-11-19 | DH-7 | `32d7011` | 插件清单/档位过滤；Minimal 运行验证；裁剪脚本；515 测试通过；CI 绿 |
| 2026-11-19 | DH-12（测试连接） | `649e5c5` | UI 测试连接/超时；S7-1200 虚拟服务器实测成功；521 测试通过；CI 绿 |

> 每次提交前必须：`dotnet build AP-Automation.Platform.slnx -c Release` 0 错误 + 三个测试项目全绿。

---

**文档版本**：v2.7（2026-11-19，DH-12 测试连接完成）
**下一步**：继续 DH-12 的报文日志/点表导入，或按项目需要做 DH-11 报表/配方。
