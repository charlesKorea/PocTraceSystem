#Requires -Version 5.1
<#
.SYNOPSIS
  Copy WinForms .NET 4.8 offline template to a new folder.

.EXAMPLE
  .\New-WinFormsAppFromTemplate.ps1 -Name PlantReport -Destination "C:\JCS\1. Codes\PlantReport"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [Parameter(Mandatory = $true)]
    [string]$Destination
)

$ErrorActionPreference = "Stop"
$src = Join-Path $PSScriptRoot "WinFormsNet48"
if (-not (Test-Path $src)) { throw "Template not found: $src" }

$safe = ($Name -replace '[^A-Za-z0-9_]', '')
if ([string]::IsNullOrWhiteSpace($safe)) { throw "Name must contain letters or digits" }

if (Test-Path $Destination) {
    throw "Destination already exists: $Destination"
}

New-Item -ItemType Directory -Force -Path $Destination | Out-Null
Copy-Item -Recurse -Force (Join-Path $src "*") $Destination

$slnOld = Join-Path $Destination "WinFormsOfflineTemplate.sln"
$slnNew = Join-Path $Destination ($safe + ".sln")
if (Test-Path $slnOld) {
    Move-Item -Force $slnOld $slnNew
}

Write-Host "Copied template -> $Destination" -ForegroundColor Green
Write-Host "Open: $slnNew"
Write-Host "NuGet cache: C:\OfflineDev\nuget-packages"
Write-Host "See README.md in the new folder."
