#Requires -Version 5.1
<#
.SYNOPSIS
  노트북에서 에어갭 반입용 ZIP을 생성합니다.

.EXAMPLE
  cd "C:\JCS\1. Codes\260718\PocTraceSystem"
  powershell -ExecutionPolicy Bypass -File .\offline-kit\airgap\Pack-AirgapTransfer.ps1
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$OfflineDevRoot = "C:\OfflineDev",
    [string]$OutDir = "",
    [switch]$SkipNuGet,
    [switch]$SkipWebAssets,
    [switch]$IncludeBinRelease
)

$ErrorActionPreference = "Stop"

if (-not $RepoRoot) {
    $RepoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
}
if (-not $OutDir) {
    $stamp = Get-Date -Format "yyyyMMdd"
    $OutDir = Join-Path $OfflineDevRoot "transfer\PocTrace-Airgap-$stamp"
}

$sln = Join-Path $RepoRoot "PocTraceSystem.sln"
if (-not (Test-Path $sln)) {
    throw "Solution not found: $sln (RepoRoot wrong?)"
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$work = Join-Path $OutDir "_stage"
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory -Force -Path $work | Out-Null

Write-Host "======== Pack Airgap Transfer ========" -ForegroundColor Magenta
Write-Host "Repo:  $RepoRoot"
Write-Host "Out:   $OutDir"

# --- exclude patterns for source ---
$excludeDirNames = @(
    'bin', 'obj', '.vs', '.git', 'node_modules',
    'agent-transcripts', 'terminals', 'html-preview'
)

function Copy-SourceTree {
    param([string]$Src, [string]$Dst)
    New-Item -ItemType Directory -Force -Path $Dst | Out-Null
    Get-ChildItem -LiteralPath $Src -Force | ForEach-Object {
        if ($_.PSIsContainer) {
            if ($excludeDirNames -contains $_.Name) { return }
            Copy-SourceTree -Src $_.FullName -Dst (Join-Path $Dst $_.Name)
        } else {
            # skip huge/local-only
            if ($_.Name -match '\.(user|suo)$') { return }
            if ($_.Name -eq 'app.js.full.bak') { return }
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $Dst $_.Name) -Force
        }
    }
}

# ========== 01 Source ==========
Write-Host "`n[1/3] Staging source..." -ForegroundColor Cyan
$srcStage = Join-Path $work "source"
Copy-SourceTree -Src $RepoRoot -Dst $srcStage

# Ensure airgap scripts + guide present under offline-kit\airgap
$airgapDst = Join-Path $srcStage "offline-kit\airgap"
New-Item -ItemType Directory -Force -Path $airgapDst | Out-Null
Copy-Item -Force (Join-Path $PSScriptRoot "*") $airgapDst

# nuget.config airgap template into package
$nugetAirgap = @"
<?xml version="1.0" encoding="utf-8"?>
<!-- 에어갭/인트라넷: nuget.org 제거. Setup-IntranetDev가 C:\OfflineDev\nuget-packages 를 채움 -->
<configuration>
  <packageSources>
    <clear />
    <add key="OfflineDev" value="C:\OfflineDev\nuget-packages" />
  </packageSources>
  <config>
    <add key="globalPackagesFolder" value="C:\OfflineDev\nuget-packages" />
  </config>
</configuration>
"@
$nugetAirgapPath = Join-Path $srcStage "nuget.config.airgap"
[System.IO.File]::WriteAllText($nugetAirgapPath, $nugetAirgap, (New-Object System.Text.UTF8Encoding $false))

# README at zip root (also in OutDir)
$readme = @"
PocTrace Airgap Transfer Package
Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Repo: $RepoRoot

1) Intranet PC: extract 01 zip, run offline-kit\airgap\Setup-IntranetDev.bat
   (place 01/02/03 zips in the same folder as -TransferDir)
2) SVDI: run Setup-SvdiRuntime.bat with Release bin zip or paths
See: offline-kit\airgap\AIRGAP_TRANSFER_GUIDE.md
"@
[System.IO.File]::WriteAllText((Join-Path $OutDir "00-README.txt"), $readme, (New-Object System.Text.UTF8Encoding $false))

$zip1 = Join-Path $OutDir "01-PocTrace-Source.zip"
if (Test-Path $zip1) { Remove-Item $zip1 -Force }
Compress-Archive -Path (Join-Path $srcStage "*") -DestinationPath $zip1 -CompressionLevel Optimal
Write-Host "  -> $zip1 ($([math]::Round((Get-Item $zip1).Length/1MB,1)) MB)" -ForegroundColor Green

# ========== 02 NuGet ==========
if (-not $SkipNuGet) {
    Write-Host "`n[2/3] Staging NuGet cache..." -ForegroundColor Cyan
    $nugetSrc = Join-Path $OfflineDevRoot "nuget-packages"
    $repoNuget = Join-Path $RepoRoot "offline-kit\nuget-packages"
    $zip2 = Join-Path $OutDir "02-PocTrace-NuGet.zip"
    if (Test-Path $zip2) { Remove-Item $zip2 -Force }
    $nugetStage = Join-Path $work "nuget-packages"
    New-Item -ItemType Directory -Force -Path $nugetStage | Out-Null
    $staged = $false
    if (Test-Path $nugetSrc) {
        Write-Host "  Copying packages (may take a while)..."
        robocopy $nugetSrc $nugetStage /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
        $staged = $true
    } else {
        Write-Warning "NuGet folder missing: $nugetSrc — run Download-OfflinePackages.ps1 -All first"
    }
    if (Test-Path $repoNuget) {
        Write-Host "  Adding repo packages (Jcs.UiAssets and any other nupkg in offline-kit)..."
        robocopy $repoNuget $nugetStage *.nupkg /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
        $staged = $true
    }
    if ($staged -and (Get-ChildItem -LiteralPath $nugetStage -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1)) {
        Compress-Archive -Path $nugetStage -DestinationPath $zip2 -CompressionLevel Optimal
        Write-Host "  -> $zip2 ($([math]::Round((Get-Item $zip2).Length/1MB,1)) MB)" -ForegroundColor Green
    } else {
        Write-Warning "No NuGet files staged. 02 zip was not created."
    }
} else {
    Write-Host "`n[2/3] Skip NuGet (-SkipNuGet)" -ForegroundColor Yellow
}

# ========== 03 Web assets ==========
if (-not $SkipWebAssets) {
    Write-Host "`n[3/3] Staging web-assets (optional backup)..." -ForegroundColor Cyan
    $webSrc = Join-Path $OfflineDevRoot "web-assets"
    if (Test-Path $webSrc) {
        $zip3 = Join-Path $OutDir "03-PocTrace-WebAssets.zip"
        if (Test-Path $zip3) { Remove-Item $zip3 -Force }
        $webStage = Join-Path $work "web-assets"
        New-Item -ItemType Directory -Force -Path $webStage | Out-Null
        robocopy $webSrc $webStage /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
        Compress-Archive -Path $webStage -DestinationPath $zip3 -CompressionLevel Optimal
        Write-Host "  -> $zip3 ($([math]::Round((Get-Item $zip3).Length/1MB,1)) MB)" -ForegroundColor Green
    } else {
        Write-Host "  web-assets not found — skip (Templates/assets already in Source zip)" -ForegroundColor Yellow
    }
} else {
    Write-Host "`n[3/3] Skip WebAssets" -ForegroundColor Yellow
}

# ========== Optional Release bins ==========
if ($IncludeBinRelease) {
    Write-Host "`n[+] Staging Release bins..." -ForegroundColor Cyan
    $relStage = Join-Path $work "runtime"
    $adminRel = Join-Path $RepoRoot "PocTrace.Admin\bin\Release"
    $viewRel = Join-Path $RepoRoot "PocTrace.UI\bin\Release"
    if ((Test-Path $adminRel) -or (Test-Path $viewRel)) {
        New-Item -ItemType Directory -Force -Path (Join-Path $relStage "Admin"), (Join-Path $relStage "Viewer") | Out-Null
        if (Test-Path $adminRel) { robocopy $adminRel (Join-Path $relStage "Admin") /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null }
        if (Test-Path $viewRel) { robocopy $viewRel (Join-Path $relStage "Viewer") /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null }
        $zip4 = Join-Path $OutDir "04-PocTrace-Runtime.zip"
        if (Test-Path $zip4) { Remove-Item $zip4 -Force }
        Compress-Archive -Path (Join-Path $relStage "*") -DestinationPath $zip4 -CompressionLevel Optimal
        Write-Host "  -> $zip4" -ForegroundColor Green
    } else {
        Write-Warning "Release bins not found. Build Release first or omit -IncludeBinRelease"
    }
}

# cleanup stage
Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue

# manifest
$manifest = Get-ChildItem $OutDir -Filter "*.zip" | ForEach-Object {
    "{0}`t{1:N1} MB`t{2}" -f $_.Name, ($_.Length/1MB), $_.FullName
}
$manifestPath = Join-Path $OutDir "MANIFEST.txt"
@(
    "PocTrace Airgap Pack $(Get-Date -Format o)",
    "RepoRoot=$RepoRoot",
    "OfflineDevRoot=$OfflineDevRoot",
    ""
) + $manifest | Set-Content -Path $manifestPath -Encoding UTF8

Write-Host "`n======== DONE ========" -ForegroundColor Green
Write-Host "Folder: $OutDir"
Write-Host "Next: mail/USB these zips → Intranet Setup-IntranetDev.bat → SVDI Setup-SvdiRuntime.bat"
Write-Host "Guide: offline-kit\airgap\AIRGAP_TRANSFER_GUIDE.md"
