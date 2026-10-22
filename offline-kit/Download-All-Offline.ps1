#Requires -Version 5.1
<#
.SYNOPSIS
  One-shot: NuGet (current+future) + web assets (css/js/fonts/emoji) into C:\OfflineDev

.EXAMPLE
  cd "C:\JCS\1. Codes\260718\PocTraceSystem"
  powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-All-Offline.ps1
  powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-All-Offline.ps1 -CopyToProject
#>
[CmdletBinding()]
param(
    [string]$Root = "C:\OfflineDev",
    [switch]$CopyToProject,
    [switch]$SkipNuGet,
    [switch]$SkipWeb,
    [switch]$EssentialWebOnly
)

$ErrorActionPreference = "Continue"
$kit = $PSScriptRoot
$code = 0

Write-Host "======== Offline kit: NuGet ========" -ForegroundColor Magenta
if (-not $SkipNuGet) {
    & powershell -ExecutionPolicy Bypass -File (Join-Path $kit "Download-OfflinePackages.ps1") -All -Root $Root
    if ($LASTEXITCODE -ne 0) { $code = $LASTEXITCODE }
}

Write-Host ""
Write-Host "======== Offline kit: Web assets ========" -ForegroundColor Magenta
if (-not $SkipWeb) {
    $webScript = Join-Path $kit "Download-OfflineWebAssets.ps1"
    $argList = @("-ExecutionPolicy", "Bypass", "-File", $webScript, "-Root", $Root)
    if ($CopyToProject) { $argList += "-CopyToProject" }
    if ($EssentialWebOnly) { $argList += "-EssentialOnly" }
    & powershell @argList
    if ($LASTEXITCODE -ne 0) { $code = $LASTEXITCODE }
}

Write-Host ""
Write-Host "======== Summary ========" -ForegroundColor Green
Write-Host "NuGet:  $Root\nuget-packages"
Write-Host "Assets: $Root\web-assets\{css,js,fonts,emoji}"
Write-Host "Models: $Root\models  (ONNX files — copy manually)"
exit $code
