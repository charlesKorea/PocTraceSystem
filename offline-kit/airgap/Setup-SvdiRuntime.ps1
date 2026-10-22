#Requires -Version 5.1
<#
.SYNOPSIS
  SVDI: 공용폴더 생성 + Admin/Viewer 런타임 배치 + 경로 설정

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\Setup-SvdiRuntime.ps1 -RuntimeZip D:\in\04-PocTrace-Runtime.zip
  powershell -ExecutionPolicy Bypass -File .\Setup-SvdiRuntime.ps1 -AdminBin D:\build\Admin -ViewerBin D:\build\Viewer
#>
[CmdletBinding()]
param(
    [string]$RuntimeZip = "",
    [string]$AdminBin = "",
    [string]$ViewerBin = "",
    [string]$SharedRoot = "X:\jHOOK",
    [string]$DeployRoot = "",
    [string]$CacheFolderName = "PocTraceData",
    [string]$ReportFolderName = "DailyReport",
    [switch]$SkipConfigPatch
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

if (-not $DeployRoot) {
    $DeployRoot = Join-Path $SharedRoot "Apps\PocTrace"
}

$cachePath = Join-Path $SharedRoot $CacheFolderName
$reportPath = Join-Path $SharedRoot $ReportFolderName
$adminDest = Join-Path $DeployRoot "Admin"
$viewerDest = Join-Path $DeployRoot "Viewer"

Write-Host "======== PocTrace SVDI Runtime Setup ========" -ForegroundColor Magenta
Write-Host "SharedRoot : $SharedRoot"
Write-Host "DeployRoot : $DeployRoot"
Write-Host "Cache      : $cachePath"
Write-Host "Report     : $reportPath"

# Resolve RuntimeZip if empty
if (-not $RuntimeZip) {
    $searchDirs = @($scriptDir, (Split-Path $scriptDir -Parent), (Get-Location).Path)
    foreach ($d in $searchDirs) {
        $z = Get-ChildItem -LiteralPath $d -Filter "04-PocTrace-Runtime.zip" -File -EA SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($z) { $RuntimeZip = $z.FullName; break }
    }
}

function Test-WritableDir([string]$Path) {
    try {
        New-Item -ItemType Directory -Force -Path $Path | Out-Null
        $probe = Join-Path $Path (".write_probe_" + [guid]::NewGuid().ToString("n"))
        Set-Content -Path $probe -Value "ok" -Encoding ASCII
        Remove-Item $probe -Force
        return $true
    } catch {
        return $false
    }
}

if (-not (Test-WritableDir $SharedRoot)) {
    throw "Cannot write to SharedRoot: $SharedRoot (map drive / ACL?)"
}

Write-Host "`n[1] Create shared folders" -ForegroundColor Cyan
@(
    $cachePath,
    $reportPath,
    (Join-Path $reportPath "assets"),
    (Join-Path $reportPath "Excel"),
    $adminDest,
    $viewerDest,
    (Join-Path $DeployRoot "logs")
) | ForEach-Object {
    New-Item -ItemType Directory -Force -Path $_ | Out-Null
    Write-Host "  OK $_"
}

Write-Host "`n[2] Deploy binaries" -ForegroundColor Cyan
$tmp = $null
if ($RuntimeZip -and (Test-Path $RuntimeZip)) {
    $tmp = Join-Path $env:TEMP ("poc-rt-" + [guid]::NewGuid().ToString("n"))
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    Expand-Archive -LiteralPath $RuntimeZip -DestinationPath $tmp -Force
    $a = Join-Path $tmp "Admin"
    $v = Join-Path $tmp "Viewer"
    if (-not (Test-Path $a)) {
        # maybe flat Release dump
        if (Test-Path (Join-Path $tmp "PocTrace.Admin.exe")) { $a = $tmp }
    }
    if (-not (Test-Path $v)) {
        if (Test-Path (Join-Path $tmp "PocTrace.UI.exe")) { $v = $tmp }
        elseif (Test-Path (Join-Path $tmp "PocTrace.Viewer.exe")) { $v = $tmp }
    }
    if (Test-Path $a) { $AdminBin = $a }
    if (Test-Path $v) { $ViewerBin = $v }
}

if (-not $AdminBin -or -not (Test-Path $AdminBin)) {
    Write-Warning "AdminBin not set. Pass -RuntimeZip or -AdminBin. Folders created only."
} else {
    Write-Host "  Admin: $AdminBin -> $adminDest"
    robocopy $AdminBin $adminDest /E /XO /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
}

if (-not $ViewerBin -or -not (Test-Path $ViewerBin)) {
    Write-Host "  Viewer: (skipped)" -ForegroundColor Yellow
} else {
    Write-Host "  Viewer: $ViewerBin -> $viewerDest"
    robocopy $ViewerBin $viewerDest /E /XO /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
}

function Patch-ExeConfig {
    param([string]$ConfigPath, [string]$Cache, [string]$Report)
    if (-not (Test-Path $ConfigPath)) {
        Write-Warning "Config not found: $ConfigPath"
        return
    }
    $bak = $ConfigPath + ".bak_" + (Get-Date -Format "yyyyMMddHHmmss")
    Copy-Item -Force $ConfigPath $bak
    [xml]$xml = Get-Content -LiteralPath $ConfigPath -Encoding UTF8
    $app = $xml.configuration.appSettings
    if ($app) {
        foreach ($pair in @(
            @{ k = "SharedCachePath"; v = $Cache },
            @{ k = "DailyReportPath"; v = $Report }
        )) {
            $node = $app.add | Where-Object { $_.key -eq $pair.k } | Select-Object -First 1
            if ($node) { $node.value = $pair.v }
            else {
                $el = $xml.CreateElement("add")
                $el.SetAttribute("key", $pair.k)
                $el.SetAttribute("value", $pair.v)
                [void]$app.AppendChild($el)
            }
        }
    }
    $xml.Save($ConfigPath)
    Write-Host "  Patched paths: $ConfigPath"
    Write-Host "  Backup: $bak"
}

if (-not $SkipConfigPatch) {
    Write-Host "`n[3] Patch SharedCachePath / DailyReportPath" -ForegroundColor Cyan
    $adminCfg = @(
        (Join-Path $adminDest "PocTrace.Admin.exe.config"),
        (Join-Path $adminDest "PocTrace.Admin.dll.config")
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    $viewerCfg = @(
        (Join-Path $viewerDest "PocTrace.UI.exe.config"),
        (Join-Path $viewerDest "PocTrace.Viewer.exe.config")
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if ($adminCfg) { Patch-ExeConfig -ConfigPath $adminCfg -Cache $cachePath -Report $reportPath }
    if ($viewerCfg) { Patch-ExeConfig -ConfigPath $viewerCfg -Cache $cachePath -Report $reportPath }
}

# Copy example configs for reference
$cfgEx = Join-Path $scriptDir "config"
if (Test-Path $cfgEx) {
    $dstEx = Join-Path $DeployRoot "config-examples"
    New-Item -ItemType Directory -Force -Path $dstEx | Out-Null
    Copy-Item -Force (Join-Path $cfgEx "*") $dstEx
}

$note = @"
PocTrace SVDI install
$(Get-Date -Format o)
SharedRoot=$SharedRoot
Cache=$cachePath
Report=$reportPath
Admin=$adminDest
Viewer=$viewerDest

MANUAL REQUIRED:
1) Edit Admin *.exe.config connectionStrings (SourceDb / PocTraceDB) to real SQL Server
2) Set TargetDatabases (e.g. x1,x2,x2_2)
3) Run PocTrace.Admin.exe once (manual job) to fill cache + DailyReport
4) Open DailyReport\index.html or report_*.html — assets must be local (no CDN)
5) Viewer: AllowDatabaseFallback=false recommended; same SharedCachePath

See AIRGAP_TRANSFER_GUIDE.md
"@
[System.IO.File]::WriteAllText((Join-Path $DeployRoot "INSTALL_NOTES.txt"), $note, (New-Object System.Text.UTF8Encoding $false))

if ($tmp) { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }

Write-Host "`n======== DONE ========" -ForegroundColor Green
Write-Host "Admin exe : $(Join-Path $adminDest 'PocTrace.Admin.exe')"
Write-Host "Notes     : $(Join-Path $DeployRoot 'INSTALL_NOTES.txt')"
Write-Host "IMPORTANT : connectionStrings must be edited manually (secrets not in zip)."
