param(
    [string]$PublishDir = (Join-Path $PSScriptRoot '..\bin\Release\publish'),
    [string]$IsccPath = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$publishFull = (Resolve-Path $PublishDir).Path
$setupScript = Join-Path $repoRoot 'installer\setup.iss'
$smokeIss = Join-Path $repoRoot 'installer\ci-smoke.iss'
$work = Join-Path $env:TEMP ("ap-install-smoke-{0}" -f ([guid]::NewGuid().ToString('N')))
$installDir = Join-Path $env:LOCALAPPDATA 'AP-Scaffold-CI-Smoke'
$dataRoot = Join-Path $work 'data'
$oldDataRoot = $env:AP_SCAFFOLD_DATA_ROOT
$appProcess = $null

function Find-Iscc {
    param([string]$ExplicitPath)
    if ($ExplicitPath) {
        if (-not (Test-Path $ExplicitPath)) { throw "ISCC 不存在: $ExplicitPath" }
        return $ExplicitPath
    }

    $candidates = @(
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'D:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { return $candidate }
    }

    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    throw '未找到 ISCC.exe，请安装 Inno Setup 6 或传 -IsccPath。'
}

try {
    New-Item -ItemType Directory -Force -Path $work | Out-Null
    if (Test-Path $installDir) {
        throw "测试安装目录已存在，请先卸载: $installDir"
    }

    $iscc = Find-Iscc -ExplicitPath $IsccPath
    $outputDir = Join-Path $work 'output'
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

    # 生成仅用于 CI/本机冒烟的非提权安装脚本：
    # - 装到 %LocalAppData%，避免 CI 无 UAC；
    # - 去掉 ProgramData 的 [Dirs]，数据根用 AP_SCAFFOLD_DATA_ROOT 指定。
    $text = Get-Content $setupScript -Raw
    $text = $text -replace 'AppId=\{\{AP-SCAFFOLD-PLATFORM-2026\}\}', 'AppId={{AP-SCAFFOLD-CI-SMOKE-2026}}'
    $text = $text -replace 'DefaultDirName=\{autopf\}\\{#MyAppName\}', 'DefaultDirName={localappdata}\AP-Scaffold-CI-Smoke'
    $text = $text -replace 'PrivilegesRequired=admin', 'PrivilegesRequired=lowest'
    $text = $text -replace 'OutputDir=Output', ("OutputDir=" + $outputDir)
    $text = $text -replace 'OutputBaseFilename=\{#[^}]+\}-\{#MyAppVersion\}-Setup', 'OutputBaseFilename=AP-Scaffold-CI-Smoke-Setup'
    $text = $text -replace '(?s)\[Dirs\].*?\[Files\]', '[Files]'
    Set-Content -Path $smokeIss -Value $text -Encoding UTF8

    & $iscc $smokeIss | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "ISCC 编译失败: $LASTEXITCODE" }

    $setup = Get-ChildItem $outputDir -Filter '*Setup.exe' | Select-Object -First 1
    if (-not $setup) { throw "未找到安装包: $outputDir" }

    $installLog = Join-Path $work 'install.log'
    $install = Start-Process -FilePath $setup.FullName `
        -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=$installLog") `
        -PassThru -Wait
    if ($install.ExitCode -ne 0) { throw "安装失败: $($install.ExitCode)，日志: $installLog" }

    $exe = Join-Path $installDir 'AP.Host.Desktop.exe'
    if (-not (Test-Path $exe)) { throw "安装目录缺少宿主: $exe" }

    $env:AP_SCAFFOLD_DATA_ROOT = $dataRoot
    $appProcess = Start-Process -FilePath $exe -WorkingDirectory $installDir -PassThru
    Start-Sleep -Seconds 12
    $appProcess.Refresh()
    if ($appProcess.HasExited) { throw "应用启动后退出，退出码: $($appProcess.ExitCode)" }

    foreach ($required in @(
        (Join-Path $dataRoot 'local_data.db'),
        (Join-Path $dataRoot 'logs'),
        (Join-Path $dataRoot 'Configuration\tags.json')
    )) {
        if (-not (Test-Path $required)) { throw "数据根缺少: $required" }
    }

    Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue
    $appProcess = $null
    Start-Sleep -Seconds 2

    $uninstaller = Join-Path $installDir 'unins000.exe'
    if (-not (Test-Path $uninstaller)) { throw "缺少卸载程序: $uninstaller" }
    $uninstall = Start-Process -FilePath $uninstaller `
        -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -PassThru -Wait
    if ($uninstall.ExitCode -ne 0) { throw "卸载失败: $($uninstall.ExitCode)" }
    if (Test-Path $installDir) { throw "卸载后安装目录仍存在: $installDir" }
    if (-not (Test-Path $dataRoot)) { throw "卸载误删了数据根: $dataRoot" }

    Write-Host 'INSTALL_SMOKE_OK' -ForegroundColor Green
}
catch {
    Write-Host "INSTALL_SMOKE_FAILED: $($_.Exception.Message)" -ForegroundColor Red
    if (Test-Path $work) { Get-ChildItem $work -Recurse -File | Select-Object FullName }
    throw
}
finally {
    if ($appProcess -and -not $appProcess.HasExited) {
        Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($null -eq $oldDataRoot) {
        Remove-Item Env:AP_SCAFFOLD_DATA_ROOT -ErrorAction SilentlyContinue
    } else {
        $env:AP_SCAFFOLD_DATA_ROOT = $oldDataRoot
    }
    if (Test-Path $smokeIss) { Remove-Item $smokeIss -Force -ErrorAction SilentlyContinue }
    if (Test-Path $work) { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
}
