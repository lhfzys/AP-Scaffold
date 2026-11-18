# 数据库备份与恢复（SQLite）

> 适用：AP-Scaffold 单机版，数据根默认 `C:\ProgramData\AP-Scaffold`。

## 自动备份

应用每次启动、在 schema 迁移之前自动执行：

```text
VACUUM INTO 'C:\ProgramData\AP-Scaffold\backups\local_data-yyyyMMdd-HHmmss.db'
PRAGMA quick_check
```

- 首次启动/空库不生成备份；
- 生成后立即 `quick_check`，失败会删除该备份并中止启动；
- 默认保留最近 10 代，配置项：`Database:Backup:RetentionCount`；
- 备份文件是完整独立的 SQLite 数据库，可直接用 DB Browser for SQLite 打开检查。

## 手工恢复（推荐流程）

应用运行中不能覆盖数据库。恢复必须离线执行：

1. 退出应用（托盘“退出”或任务管理器结束 `AP.Host.Desktop`）；
2. 运行仓库脚本：

```powershell
cd D:\workspace\AP-Scaffold
.\scripts\restore-db.ps1 -BackupPath "C:\ProgramData\AP-Scaffold\backups\local_data-20261118-152309.db"
```

3. 脚本会先备份当前库为 `pre-restore-yyyyMMdd-HHmmss.db`，再覆盖 `local_data.db`，并删除 `-wal`/`-shm`；
4. 重新启动应用，确认数据/配置/点表正常。

> 如果数据根不是默认路径，加 `-DataRoot "D:\AP-Scaffold-Data"`。

## 手工恢复（无脚本）

```powershell
# 1. 退出应用
Stop-Process -Name AP.Host.Desktop -Force -ErrorAction SilentlyContinue

# 2. 备份当前库（保险）
$root = "$env:ProgramData\AP-Scaffold"
Copy-Item "$root\local_data.db" "$root\local_data.before-restore.db" -Force

# 3. 覆盖并清理 WAL
Copy-Item "C:\path\to\local_data-20261118-152309.db" "$root\local_data.db" -Force
Remove-Item "$root\local_data.db-wal","$root\local_data.db-shm" -Force -ErrorAction SilentlyContinue

# 4. 启动应用
& "C:\Program Files\自动化监控系统\AP.Host.Desktop.exe"
```

## 注意

- 不要只复制 `local_data.db` 而保留旧 `-wal`/`-shm`：会混入旧事务，必须一起删除；
- 恢复后首次启动会再生成一份新备份（因为库已存在）；
- 备份文件含用户哈希与审计数据，按现场数据同样保护，不要随意外发。
