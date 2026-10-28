#Requires -Version 5.1
<#
.SYNOPSIS
  Jcs.UiAssets 1.0.0 nupkg를 인트라넷 오프라인 NuGet 폴더에 복사합니다.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\Install-JcsUiAssets.ps1
#>
[CmdletBinding()]
param(
    [string]$OfflineDevRoot = "C:\OfflineDev",
    [string]$PackageId = "Jcs.UiAssets",
    [string]$PackageVersion = "1.0.0"
)

$ErrorActionPreference = "Stop"
$fileName = "$PackageId.$PackageVersion.nupkg"
$candidates = @(
    (Join-Path $PSScriptRoot $fileName),
    (Join-Path $PSScriptRoot "..\nuget-packages\$fileName")
)
$source = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $source) {
    throw "nupkg not found. Looked for $fileName next to this script and in offline-kit\nuget-packages."
}

$destDir = Join-Path $OfflineDevRoot "nuget-packages"
New-Item -ItemType Directory -Force -Path $destDir | Out-Null
$dest = Join-Path $destDir $fileName
Copy-Item -LiteralPath $source -Destination $dest -Force

$note = @"
id=$PackageId
version=$PackageVersion
file=$fileName
installed=$(Get-Date -Format o)
"@
[System.IO.File]::WriteAllText((Join-Path $destDir "$PackageId.$PackageVersion.txt"), $note.Trim() + "`r`n", (New-Object System.Text.UTF8Encoding $false))

Write-Host "Installed $PackageId $PackageVersion"
Write-Host "  from $source"
Write-Host "  to   $dest"
Write-Host "Install into another project:"
Write-Host "  nuget install $PackageId -Version $PackageVersion -Source $destDir -OutputDirectory packages"
