param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,

    [string]$DataRoot = (Join-Path $env:ProgramData 'AP-Scaffold')
)

$ErrorActionPreference = 'Stop'

$databasePath = Join-Path $DataRoot 'local_data.db'
if (-not (Test-Path $BackupPath)) {
    throw "备份文件不存在: $BackupPath"
}
if (-not (Test-Path $databasePath)) {
    throw "目标数据库不存在: $databasePath"
}

$running = Get-Process -Name 'AP.Host.Desktop' -ErrorAction SilentlyContinue
if ($running) {
    throw "应用正在运行（PID: $($running.Id -join ',')）。请先退出应用再恢复。"
}

$backupFull = (Resolve-Path $BackupPath).Path
$databaseFull = (Resolve-Path $databasePath).Path
$backupDirectory = Join-Path $DataRoot 'backups'
New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null

$preRestore = Join-Path $backupDirectory ("pre-restore-{0}.db" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
Copy-Item $databaseFull $preRestore -Force

Copy-Item $backupFull $databaseFull -Force
Remove-Item "$databaseFull-wal", "$databaseFull-shm" -Force -ErrorAction SilentlyContinue

Write-Host "恢复完成:" -ForegroundColor Green
Write-Host "  备份来源: $backupFull"
Write-Host "  目标数据库: $databaseFull"
Write-Host "  恢复前快照: $preRestore"
Write-Host "请重新启动应用，并在启动日志中确认 quick_check/迁移结果。"
