# Enrich preview REPORT/HISTORY JS with demo HookupStats + clamped AvgPassRate (0~100).
# String-only patches (no ConvertTo-Json) to avoid PS array unwrap / encoding issues.

param(
  [string]$PreviewRoot = "C:\OfflineDev\html-preview"
)

$ErrorActionPreference = "Stop"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

function Clamp100([double]$v) {
  if ($v -lt 0) { return 0 }
  if ($v -gt 100) { return 100 }
  return [math]::Round($v, 1)
}

function Add-HistoryAvgFields([string]$text, [double]$hookupRateBase) {
  # Only touches ...RatePoc,IssuePocCount} — already-enriched rows are skipped by pattern
  $pattern = '"RatePoc":(\d+(?:\.\d+)?),"IssuePocCount":(\d+)\}'
  $rx = [regex]$pattern
  if (-not $rx.IsMatch($text)) { return $text }

  $sb = New-Object System.Text.StringBuilder
  $last = 0
  $i = 0
  foreach ($m in $rx.Matches($text)) {
    [void]$sb.Append($text.Substring($last, $m.Index - $last))
    $ratePoc = [double]$m.Groups[1].Value
    $issues = $m.Groups[2].Value
    $hu = Clamp100 ($hookupRateBase + (($i % 5) - 2) * 1.2)
    $avg = Clamp100 (($ratePoc + $hu) / 2.0)
    $rateStr = $m.Groups[1].Value
    $repl = '"RatePoc":' + $rateStr + ',"IssuePocCount":' + $issues +
      ',"AvgPassRate":' + $avg + ',"RateHookup":' + $hu + ',"CheckRulesCount":10}'
    [void]$sb.Append($repl)
    $last = $m.Index + $m.Length
    $i++
  }
  [void]$sb.Append($text.Substring($last))
  return $sb.ToString()
}

function Enrich-ReportFile([string]$path, [hashtable]$hookup) {
  if (-not (Test-Path $path)) { return }
  $raw = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)

  if (-not $raw.Contains('"HookupStats"')) {
    $hs = '"HookupStats":{"TotalObjects":' + $hookup.TotalObjects +
      ',"OK":' + $hookup.OK +
      ',"MISSING":' + $hookup.MISSING +
      ',"MISMATCH":' + $hookup.MISMATCH +
      ',"TAG_ERROR":' + $hookup.TAG_ERROR +
      ',"DATA_ERROR":' + $hookup.DATA_ERROR + '}'
    if ($raw -match '"BelongStats"\s*:\s*\{') {
      $raw = [regex]::Replace($raw, '("BelongStats"\s*:\s*\{[^{}]*\})(\s*\})', ('$1,' + $hs + '$2'), 1)
    } else {
      $raw = [regex]::Replace($raw, '(const REPORT_DATA\s*=\s*\{)', ('$1' + $hs + ','), 1)
    }
  }

  if ($raw -match '"BelongStats"\s*:\s*\{[^}]{0,120}\}') {
    $richBelong = '"BelongStats":{"Piping-Design":18420,"SCS":12650,"Construction":9820,"QA":7450,"Equipment-Eng":5239}'
    $raw = [regex]::Replace($raw, '"BelongStats"\s*:\s*\{[^{}]*\}', $richBelong, 1)
  }

  $huRate = 78.0
  if ($hookup.TotalObjects -gt 0) {
    $huRate = 100.0 * $hookup.OK / $hookup.TotalObjects
  }
  $raw = Add-HistoryAvgFields $raw $huRate

  [System.IO.File]::WriteAllText($path, $raw, $utf8NoBom)
  Write-Host "Enriched: $path" -ForegroundColor Cyan
}

function Enrich-SummaryFile([string]$path) {
  if (-not (Test-Path $path)) { return }
  $raw = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)

  if ($raw.Contains('"TotalIssues":54081}') -and -not $raw.Contains('"AvgPassRate":81.2')) {
    $raw = $raw.Replace(
      '"TotalIssues":54081}',
      '"TotalIssues":54081,"HookupTotal":18420,"HookupPassRate":79.7,"AvgPassRate":81.2,"CheckRulesCount":10}'
    )
  }

  $raw = Add-HistoryAvgFields $raw 79.7

  [System.IO.File]::WriteAllText($path, $raw, $utf8NoBom)
  Write-Host "Enriched: $path" -ForegroundColor Cyan
}

Enrich-ReportFile (Join-Path $PreviewRoot "data\data_x1.js") @{
  TotalObjects = 18420; OK = 14680; MISSING = 2140; MISMATCH = 980; TAG_ERROR = 420; DATA_ERROR = 200
}
Enrich-ReportFile (Join-Path $PreviewRoot "data\data_x2.js") @{
  TotalObjects = 9200; OK = 7100; MISSING = 1100; MISMATCH = 600; TAG_ERROR = 280; DATA_ERROR = 120
}
Enrich-ReportFile (Join-Path $PreviewRoot "data\data_x2_2.js") @{
  TotalObjects = 6400; OK = 5100; MISSING = 700; MISMATCH = 380; TAG_ERROR = 140; DATA_ERROR = 80
}
Enrich-SummaryFile (Join-Path $PreviewRoot "data\data_summary.js")

# 레거시 preview 루트 파일도 있으면 보강
Enrich-ReportFile (Join-Path $PreviewRoot "data_x1.js") @{
  TotalObjects = 18420; OK = 14680; MISSING = 2140; MISMATCH = 980; TAG_ERROR = 420; DATA_ERROR = 200
}
Enrich-SummaryFile (Join-Path $PreviewRoot "data_summary.js")

Write-Host "Demo data enrichment done." -ForegroundColor Green
