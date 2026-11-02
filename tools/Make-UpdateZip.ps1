<#
  마지막 배포 이후 바뀐 파일만 모아 zip 한 개로 만든다.
  사용:  .\tools\Make-UpdateZip.ps1 -From <이전 배포 커밋 또는 태그> -Name 2026-11-02_web-voc
  결과:  updates\<Name>\PocTrace-update-<Name>.zip  +  CHANGED-FILES.md
  zip 안은 저장소 상대 경로 그대로다. 소스는 빌드가 필요하고, wwwroot 파일은 exe 옆 wwwroot에 덮어쓴다.
  git 이 설치된 PC에서 저장소 루트 어디서나 실행할 수 있다.
#>
param(
  [Parameter(Mandatory = $true)][string]$From,
  [string]$To = 'HEAD',
  [Parameter(Mandatory = $true)][string]$Name
)

$ErrorActionPreference = 'Stop'
$root = (& git rev-parse --show-toplevel).Trim()
Set-Location $root

$outDir = Join-Path $root ('updates\' + $Name)
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$zipPath = Join-Path $outDir ('PocTrace-update-' + $Name + '.zip')
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

$raw = & git diff --name-status --no-renames "$From..$To" -- . ':(exclude)updates' ':(exclude).cursor'
$changed = @()
$deleted = @()
foreach ($line in $raw) {
  if ([string]::IsNullOrWhiteSpace($line)) { continue }
  $parts = $line -split "`t", 2
  if ($parts.Count -lt 2) { continue }
  $item = New-Object psobject -Property @{ Status = $parts[0]; Path = $parts[1] }
  if ($parts[0] -eq 'D') { $deleted += $item } else { $changed += $item }
}
if ($changed.Count -eq 0 -and $deleted.Count -eq 0) { throw "$From 이후 바뀐 파일이 없습니다." }

function Get-Category([string]$path) {
  if ($path -match '/wwwroot/') { return 'wwwroot (exe 옆 wwwroot에 덮어쓰기)' }
  if ($path -match '\.(cs|csproj|config|sln|xml|props|targets)$') { return '소스 (빌드 필요)' }
  if ($path -match '^docs/|\.md$|\.csv$') { return '문서' }
  return '도구/기타'
}

if ($changed.Count -gt 0) {
  $paths = @($changed | ForEach-Object { $_.Path })
  & git archive --format=zip --output="$zipPath" $To -- $paths
  if ($LASTEXITCODE -ne 0) { throw 'git archive 실패' }
}

$tz = [System.TimeZoneInfo]::FindSystemTimeZoneById('China Standard Time')
$rows = @()
foreach ($item in ($changed | Sort-Object Path)) {
  $stamp = (& git log -1 --format=%cI $To -- $item.Path).Trim()
  $local = [System.TimeZoneInfo]::ConvertTime([DateTimeOffset]::Parse($stamp), $tz).ToString('yyyy-MM-dd HH:mm')
  $label = if ($item.Status -eq 'A') { '추가' } else { '수정' }
  $rows += ('| `' + $item.Path + '` | ' + $label + ' | ' + (Get-Category $item.Path) + ' | ' + $local + ' |')
}

$head = (& git rev-parse --short $To).Trim()
$base = (& git rev-parse --short $From).Trim()
$md = @()
$md += "# 업데이트 $Name"
$md += ''
$md += "- 기준: ``$base`` 이후 ``$head`` 까지"
$md += "- 파일 수: 변경 $($changed.Count)개, 삭제 $($deleted.Count)개"
$md += '- 시각은 중국 표준시(UTC+8) 기준 마지막 커밋 시각입니다.'
$md += ''
$md += '## 적용 순서'
$md += ''
$md += '1. 소스 파일을 저장소에 덮어쓰고 **PocTrace.Web을 Release로 빌드**합니다. 새 .cs 파일은 csproj에 이미 포함되어 있습니다.'
$md += '2. 공유 폴더의 `PocTrace.Web.exe`를 새 빌드로 바꾸고, `wwwroot` 파일을 같은 상대 경로로 덮어씁니다.'
$md += '3. `PocTrace.Web.exe.config`에 새 키가 있으면 추가합니다 (이 문서의 파일 목록에 `App.config`가 있으면 확인).'
$md += '4. 폴더에 `EPPlus.dll`, `Serilog.dll` 등 NuGet DLL이 남아 있으면 지웁니다 (docs/SVDI-SHARED-RUN.md).'
$md += ''
$md += '## 바뀐 파일'
$md += ''
$md += '| 파일 | 상태 | 구분 | 마지막 수정 |'
$md += '|------|------|------|-------------|'
$md += $rows
if ($deleted.Count -gt 0) {
  $md += ''
  $md += '## 삭제된 파일 (직접 지우세요)'
  $md += ''
  foreach ($item in $deleted) { $md += ('- `' + $item.Path + '`') }
}
$manifest = Join-Path $outDir 'CHANGED-FILES.md'
[System.IO.File]::WriteAllText($manifest, ($md -join "`r`n") + "`r`n", (New-Object System.Text.UTF8Encoding($true)))

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, 'Update')
try {
  [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $manifest, 'CHANGED-FILES.md')
} finally {
  $zip.Dispose()
}

Write-Host "만들었습니다: $zipPath"
Write-Host "목록: $manifest"
Write-Host 'updates\README.md 의 표에 한 줄을 추가하고 커밋·푸시하세요.'
