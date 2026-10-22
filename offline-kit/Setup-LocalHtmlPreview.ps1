# Build local HTML preview from Templates SoT + OfflineDev data.
#   powershell -ExecutionPolicy Bypass -File .\offline-kit\Setup-LocalHtmlPreview.ps1

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
$templatesAssets = Join-Path $repoRoot "PocTrace.Core\Templates\assets"
$templatesLayout = Join-Path $repoRoot "PocTrace.Core\Templates\layout.html"
$webAssets = "C:\OfflineDev\web-assets"
$preview = "C:\OfflineDev\html-preview"

if (-not (Test-Path $templatesAssets)) { throw "Missing Templates assets: $templatesAssets" }

New-Item -ItemType Directory -Force -Path "$preview\assets\css","$preview\assets\js","$preview\assets\fonts","$preview\assets\emoji" | Out-Null

# 1) Templates = SoT for css/js/fonts
Copy-Item -Force -Recurse "$templatesAssets\css\*" "$preview\assets\css\"
Copy-Item -Force -Recurse "$templatesAssets\js\*"  "$preview\assets\js\"
if (Test-Path "$templatesAssets\fonts") {
  Copy-Item -Force "$templatesAssets\fonts\*" "$preview\assets\fonts\"
}

# 2) Optional extras from OfflineDev\web-assets (do not overwrite app.js/lang.js/style)
if (Test-Path $webAssets) {
  Get-ChildItem "$webAssets\css" -File -ErrorAction SilentlyContinue | ForEach-Object {
    if ($_.Name -notin @('style.css','bootstrap-icons.css','bootstrap-icons.min.css')) {
      Copy-Item -Force $_.FullName "$preview\assets\css\"
    }
  }
  Get-ChildItem "$webAssets\js" -File -ErrorAction SilentlyContinue | ForEach-Object {
    # Templates SoT 보호: app/lang + poc-* 모듈은 web-assets로 덮어쓰지 않음
    if ($_.Name -match '^(app|lang|poc-).*\.js$') { return }
    Copy-Item -Force $_.FullName "$preview\assets\js\"
  }
  if (Test-Path "$webAssets\fonts") {
    Copy-Item -Force "$webAssets\fonts\*" "$preview\assets\fonts\" -ErrorAction SilentlyContinue
  }
}

# 3) Normalize bootstrap-icons font urls -> ../fonts/
foreach ($cssName in @('bootstrap-icons.css','bootstrap-icons.min.css')) {
  $p = Join-Path $preview "assets\css\$cssName"
  if (Test-Path $p) {
    $t = Get-Content $p -Raw -Encoding UTF8
    $fixed = $t `
      -replace 'url\("fonts/', 'url("../fonts/' `
      -replace "url\('fonts/", "url('../fonts/" `
      -replace 'url\(fonts/', 'url(../fonts/' `
      -replace 'url\("\./fonts/', 'url("../fonts/'
    if ($fixed -ne $t) {
      Set-Content -Path $p -Value $fixed -Encoding UTF8 -NoNewline
      Write-Host "Normalized font paths: $cssName" -ForegroundColor Cyan
    }
  }
}

# aliases
if ((Test-Path "$preview\assets\js\dataTables.min.js") -and -not (Test-Path "$preview\assets\js\jquery.dataTables.min.js")) {
  Copy-Item -Force "$preview\assets\js\dataTables.min.js" "$preview\assets\js\jquery.dataTables.min.js"
}
if ((Test-Path "$preview\assets\css\bootstrap-icons.min.css") -and -not (Test-Path "$preview\assets\css\bootstrap-icons.css")) {
  Copy-Item -Force "$preview\assets\css\bootstrap-icons.min.css" "$preview\assets\css\bootstrap-icons.css"
}

# 4) Data under preview/data (Admin DailyReport/data 레이아웃과 동일)
$previewData = Join-Path $preview "data"
New-Item -ItemType Directory -Force -Path $previewData | Out-Null
$dataSrc = Join-Path $webAssets "data"
if (Test-Path "$dataSrc\data_summary.js") {
  Copy-Item -Force "$dataSrc\data_summary.js" "$previewData\data_summary.js"
}
Get-ChildItem $dataSrc -Filter "data_*.js" -ErrorAction SilentlyContinue | ForEach-Object {
  Copy-Item -Force $_.FullName (Join-Path $previewData $_.Name)
}
# 레거시: web-assets 루트에 남아 있는 data_*.js 도 수용
Get-ChildItem $webAssets -Filter "data_*.js" -ErrorAction SilentlyContinue | ForEach-Object {
  Copy-Item -Force $_.FullName (Join-Path $previewData $_.Name)
}

# 5) HTML from Templates layout — script src = ./data/...
function New-ReportHtml {
  param([string]$DbName, [string]$ActiveToken, [string]$DataScript, [string]$OutFile)
  $html = Get-Content $templatesLayout -Raw -Encoding UTF8
  $html = $html.Replace('{{TITLE}}', "M-Link : Report [$DbName]")
  $html = $html.Replace('{{DATA_SCRIPT}}', "<script src=`"./data/$DataScript`"></script>")
  if ($ActiveToken) { $html = $html.Replace("{{ACTIVE_$ActiveToken}}", 'active') }
  $html = [regex]::Replace($html, '\{\{ACTIVE_.*?\}\}', '')
  Set-Content -Path (Join-Path $preview $OutFile) -Value $html -Encoding UTF8
}

$index = Get-Content $templatesLayout -Raw -Encoding UTF8
$index = $index.Replace('{{TITLE}}', 'M-Link : Integrated Summary')
$index = $index.Replace('{{DATA_SCRIPT}}', '<script src="./data/data_summary.js"></script>')
$index = $index.Replace('{{ACTIVE_SUMMARY}}', 'active')
$index = [regex]::Replace($index, '\{\{ACTIVE_.*?\}\}', '')
Set-Content -Path "$preview\index.html" -Value $index -Encoding UTF8

# rules.html + data/data_rules.js
$rulesPreviewSrc = Join-Path $templatesAssets "js\data_rules.preview.js"
$rulesJs = Join-Path $previewData "data_rules.js"
if (Test-Path $rulesPreviewSrc) {
  Copy-Item -Force $rulesPreviewSrc $rulesJs
} else {
  Write-Warning "Missing $rulesPreviewSrc"
}

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$rulesHtml = [System.IO.File]::ReadAllText($templatesLayout, [System.Text.Encoding]::UTF8)
$rulesHtml = $rulesHtml.Replace('{{TITLE}}', 'M-Link : Check Rules')
$rulesHtml = $rulesHtml.Replace('{{DATA_SCRIPT}}', '<script src="./data/data_rules.js"></script>')
$rulesHtml = $rulesHtml.Replace('{{ACTIVE_RULES}}', 'active')
$rulesHtml = [regex]::Replace($rulesHtml, '\{\{ACTIVE_.*?\}\}', '')
[System.IO.File]::WriteAllText("$preview\rules.html", $rulesHtml, $utf8NoBom)

# progress.html + data/data_progress.js (demo)
$progressPreviewSrc = Join-Path $templatesAssets "js\data_progress.preview.js"
$progressJs = Join-Path $previewData "data_progress.js"
if (Test-Path $progressPreviewSrc) {
  Copy-Item -Force $progressPreviewSrc $progressJs
} else {
  Write-Warning "Missing $progressPreviewSrc"
}
$progressHtml = [System.IO.File]::ReadAllText($templatesLayout, [System.Text.Encoding]::UTF8)
$progressHtml = $progressHtml.Replace('{{TITLE}}', 'M-Link : Design Progress')
$progressHtml = $progressHtml.Replace('{{DATA_SCRIPT}}', '<script src="./data/data_progress.js"></script>')
$progressHtml = $progressHtml.Replace('{{ACTIVE_PROGRESS}}', 'active')
$progressHtml = [regex]::Replace($progressHtml, '\{\{ACTIVE_.*?\}\}', '')
[System.IO.File]::WriteAllText("$preview\progress.html", $progressHtml, $utf8NoBom)

if (Test-Path "$previewData\data_x1.js") {
  New-ReportHtml -DbName 'x1' -ActiveToken 'X1' -DataScript 'data_x1.js' -OutFile 'report_x1.html'
}
if (Test-Path "$previewData\data_x2.js") {
  New-ReportHtml -DbName 'x2' -ActiveToken 'X2' -DataScript 'data_x2.js' -OutFile 'report_x2.html'
}
if (Test-Path "$previewData\data_x2_2.js") {
  New-ReportHtml -DbName 'x2_2' -ActiveToken 'X2_2' -DataScript 'data_x2_2.js' -OutFile 'report_x2_2.html'
}

# Inject AVAILABLE_LINES / CHECK_RULES into preview data files if missing
$previewLines = '["x1","x2","x2_2"]'
Get-ChildItem $previewData -Filter "data_*.js" | ForEach-Object {
  if ($_.Name -eq 'data_rules.js' -or $_.Name -eq 'data_progress.js' -or $_.Name -like 'data_progress_*.js') { return }
  $content = Get-Content $_.FullName -Raw -Encoding UTF8
  if ($content -notmatch 'AVAILABLE_LINES') {
    if ($content -match 'const IS_SUMMARY') {
      $content = $content -replace 'const IS_SUMMARY', "const AVAILABLE_LINES = $previewLines;`r`nconst IS_SUMMARY"
    } elseif ($content -match 'const REPORT_META') {
      $content = $content -replace '(const REPORT_META = .*?;)', "`$1`r`nconst AVAILABLE_LINES = $previewLines;"
    }
  }
  if ($content -match 'CHECK_RULES_COUNT') {
    $content = $content -replace 'CHECK_RULES_COUNT\s*=\s*\d+', 'CHECK_RULES_COUNT = 10'
  } elseif ($content -match 'const IS_SUMMARY') {
    $content = $content -replace 'const IS_SUMMARY', "const CHECK_RULES_COUNT = 10;`r`nconst IS_SUMMARY"
  }
  if ($content -match 'checkRulesCount') {
    $content = $content -replace 'checkRulesCount\s*:\s*\d+', 'checkRulesCount: 10'
  } elseif ($content -match 'const REPORT_META') {
    $content = $content -replace 'const REPORT_META = \{', 'const REPORT_META = { checkRulesCount: 10, '
  }
  Set-Content -Path $_.FullName -Value $content -Encoding UTF8 -NoNewline
}

# 6) Preview demo enrich: HookupStats + AvgPassRate(0~100) so KPI/charts look full
& (Join-Path $PSScriptRoot "Enrich-PreviewDemoData.ps1") -PreviewRoot $preview

Write-Host "Preview ready: $preview" -ForegroundColor Green
Write-Host "Open: file:///$($preview.Replace('\','/'))/report_x1.html  (Ctrl+F5)"
Write-Host "Data folder: $previewData"
Write-Host "SoT: $templatesAssets"
