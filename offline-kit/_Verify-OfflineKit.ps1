$report = @()
function Add($s) { $script:report += $s; Write-Output $s }

Add "===== 1. NuGet packages under C:\OfflineDev\nuget-packages ====="
$nugetRoot = "C:\OfflineDev\nuget-packages"
if (-not (Test-Path $nugetRoot)) {
  Add "FAIL: folder missing"
} else {
  $dirs = Get-ChildItem $nugetRoot -Directory | Select-Object -ExpandProperty Name
  Add ("Count dirs: {0}" -f $dirs.Count)
  $dirs | Sort-Object | ForEach-Object { Add ("  {0}" -f $_) }
}

$required = @(
  "Dapper.2.1.66",
  "EPPlus.7.2.1",
  "LargeXlsx.1.10.0",
  "Newtonsoft.Json.13.0.4",
  "Serilog.4.3.0",
  "Serilog.Sinks.File.7.0.0",
  "System.Data.SqlClient.4.8.6",
  "System.Data.SQLite.2.0.2",
  "Quartz.3.15.1",
  "Costura.Fody.6.0.0",
  "Fody.6.8.2",
  "MetroFramework.1.2.0.3",
  "MetroModernUI.1.4.0",
  "Bogus.35.6.5",
  "Microsoft.ML.3.0.1",
  "Microsoft.ML.OnnxRuntime.1.20.1"
)

Add ""
Add "===== 2. Required package folders (exact or prefix match) ====="
foreach ($r in $required) {
  $hit = Get-ChildItem $nugetRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -eq $r -or $_.Name.StartsWith(($r -replace '\.\d+$','')) -or $_.Name -like ($r.Split('.')[0] + "*") }
  # simpler: name starts with package id before version
  $id = ($r -split '\.(?=\d)')[0]
  # better approach for Id.Version folders
  $found = Test-Path (Join-Path $nugetRoot $r)
  if (-not $found) {
    $found = @(Get-ChildItem $nugetRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "$($r.Substring(0, [Math]::Min(20,$r.Length)))*" }).Count -gt 0
    # match by prefix of package name
    $pkgId = $r -replace '\.\d+(\.\d+)*$',''
    # Dapper.2.1.66 -> Dapper ; Microsoft.ML.3.0.1 is tricky
  }
}

# Precise check: nuget install creates PackageId.Version folders
function Test-Pkg($id, $ver) {
  $p1 = Join-Path $nugetRoot "$id.$ver"
  if (Test-Path $p1) { return $true }
  # sometimes lowercase
  $hits = Get-ChildItem $nugetRoot -Directory -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -ieq "$id.$ver" -or ($_.Name -ilike "$id.*" -and $_.Name -match [regex]::Escape($ver))
  }
  return ($hits.Count -gt 0)
}

$checks = @(
  @{Id="Dapper"; Ver="2.1.66"},
  @{Id="EPPlus"; Ver="7.2.1"},
  @{Id="LargeXlsx"; Ver="1.10.0"},
  @{Id="Newtonsoft.Json"; Ver="13.0.4"},
  @{Id="Serilog"; Ver="4.3.0"},
  @{Id="Serilog.Sinks.File"; Ver="7.0.0"},
  @{Id="System.Data.SqlClient"; Ver="4.8.6"},
  @{Id="System.Data.SQLite"; Ver="2.0.2"},
  @{Id="Quartz"; Ver="3.15.1"},
  @{Id="Costura.Fody"; Ver="6.0.0"},
  @{Id="Fody"; Ver="6.8.2"},
  @{Id="MetroFramework"; Ver="1.2.0.3"},
  @{Id="MetroModernUI"; Ver="1.4.0"},
  @{Id="Bogus"; Ver="35.6.5"},
  @{Id="Microsoft.Bcl.AsyncInterfaces"; Ver="8.0.0"},
  @{Id="System.Threading.Tasks.Extensions"; Ver="4.5.4"},
  @{Id="System.ValueTuple"; Ver="4.5.0"},
  @{Id="Microsoft.ML"; Ver="3.0.1"},
  @{Id="Microsoft.ML.OnnxRuntime"; Ver="1.20.1"}
)

foreach ($c in $checks) {
  $path = Join-Path $nugetRoot ("{0}.{1}" -f $c.Id, $c.Ver)
  $ok = Test-Path $path
  if (-not $ok) {
    $alt = Get-ChildItem $nugetRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -ieq ("{0}.{1}" -f $c.Id, $c.Ver) }
    $ok = $null -ne $alt
  }
  if ($ok) { Add ("OK   {0}.{1}" -f $c.Id, $c.Ver) }
  else {
    $any = Get-ChildItem $nugetRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like ($c.Id + ".*") } | Select-Object -First 3 -ExpandProperty Name
    if ($any) { Add ("MISS {0}.{1}  (found similar: {2})" -f $c.Id, $c.Ver, ($any -join ", ")) }
    else { Add ("MISS {0}.{1}" -f $c.Id, $c.Ver) }
  }
}

Add ""
Add "===== 3. Assets: C:\OfflineDev\web-assets ====="
$wa = "C:\OfflineDev\web-assets"
if (Test-Path $wa) {
  Get-ChildItem $wa -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
    Add ("  {0}" -f $_.FullName.Substring($wa.Length+1))
  }
} else { Add "MISS folder" }

Add ""
Add "===== 4. Assets: Templates\assets ====="
$ta = "C:\JCS\1. Codes\260718\PocTraceSystem\PocTrace.Core\Templates\assets"
if (Test-Path $ta) {
  Get-ChildItem $ta -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
    Add ("  {0}" -f $_.FullName.Substring($ta.Length+1))
  }
} else { Add "MISS folder" }

$needed = @(
  "css\bootstrap.min.css",
  "css\dataTables.bootstrap5.min.css",
  "css\style.css",
  "js\jquery.min.js",
  "js\bootstrap.bundle.min.js",
  "js\echarts.min.js",
  "js\jszip.min.js",
  "js\lodash.min.js",
  "js\dayjs.min.js",
  "js\jquery.dataTables.min.js",
  "js\dataTables.bootstrap5.min.js",
  "js\dataTables.buttons.min.js",
  "js\buttons.html5.min.js",
  "js\lang.js",
  "js\app.js"
)

Add ""
Add "===== 5. layout.html required files in Templates\assets ====="
foreach ($n in $needed) {
  $p = Join-Path $ta $n
  if (Test-Path $p) { Add ("OK   {0}" -f $n) }
  else { Add ("MISS {0}" -f $n) }
}

Add ""
Add "===== 6. Same check in C:\OfflineDev\web-assets ====="
foreach ($n in $needed) {
  $p = Join-Path $wa $n
  if (Test-Path $p) { Add ("OK   {0}" -f $n) }
  else { Add ("MISS {0}" -f $n) }
}

Add ""
Add "===== 7. models ====="
$md = "C:\OfflineDev\models"
if (Test-Path $md) {
  $mf = Get-ChildItem $md -Recurse -File -ErrorAction SilentlyContinue
  if ($mf) { $mf | ForEach-Object { Add ("  {0}" -f $_.Name) } }
  else { Add "  (empty - OK for now, ONNX optional until ML phase)" }
}

Add ""
Add "===== 8. MSBuild / VS ====="
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vswhere) {
  Add (& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath)
  $msb = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
  Add ("MSBuild: {0}" -f $msb)
} else { Add "vswhere not found" }

$out = "C:\JCS\1. Codes\260718\PocTraceSystem\offline-kit\_verify-report.txt"
$report | Set-Content -Path $out -Encoding UTF8
Add ("Report written: {0}" -f $out)
