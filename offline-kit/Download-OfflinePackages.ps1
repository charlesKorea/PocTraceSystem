#Requires -Version 5.1
<#
.SYNOPSIS
  Future-ready NuGet packages -> C:\OfflineDev\nuget-packages

.DESCRIPTION
  -Current : packages already referenced by PocTrace
  -Future  : recommended packages for VDI/airgap extensibility (DI, config, ML, Excel, etc.)
  -All     : Current + Future (default)

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-OfflinePackages.ps1
  powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-OfflinePackages.ps1 -Future
  powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-OfflinePackages.ps1 -All
#>
[CmdletBinding()]
param(
    [switch]$Current,
    [switch]$Future,
    [switch]$All,
    [string]$Root = "C:\OfflineDev",
    [string]$Source = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"

if (-not $Current -and -not $Future -and -not $All) { $All = $true }
if ($All) { $Current = $true; $Future = $true }

$NuGetDir = Join-Path $Root "nuget-packages"
New-Item -ItemType Directory -Force -Path $NuGetDir | Out-Null

$nugetCmd = $null
$nugetFromPath = Get-Command nuget -ErrorAction SilentlyContinue
if ($nugetFromPath) { $nugetCmd = $nugetFromPath.Source }
elseif (Test-Path "C:\Tools\nuget\nuget.exe") { $nugetCmd = "C:\Tools\nuget\nuget.exe" }

if (-not $nugetCmd) {
    $toolsDir = "C:\Tools\nuget"
    New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
    $nugetExe = Join-Path $toolsDir "nuget.exe"
    Write-Host "Downloading nuget.exe -> $nugetExe" -ForegroundColor Cyan
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $nugetExe -UseBasicParsing
        $nugetCmd = $nugetExe
    }
    catch {
        Write-Host "nuget.exe not found and download failed: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "  winget install Microsoft.NuGet" -ForegroundColor Yellow
        Write-Host "  or save nuget.exe to C:\Tools\nuget\nuget.exe" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "Using nuget: $nugetCmd" -ForegroundColor DarkGray
Write-Host "Target: $NuGetDir" -ForegroundColor Cyan
Write-Host "Source: $Source" -ForegroundColor DarkGray

# PackageId|Version — pin versions for reproducible airgap kits
$currentPackages = @(
    "Dapper|2.1.66"
    "EPPlus|7.2.1"
    "LargeXlsx|1.10.0"
    "Newtonsoft.Json|13.0.4"
    "Serilog|4.3.0"
    "Serilog.Sinks.File|7.0.0"
    "System.Data.SqlClient|4.8.6"
    "System.Data.SQLite|2.0.2"
    "Quartz|3.15.1"
    "Costura.Fody|6.0.0"
    "Fody|6.8.2"
    "MetroFramework|1.2.0.3"
    "MetroModernUI|1.4.0"
    "Bogus|35.6.5"
    "Microsoft.Bcl.AsyncInterfaces|8.0.0"
    "System.Threading.Tasks.Extensions|4.5.4"
    "System.ValueTuple|4.5.0"
    "Microsoft.ML|3.0.1"
    "Microsoft.ML.OnnxRuntime|1.20.1"
)

# Future / extensibility — download now while online; adopt in csproj later as needed
$futurePackages = @(
    # Composition / config (no cloud APIs)
    "Microsoft.Extensions.DependencyInjection|8.0.1"
    "Microsoft.Extensions.DependencyInjection.Abstractions|8.0.2"
    "Microsoft.Extensions.Configuration|8.0.0"
    "Microsoft.Extensions.Configuration.Json|8.0.1"
    "Microsoft.Extensions.Configuration.Binder|8.0.2"
    "Microsoft.Extensions.Options|8.0.2"
    "Microsoft.Extensions.Logging|8.0.1"
    "Microsoft.Extensions.Logging.Abstractions|8.0.2"
    "Serilog.Extensions.Logging|8.0.0"
    "Serilog.Sinks.Console|6.0.0"

    # Data / SQL / cache helpers
    "Microsoft.Data.SqlClient|5.2.2"
    "CsvHelper|33.0.1"
    "Dapper.Contrib|2.0.78"
    "System.Text.Json|8.0.5"
    "MessagePack|2.5.192"
    "protobuf-net|3.2.45"

    # Excel / documents (EPPlus complement)
    "ClosedXML|0.104.2"
    "DocumentFormat.OpenXml|3.1.1"
    "ExcelDataReader|3.7.0"
    "ExcelDataReader.DataSet|3.7.0"

    # Resilience / utils (local only)
    "Polly|8.5.0"
    "Humanizer.Core|2.14.1"
    "FluentValidation|11.11.0"
    "YamlDotNet|16.2.0"
    "HtmlAgilityPack|1.11.72"
    "Markdig|0.40.0"
    "SharpCompress|0.39.0"
    "System.IO.Hashing|8.0.0"

    # .NET Framework compatibility shims
    "System.Memory|4.5.5"
    "System.Buffers|4.5.1"
    "System.Runtime.CompilerServices.Unsafe|6.0.0"
    "System.Numerics.Vectors|4.5.0"
    "Microsoft.Bcl.HashCode|1.1.1"
    "System.Collections.Immutable|8.0.0"

    # ML / ONNX extras (on-device only)
    "Microsoft.ML.OnnxRuntime.Managed|1.20.1"
    "Microsoft.ML.ImageAnalytics|3.0.1"
    "Microsoft.ML.TimeSeries|3.0.1"

    # WinForms / desktop polish (Admin / Viewer)
    "Serilog.Enrichers.Thread|4.0.0"
    "Serilog.Settings.Configuration|8.0.4"
    "Serilog.Formatting.Compact|3.0.0"
    "CommandLineParser|2.9.1"
    "Spectre.Console|0.49.1"

    # Framework / encoding / compatibility (net48 airgap build)
    "Microsoft.NETFramework.ReferenceAssemblies.net48|1.0.3"
    "System.Text.Encoding.CodePages|8.0.0"
    "System.Configuration.ConfigurationManager|8.0.1"
    "System.Diagnostics.DiagnosticSource|8.0.1"
    "System.Threading.Channels|8.0.0"
    "Microsoft.Windows.Compatibility|8.0.11"
    "Microsoft.IO.RecyclableMemoryStream|3.0.1"
    "System.IO.Hashing|8.0.0"

    # SQLite extras (Core already uses System.Data.SQLite)
    "System.Data.SQLite.Core|1.0.119"
    "Microsoft.Data.Sqlite|8.0.11"

    # Local HTML preview in Viewer / Admin (no CDN)
    "Microsoft.Web.WebView2|1.0.2849.39"

    # Charts in WinForms (optional dashboards)
    "ScottPlot|4.1.74"
    "ScottPlot.WinForms|4.1.74"
    "OxyPlot.WindowsForms|2.2.0"

    # Excel / PDF / zip (future reports)
    "MiniExcel|1.36.0"
    "ExcelNumberFormat|1.1.0"
    "SharpZipLib|1.4.2"
    "PdfSharp|1.50.5147"

    # Parsing / mapping (local files only)
    "AngleSharp|1.2.0"
    "AutoMapper|12.0.1"
    "NodaTime|3.2.0"
    "TimeZoneConverter|6.1.0"
    "MathNet.Numerics|5.0.0"
    "DiffPlex|1.7.2"

    # Quartz extras (Admin scheduler)
    "Quartz.Serialization.Json|3.15.1"
    "Quartz.Plugins.TimeZoneConverter|3.15.1"

    # ML extras (on-device; no cloud)
    "Microsoft.ML.DataView|3.0.1"
    "Microsoft.ML.CpuMath|3.0.1"
    "Microsoft.ML.FastTree|3.0.1"
    "Microsoft.ML.LightGbm|3.0.1"
    "Microsoft.ML.OnnxTransformer|3.0.1"

    # NuGet itself (restore tooling on intranet PC)
    "NuGet.CommandLine|6.12.1"
)

$packages = @()
if ($Current) { $packages += $currentPackages }
if ($Future) { $packages += $futurePackages }
$packages = $packages | Select-Object -Unique

$failed = @()
$ok = 0
foreach ($entry in $packages) {
    $parts = $entry.Split("|")
    $id = $parts[0]
    $ver = $parts[1]
    Write-Host ("  nuget install {0} -Version {1}" -f $id, $ver)
    & $nugetCmd install $id -Version $ver -OutputDirectory $NuGetDir -NonInteractive -Source $Source
    if ($LASTEXITCODE -ne 0) {
        Write-Warning ("FAILED: {0} {1}" -f $id, $ver)
        $failed += $entry
    }
    else { $ok++ }
}

Write-Host ""
Write-Host "=== NuGet Done ===" -ForegroundColor Green
Write-Host ("OK: {0}  Failed: {1}  Folder: {2}" -f $ok, $failed.Count, $NuGetDir)
Write-Host "nuget.config OfflineDev source should point to: $NuGetDir"

if ($failed.Count -gt 0) {
    Write-Host "Failed packages (re-run later):" -ForegroundColor Yellow
    $failed | ForEach-Object { Write-Host ("  - {0}" -f $_) }
    exit 2
}
