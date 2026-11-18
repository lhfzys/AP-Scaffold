param(
    [string]$Solution = 'AP-Automation.Platform.slnx'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repoRoot
try {
    $jsonText = (& dotnet list $Solution package --vulnerable --include-transitive --format json 2>$null | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet list package 失败，退出码 $LASTEXITCODE"
    }

    $data = $jsonText | ConvertFrom-Json
    $findings = @()
    foreach ($project in $data.projects) {
        foreach ($framework in @($project.frameworks)) {
            if ($null -eq $framework) { continue }
            $packages = @($framework.topLevelPackages) + @($framework.transitivePackages)
            foreach ($package in $packages) {
                if ($null -eq $package) { continue }
                foreach ($vulnerability in @($package.vulnerabilities)) {
                    if ($null -eq $vulnerability) { continue }
                    $findings += [pscustomobject]@{
                        Project    = Split-Path $project.path -Leaf
                        Framework  = $framework.framework
                        Package    = $package.id
                        Version    = $package.resolvedVersion
                        Severity   = $vulnerability.severity
                        Advisory   = $vulnerability.advisoryurl
                    }
                }
            }
        }
    }

    if ($findings.Count -gt 0) {
        $findings | Format-Table -AutoSize | Out-String | Write-Host
        throw "发现 $($findings.Count) 个易受攻击的包，CI 门禁失败。"
    }

    Write-Host 'VULNERABILITY_CHECK_OK: 未发现易受攻击的包。' -ForegroundColor Green
}
finally {
    Pop-Location
}
