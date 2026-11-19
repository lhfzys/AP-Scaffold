param(
    [Parameter(Mandatory = $true)]
    [string]$Profile,

    [string]$PublishDir = 'bin\Release\publish'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$publish = if ([IO.Path]::IsPathRooted($PublishDir)) {
    (Resolve-Path $PublishDir).Path
} else {
    (Resolve-Path (Join-Path $root $PublishDir)).Path
}
$catalogPath = Join-Path $publish 'Configuration\plugins.catalog.json'

if (-not (Test-Path $catalogPath)) {
    throw "插件清单不存在: $catalogPath"
}

$catalog = Get-Content $catalogPath -Raw | ConvertFrom-Json
if ($Profile -eq 'Full') {
    Write-Host "插件档位 Full：保留全部插件。" -ForegroundColor Green
    return
}

$profileEntry = $catalog.profiles.PSObject.Properties[$Profile]
if (-not $profileEntry) {
    throw "插件档位不存在: $Profile（可选: $($catalog.profiles.PSObject.Properties.Name -join ', ')）"
}

$allowed = @($profileEntry.Value)
if ($allowed -contains '*') {
    Write-Host "插件档位 $Profile 使用通配，保留全部插件。" -ForegroundColor Green
    return
}

foreach ($required in @('AP.Plugin.Layout', 'AP.Plugin.Login')) {
    if ($allowed -notcontains $required) {
        throw "档位 $Profile 缺少必需插件 $required，宿主将无法启动。"
    }
}

$pluginsDir = Join-Path $publish 'plugins'
$removed = 0
Get-ChildItem $pluginsDir -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    if ($allowed -notcontains $_.Name) {
        Remove-Item $_.FullName -Recurse -Force
        $removed++
        Write-Host "  移除插件: $($_.Name)"
    }
}

Write-Host "插件档位 $Profile 应用完成：保留 $($allowed.Count) 个插件，移除 $removed 个插件目录。" -ForegroundColor Green
