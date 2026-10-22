#Requires -Version 5.1
<#
.SYNOPSIS
  인트라넷 PC: 반입 ZIP → 소스 전개 + NuGet 복원 + (가능하면) Release 빌드

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\Setup-IntranetDev.ps1 -TransferDir D:\in -TargetRoot D:\PocTrace\PocTraceSystem
#>
[CmdletBinding()]
param(
    [string]$TransferDir = "",
    [string]$TargetRoot = "",
    [string]$OfflineDevRoot = "C:\OfflineDev",
    [switch]$SkipBuild,
    [switch]$SkipRestore
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

function Find-Zip {
    param([string]$Dir, [string]$Pattern)
    if (-not $Dir -or -not (Test-Path $Dir)) { return $null }
    Get-ChildItem -LiteralPath $Dir -Filter $Pattern -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

function Expand-ZipTo {
    param([string]$ZipPath, [string]$Dest, [switch]$WipeDest)
    if ($WipeDest -and (Test-Path $Dest)) {
        Write-Host "  Clearing $Dest ..."
        Remove-Item -Recurse -Force $Dest
    }
    New-Item -ItemType Directory -Force -Path $Dest | Out-Null
    Write-Host "  Expanding $(Split-Path $ZipPath -Leaf) -> $Dest"
    Expand-Archive -LiteralPath $ZipPath -DestinationPath $Dest -Force
}

function Find-MSBuild {
    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\MSBuild.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null | Select-Object -First 1
        if ($found -and (Test-Path $found)) { return $found }
    }
    return $null
}

function Find-NuGetExe {
    $cands = @(
        (Join-Path $OfflineDevRoot "tools\nuget.exe"),
        "C:\Tools\nuget\nuget.exe",
        (Join-Path $TargetRoot "nuget.exe")
    )
    foreach ($c in $cands) { if ($c -and (Test-Path $c)) { return $c } }
    $cmd = Get-Command nuget -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

Write-Host "======== PocTrace Intranet Dev Setup ========" -ForegroundColor Magenta

# Resolve TransferDir: param > parent of script > script dir
if (-not $TransferDir) {
    $parent = Split-Path $scriptDir -Parent
    $grand = if ($parent) { Split-Path $parent -Parent } else { $null }
    foreach ($d in @((Split-Path $scriptDir -Parent), $scriptDir, $grand, (Get-Location).Path)) {
        if ($d -and (Find-Zip $d "01-PocTrace-Source.zip")) {
            $TransferDir = $d
            break
        }
    }
}
if (-not $TransferDir) { $TransferDir = (Get-Location).Path }

$zipSource = Find-Zip $TransferDir "01-PocTrace-Source.zip"
$zipNuget  = Find-Zip $TransferDir "02-PocTrace-NuGet.zip"
$zipWeb    = Find-Zip $TransferDir "03-PocTrace-WebAssets.zip"

# If running from already-extracted repo (no zip), detect solution
$hereSln = Join-Path (Split-Path (Split-Path $scriptDir -Parent) -Parent) "PocTraceSystem.sln"
if (-not $TargetRoot) {
    if (Test-Path $hereSln) {
        $TargetRoot = Split-Path $hereSln -Parent
    } else {
        $TargetRoot = Join-Path (Split-Path $TransferDir -Parent) "PocTraceSystem"
    }
}

Write-Host "TransferDir : $TransferDir"
Write-Host "TargetRoot  : $TargetRoot"
Write-Host "OfflineDev  : $OfflineDevRoot"

# --- 1) Source ---
if ($zipSource) {
    Write-Host "`n[1] Source ZIP" -ForegroundColor Cyan
    Expand-ZipTo -ZipPath $zipSource.FullName -Dest $TargetRoot -WipeDest:(-not (Test-Path (Join-Path $TargetRoot "PocTraceSystem.sln")))
    # If zip contained nested folder, flatten detection
    if (-not (Test-Path (Join-Path $TargetRoot "PocTraceSystem.sln"))) {
        $nested = Get-ChildItem $TargetRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName "PocTraceSystem.sln") } | Select-Object -First 1
        if ($nested) {
            Write-Host "  Nested layout detected: $($nested.FullName)"
            $TargetRoot = $nested.FullName
        }
    }
} elseif (Test-Path (Join-Path $TargetRoot "PocTraceSystem.sln")) {
    Write-Host "`n[1] Source already at TargetRoot (no 01 zip)" -ForegroundColor Yellow
} else {
    throw "01-PocTrace-Source.zip not found in $TransferDir and no solution at $TargetRoot"
}

$sln = Join-Path $TargetRoot "PocTraceSystem.sln"
if (-not (Test-Path $sln)) { throw "PocTraceSystem.sln missing under $TargetRoot" }

# --- 2) NuGet ---
$nugetDest = Join-Path $OfflineDevRoot "nuget-packages"
New-Item -ItemType Directory -Force -Path $nugetDest | Out-Null
if ($zipNuget) {
    Write-Host "`n[2] NuGet ZIP -> $nugetDest" -ForegroundColor Cyan
    $tmpN = Join-Path $env:TEMP ("poc-nuget-" + [guid]::NewGuid().ToString("n"))
    Expand-ZipTo -ZipPath $zipNuget.FullName -Dest $tmpN
    $inner = Join-Path $tmpN "nuget-packages"
    if (Test-Path $inner) {
        robocopy $inner $nugetDest /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    } else {
        robocopy $tmpN $nugetDest /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    }
    Remove-Item -Recurse -Force $tmpN -ErrorAction SilentlyContinue
} else {
    Write-Host "`n[2] No 02-PocTrace-NuGet.zip — using existing $nugetDest" -ForegroundColor Yellow
    if (-not (Get-ChildItem $nugetDest -ErrorAction SilentlyContinue | Select-Object -First 1)) {
        Write-Warning "nuget-packages is empty. Restore will fail without 02 zip."
    }
}

# --- 3) Web assets optional ---
if ($zipWeb) {
    Write-Host "`n[3] WebAssets ZIP" -ForegroundColor Cyan
    $webDest = Join-Path $OfflineDevRoot "web-assets"
    $tmpW = Join-Path $env:TEMP ("poc-web-" + [guid]::NewGuid().ToString("n"))
    Expand-ZipTo -ZipPath $zipWeb.FullName -Dest $tmpW
    $innerW = Join-Path $tmpW "web-assets"
    New-Item -ItemType Directory -Force -Path $webDest | Out-Null
    if (Test-Path $innerW) { robocopy $innerW $webDest /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null }
    else { robocopy $tmpW $webDest /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null }
    Remove-Item -Recurse -Force $tmpW -ErrorAction SilentlyContinue
} else {
    Write-Host "`n[3] Skip WebAssets (optional)" -ForegroundColor Yellow
}

# --- 4) nuget.config airgap ---
Write-Host "`n[4] Apply airgap nuget.config" -ForegroundColor Cyan
$airgapCfg = Join-Path $TargetRoot "nuget.config.airgap"
$cfgDest = Join-Path $TargetRoot "nuget.config"
if (Test-Path $airgapCfg) {
    Copy-Item -Force $airgapCfg $cfgDest
} else {
    $xml = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="OfflineDev" value="$OfflineDevRoot\nuget-packages" />
  </packageSources>
  <config>
    <add key="globalPackagesFolder" value="$OfflineDevRoot\nuget-packages" />
  </config>
</configuration>
"@
    [System.IO.File]::WriteAllText($cfgDest, $xml, (New-Object System.Text.UTF8Encoding $false))
}
Write-Host "  $cfgDest"

# --- 5) Restore ---
if (-not $SkipRestore) {
    Write-Host "`n[5] nuget restore" -ForegroundColor Cyan
    $nuget = Find-NuGetExe
    if (-not $nuget) {
        Write-Warning "nuget.exe not found. Open solution in VS and restore, or place nuget.exe under $OfflineDevRoot\tools\"
    } else {
        & $nuget restore $sln -PackagesDirectory $nugetDest -NonInteractive
        if ($LASTEXITCODE -ne 0) { throw "nuget restore failed (exit $LASTEXITCODE)" }
        Write-Host "  Restore OK" -ForegroundColor Green
    }
}

# --- 6) Build ---
if (-not $SkipBuild) {
    Write-Host "`n[6] MSBuild Release" -ForegroundColor Cyan
    $msbuild = Find-MSBuild
    if (-not $msbuild) {
        Write-Warning "MSBuild not found. Open $sln in Visual Studio and Build > Rebuild Solution (Release)."
    } else {
        Write-Host "  $msbuild"
        & $msbuild $sln /t:Rebuild /p:Configuration=Release /m /v:minimal
        if ($LASTEXITCODE -ne 0) { throw "MSBuild failed (exit $LASTEXITCODE)" }
        Write-Host "  Build OK" -ForegroundColor Green
        Write-Host "  Admin:  $(Join-Path $TargetRoot 'PocTrace.Admin\bin\Release')"
        Write-Host "  Viewer: $(Join-Path $TargetRoot 'PocTrace.UI\bin\Release')"
    }
}

# --- 7) Next steps ---
$configEx = Join-Path $scriptDir "config"
Write-Host "`n======== DONE ========" -ForegroundColor Green
Write-Host "Solution: $sln"
Write-Host "Next:"
Write-Host "  1) VS에서 솔루션 열어 빌드 확인"
Write-Host "  2) SVDI용 Runtime ZIP: Pack 시 -IncludeBinRelease 또는 Admin/Viewer bin\Release 수동 ZIP"
Write-Host "  3) SVDI에서 Setup-SvdiRuntime.bat 실행"
Write-Host "  4) 설정 예: $configEx"
Write-Host "Guide: $(Join-Path $scriptDir 'AIRGAP_TRANSFER_GUIDE.md')"
