#Requires -Version 5.1
<#
.SYNOPSIS
  Download recommended local CSS/JS/fonts/emoji into C:\OfflineDev\web-assets
  (no CDN at runtime — airgap / VDI).

.DESCRIPTION
  Layout (agreed):
    C:\OfflineDev\web-assets\
      css\
      js\
      fonts\
      emoji\          # Twemoji assets + helper CSS
      vendor-meta\    # download manifest log

  Optional: -CopyToProject copies into PocTrace.Core\Templates\assets

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-OfflineWebAssets.ps1
  powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-OfflineWebAssets.ps1 -CopyToProject
  powershell -ExecutionPolicy Bypass -File .\offline-kit\Download-OfflineWebAssets.ps1 -EssentialOnly
#>
[CmdletBinding()]
param(
    [string]$Root = "C:\OfflineDev",
    [switch]$CopyToProject,
    [switch]$EssentialOnly,
    [string]$ProjectAssets = ""
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Assets = Join-Path $Root "web-assets"
$Css = Join-Path $Assets "css"
$Js = Join-Path $Assets "js"
$Fonts = Join-Path $Assets "fonts"
$Emoji = Join-Path $Assets "emoji"
$Meta = Join-Path $Assets "vendor-meta"
New-Item -ItemType Directory -Force -Path $Css, $Js, $Fonts, $Emoji, $Meta, (Join-Path $Assets "images") | Out-Null

if (-not $ProjectAssets) {
    $ProjectAssets = Join-Path $PSScriptRoot "..\PocTrace.Core\Templates\assets"
}

function Get-FileSafe {
    param(
        [Parameter(Mandatory)][string]$Url,
        [Parameter(Mandatory)][string]$OutPath,
        [string]$Label = ""
    )
    $dir = Split-Path $OutPath -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }

    if (Test-Path $OutPath) {
        $len = (Get-Item $OutPath).Length
        if ($len -gt 0) {
            Write-Host ("  SKIP (exists {0:N0} bytes): {1}" -f $len, $(if ($Label) { $Label } else { $OutPath })) -ForegroundColor DarkGray
            return $true
        }
    }

    Write-Host ("  GET {0}" -f $(if ($Label) { $Label } else { $Url }))
    try {
        Invoke-WebRequest -Uri $Url -OutFile $OutPath -UseBasicParsing -TimeoutSec 600
        if (-not (Test-Path $OutPath) -or (Get-Item $OutPath).Length -eq 0) {
            throw "Empty download"
        }
        return $true
    }
    catch {
        Write-Warning ("FAILED: {0} -> {1} ({2})" -f $Url, $OutPath, $_.Exception.Message)
        return $false
    }
}

# Url|RelativePath|Label
$essential = @(
    # Core report stack (layout.html)
    "https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css|css/bootstrap.min.css|bootstrap.css"
    "https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js|js/bootstrap.bundle.min.js|bootstrap.js"
    "https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css|css/bootstrap-icons.min.css|bootstrap-icons.css"
    "https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/fonts/bootstrap-icons.woff2|fonts/bootstrap-icons.woff2|bi.woff2"
    "https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/fonts/bootstrap-icons.woff|fonts/bootstrap-icons.woff|bi.woff"
    "https://cdn.jsdelivr.net/npm/jquery@3.7.1/dist/jquery.min.js|js/jquery.min.js|jquery"
    "https://cdn.jsdelivr.net/npm/echarts@5.5.1/dist/echarts.min.js|js/echarts.min.js|echarts"
    "https://cdn.jsdelivr.net/npm/lodash@4.17.21/lodash.min.js|js/lodash.min.js|lodash"
    "https://cdn.jsdelivr.net/npm/dayjs@1.11.13/dayjs.min.js|js/dayjs.min.js|dayjs"
    "https://cdn.jsdelivr.net/npm/jszip@3.10.1/dist/jszip.min.js|js/jszip.min.js|jszip"
    "https://cdn.jsdelivr.net/npm/datatables.net@2.1.8/js/dataTables.min.js|js/dataTables.min.js|dataTables"
    "https://cdn.jsdelivr.net/npm/datatables.net@2.1.8/js/dataTables.min.js|js/jquery.dataTables.min.js|dataTables alias"
    "https://cdn.jsdelivr.net/npm/datatables.net-bs5@2.1.8/js/dataTables.bootstrap5.min.js|js/dataTables.bootstrap5.min.js|dt-bs5.js"
    "https://cdn.jsdelivr.net/npm/datatables.net-bs5@2.1.8/css/dataTables.bootstrap5.min.css|css/dataTables.bootstrap5.min.css|dt-bs5.css"
    "https://cdn.jsdelivr.net/npm/datatables.net-buttons@3.1.2/js/dataTables.buttons.min.js|js/dataTables.buttons.min.js|dt-buttons"
    "https://cdn.jsdelivr.net/npm/datatables.net-buttons@3.1.2/js/buttons.html5.min.js|js/buttons.html5.min.js|buttons-html5"
    "https://cdn.jsdelivr.net/npm/datatables.net-buttons@3.1.2/css/buttons.dataTables.min.css|css/buttons.dataTables.min.css|dt-buttons.css"
    "https://cdn.jsdelivr.net/npm/datatables.net-fixedheader@4.0.1/js/dataTables.fixedHeader.min.js|js/dataTables.fixedHeader.min.js|dt-fixedHeader"
    "https://cdn.jsdelivr.net/npm/datatables.net-fixedheader-dt@4.0.1/css/fixedHeader.dataTables.min.css|css/fixedHeader.dataTables.min.css|dt-fixedHeader.css"
    "https://cdn.jsdelivr.net/npm/dayjs@1.11.13/locale/ko.js|js/dayjs-locale-ko.js|dayjs-ko"
    "https://cdn.jsdelivr.net/npm/dayjs@1.11.13/locale/zh-cn.js|js/dayjs-locale-zh-cn.js|dayjs-zh"
    "https://cdn.jsdelivr.net/npm/sweetalert2@11.14.5/dist/sweetalert2.all.min.js|js/sweetalert2.all.min.js|sweetalert2"
    "https://cdn.jsdelivr.net/npm/animate.css@4.1.1/animate.min.css|css/animate.min.css|animate.css"
    "https://cdn.jsdelivr.net/npm/countup.js@2.8.0/dist/countUp.umd.js|js/countUp.min.js|countUp"
)

$futureUi = @(
    # Charts / viz alternatives & extras
    "https://cdn.jsdelivr.net/npm/apexcharts@4.3.0/dist/apexcharts.min.js|js/apexcharts.min.js|apexcharts"
    "https://cdn.jsdelivr.net/npm/apexcharts@4.3.0/dist/apexcharts.css|css/apexcharts.css|apexcharts.css"
    "https://cdn.jsdelivr.net/npm/chart.js@4.4.7/dist/chart.umd.min.js|js/chart.umd.min.js|chart.js (optional)"
    "https://cdn.jsdelivr.net/npm/mermaid@11.4.1/dist/mermaid.min.js|js/mermaid.min.js|mermaid"

    # Tables / grids
    "https://cdn.jsdelivr.net/npm/tabulator-tables@6.3.0/dist/js/tabulator.min.js|js/tabulator.min.js|tabulator"
    "https://cdn.jsdelivr.net/npm/tabulator-tables@6.3.0/dist/css/tabulator.min.css|css/tabulator.min.css|tabulator.css"
    "https://cdn.jsdelivr.net/npm/tabulator-tables@6.3.0/dist/css/tabulator_bootstrap5.min.css|css/tabulator_bootstrap5.min.css|tabulator-bs5"

    # Date / select / tips
    "https://cdn.jsdelivr.net/npm/flatpickr@4.6.13/dist/flatpickr.min.js|js/flatpickr.min.js|flatpickr"
    "https://cdn.jsdelivr.net/npm/flatpickr@4.6.13/dist/flatpickr.min.css|css/flatpickr.min.css|flatpickr.css"
    "https://cdn.jsdelivr.net/npm/flatpickr@4.6.13/dist/l10n/ko.js|js/flatpickr-ko.js|flatpickr-ko"
    "https://cdn.jsdelivr.net/npm/tom-select@2.4.1/dist/js/tom-select.complete.min.js|js/tom-select.complete.min.js|tom-select"
    "https://cdn.jsdelivr.net/npm/tom-select@2.4.1/dist/css/tom-select.bootstrap5.min.css|css/tom-select.bootstrap5.min.css|tom-select.css"
    "https://cdn.jsdelivr.net/npm/@popperjs/core@2.11.8/dist/umd/popper.min.js|js/popper.min.js|popper"
    "https://cdn.jsdelivr.net/npm/tippy.js@6.3.7/dist/tippy.umd.min.js|js/tippy.umd.min.js|tippy"
    "https://cdn.jsdelivr.net/npm/tippy.js@6.3.7/dist/tippy.css|css/tippy.css|tippy.css"

    # Light interactivity / markdown / highlight
    "https://cdn.jsdelivr.net/npm/alpinejs@3.14.8/dist/cdn.min.js|js/alpine.min.js|alpine"
    "https://cdn.jsdelivr.net/npm/sortablejs@1.15.6/Sortable.min.js|js/Sortable.min.js|sortable"
    "https://cdn.jsdelivr.net/npm/marked@15.0.4/marked.min.js|js/marked.min.js|marked"
    "https://cdn.jsdelivr.net/npm/highlight.js@11.11.1/lib/common.min.js|js/highlight.min.js|highlight.js"
    "https://cdn.jsdelivr.net/npm/highlight.js@11.11.1/styles/github.min.css|css/highlight-github.min.css|hljs-github"

    # Maps (plant/site plans later)
    "https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/leaflet.js|js/leaflet.js|leaflet"
    "https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/leaflet.css|css/leaflet.css|leaflet.css"
    "https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/images/marker-icon.png|images/marker-icon.png|leaflet marker"
    "https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/images/marker-icon-2x.png|images/marker-icon-2x.png|leaflet marker2x"
    "https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/images/marker-shadow.png|images/marker-shadow.png|leaflet shadow"
    "https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/images/layers.png|images/layers.png|leaflet layers"
    "https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/images/layers-2x.png|images/layers-2x.png|leaflet layers2x"

    # Excel-in-browser / PDF helper (DataTables buttons future)
    "https://cdn.jsdelivr.net/npm/xlsx@0.18.5/dist/xlsx.full.min.js|js/xlsx.full.min.js|sheetjs"
    "https://cdn.jsdelivr.net/npm/pdfmake@0.2.18/build/pdfmake.min.js|js/pdfmake.min.js|pdfmake"
    "https://cdn.jsdelivr.net/npm/pdfmake@0.2.18/build/vfs_fonts.js|js/vfs_fonts.js|pdfmake-fonts"
)

# Emoji: Twemoji (SVG) — Unicode emoji that break with Pretendard/MiSans
$emojiAssets = @(
    "https://cdn.jsdelivr.net/npm/@twemoji/api@15.1.0/dist/twemoji.min.js|emoji/twemoji.min.js|twemoji.js"
    "https://cdn.jsdelivr.net/npm/twemoji@14.0.2/dist/twemoji.min.js|emoji/twemoji.legacy.min.js|twemoji.legacy (fallback)"
)

# Color emoji font (large) — covers 📊🔧 etc. when font-family includes it
$emojiFonts = @(
    # Google Noto Color Emoji (COLRv1) — github raw may redirect; jsDelivr npm mirror
    "https://cdn.jsdelivr.net/npm/@fontsource/noto-color-emoji@5.1.1/files/noto-color-emoji-emoji-400-normal.woff|fonts/NotoColorEmoji.woff|NotoColorEmoji.woff"
)

# Korean / CJK UI fonts (if missing)
$uiFonts = @(
    "https://cdn.jsdelivr.net/npm/pretendard@1.3.9/dist/web/static/woff2/Pretendard-Regular.woff2|fonts/Pretendard-Regular.woff2|Pretendard-Regular"
    "https://cdn.jsdelivr.net/npm/pretendard@1.3.9/dist/web/static/woff2/Pretendard-Bold.woff2|fonts/Pretendard-Bold.woff2|Pretendard-Bold"
    "https://cdn.jsdelivr.net/npm/pretendard@1.3.9/dist/web/static/woff2/Pretendard-Medium.woff2|fonts/Pretendard-Medium.woff2|Pretendard-Medium"
    "https://cdn.jsdelivr.net/npm/pretendard@1.3.9/dist/web/static/woff2/Pretendard-SemiBold.woff2|fonts/Pretendard-SemiBold.woff2|Pretendard-SemiBold"
)

$queue = @()
$queue += $essential
$queue += $emojiAssets
$queue += $emojiFonts
$queue += $uiFonts
if (-not $EssentialOnly) { $queue += $futureUi }

$ok = 0
$fail = 0
$log = New-Object System.Collections.Generic.List[string]
$log.Add("# Downloaded $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")

foreach ($entry in $queue) {
    $parts = $entry.Split("|")
    $url = $parts[0]
    $rel = $parts[1]
    $label = if ($parts.Count -gt 2) { $parts[2] } else { $rel }
    $out = Join-Path $Assets $rel
    if (Get-FileSafe -Url $url -OutPath $out -Label $label) {
        $ok++
        $log.Add("OK|$rel|$url")
    }
    else {
        $fail++
        $log.Add("FAIL|$rel|$url")
    }
}

# Local helper CSS: emoji font stack + Twemoji parse hint (no CDN)
$emojiCss = Join-Path $Css "emoji-support.css"
$emojiCssBody = @"
/* Offline emoji support — generated by Download-OfflineWebAssets.ps1
   1) Prefer Twemoji images (see emoji/twemoji.min.js + parse on DOMContentLoaded)
   2) Fallback: Noto Color Emoji font for raw Unicode glyphs (📊 🔧 etc.)
*/
@font-face {
  font-family: "Noto Color Emoji";
  src: url("../fonts/NotoColorEmoji.woff") format("woff");
  font-weight: 400;
  font-style: normal;
  font-display: swap;
}

/* Append after body font stack in style.css / portal_theme.css:
   font-family: 'Pretendard', 'MiSans', 'Noto Color Emoji', 'Segoe UI Emoji', sans-serif;
*/
.emoji-font,
body.emoji-enabled {
  font-family: 'Pretendard', 'MiSans', 'Noto Color Emoji', 'Segoe UI Emoji', 'Apple Color Emoji', sans-serif;
}

img.emoji {
  height: 1.2em;
  width: 1.2em;
  margin: 0 0.05em 0 0.1em;
  vertical-align: -0.1em;
}
"@
Set-Content -Path $emojiCss -Value $emojiCssBody -Encoding UTF8
$log.Add("OK|css/emoji-support.css|(generated)")

# Twemoji init snippet (local assets only — parse uses CDN images by default;
# override to jsDelivr path that we document; for full airgap use SVG base later)
$twemojiInit = Join-Path $Js "twemoji-init.js"
$twemojiInitBody = @"
/* Offline Twemoji bootstrap — include AFTER emoji/twemoji.min.js
   Usage in layout.html (after body content):
     <script src="assets/emoji/twemoji.min.js"></script>
     <script src="assets/js/twemoji-init.js"></script>
   Note: default Twemoji CDN for images needs network OR set base to local SVG folder.
   For true airgap, download Twemoji SVG zip and set:
     twemoji.base = 'assets/emoji/svg/';
*/
(function () {
  function run() {
    if (typeof twemoji === 'undefined') return;
    twemoji.parse(document.body, {
      folder: 'svg',
      ext: '.svg',
      // Airgap: local SVG folder under web-assets/emoji/svg (downloaded by this script).
      base: 'assets/emoji/'
    });
  }
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', run);
  } else {
    run();
  }
})();
"@
Set-Content -Path $twemojiInit -Value $twemojiInitBody -Encoding UTF8
$log.Add("OK|js/twemoji-init.js|(generated)")

# Fix bootstrap-icons.css url paths to ../fonts/ (our layout)
$biCss = Join-Path $Css "bootstrap-icons.min.css"
if (Test-Path $biCss) {
    $text = Get-Content $biCss -Raw -Encoding UTF8
    $text = $text -replace 'url\("?\.?/?fonts/', 'url("../fonts/'
    $text = $text -replace "url\('?\.?/?fonts/", "url('../fonts/"
    # common npm package paths
    $text = $text -replace 'url\("?\./fonts/', 'url("../fonts/'
    Set-Content -Path $biCss -Value $text -Encoding UTF8 -NoNewline
    Copy-Item -Force $biCss (Join-Path $Css "bootstrap-icons.css")
    $log.Add("OK|css/bootstrap-icons.css|(alias+path fix)")
}

$manifestPath = Join-Path $Meta ("download-log-{0}.txt" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
$log | Set-Content -Path $manifestPath -Encoding UTF8

# Twemoji SVG pack for true airgap (npm tarball has JS only; GitHub zip has assets/svg)
$twemojiZip = Join-Path $Meta "twemoji-15.1.0.zip"
$twemojiSvgDir = Join-Path $Emoji "svg"
if (-not (Test-Path (Join-Path $twemojiSvgDir "1f4ca.svg"))) {
    Write-Host "  GET twemoji SVG pack (GitHub zip)"
    try {
        if (Get-FileSafe -Url "https://codeload.github.com/jdecked/twemoji/zip/refs/tags/v15.1.0" -OutPath $twemojiZip -Label "twemoji.zip") {
            $extractRoot = Join-Path $Meta "twemoji-extract"
            if (Test-Path $extractRoot) { Remove-Item -Recurse -Force $extractRoot }
            New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
            Expand-Archive -LiteralPath $twemojiZip -DestinationPath $extractRoot -Force
            $svgSrc = Get-ChildItem $extractRoot -Recurse -Directory -Filter "svg" |
                Where-Object { $_.FullName -match '\\assets\\svg$' } |
                Select-Object -First 1
            if ($svgSrc -and (Test-Path $svgSrc.FullName)) {
                New-Item -ItemType Directory -Force -Path $twemojiSvgDir | Out-Null
                Copy-Item -Recurse -Force (Join-Path $svgSrc.FullName "*") $twemojiSvgDir
                $count = (Get-ChildItem $twemojiSvgDir -File).Count
                $log.Add("OK|emoji/svg|$count files from twemoji-15.1.0.zip")
                Write-Host ("  Twemoji SVG copied: {0} files" -f $count) -ForegroundColor Green
            }
            else {
                Write-Warning "twemoji zip extracted but assets/svg folder not found"
                $log.Add("FAIL|emoji/svg|zip layout unexpected")
                $fail++
            }
        }
        else {
            $fail++
        }
    }
    catch {
        Write-Warning ("twemoji SVG pack failed: {0}" -f $_.Exception.Message)
        $log.Add("FAIL|emoji/svg|$($_.Exception.Message)")
        $fail++
    }
}
else {
    Write-Host "  SKIP twemoji SVG (already present)" -ForegroundColor DarkGray
}

# Pretendard @font-face helper (local only)
$pretendardCss = Join-Path $Css "pretendard-local.css"
$pretendardCssBody = @"
@font-face {
  font-family: "Pretendard";
  src: url("../fonts/Pretendard-Regular.woff2") format("woff2");
  font-weight: 400;
  font-style: normal;
  font-display: swap;
}
@font-face {
  font-family: "Pretendard";
  src: url("../fonts/Pretendard-Medium.woff2") format("woff2");
  font-weight: 500;
  font-style: normal;
  font-display: swap;
}
@font-face {
  font-family: "Pretendard";
  src: url("../fonts/Pretendard-SemiBold.woff2") format("woff2");
  font-weight: 600;
  font-style: normal;
  font-display: swap;
}
@font-face {
  font-family: "Pretendard";
  src: url("../fonts/Pretendard-Bold.woff2") format("woff2");
  font-weight: 700;
  font-style: normal;
  font-display: swap;
}
"@
Set-Content -Path $pretendardCss -Value $pretendardCssBody -Encoding UTF8
$log.Add("OK|css/pretendard-local.css|(generated)")
$log | Set-Content -Path $manifestPath -Encoding UTF8


Write-Host ""
Write-Host "=== Web assets Done ===" -ForegroundColor Green
Write-Host ("OK: {0}  Failed: {1}" -f $ok, $fail)
Write-Host "Root: $Assets"
Write-Host "Log:  $manifestPath"
Write-Host ""
Write-Host "Emoji tip: link css/emoji-support.css and prefer Twemoji or Noto Color Emoji." -ForegroundColor Cyan
Write-Host "  Pretendard alone does not cover 📊🔧 — that is why glyphs break."

if ($CopyToProject) {
    foreach ($sub in @("css", "js", "fonts", "emoji", "images")) {
        $dest = Join-Path $ProjectAssets $sub
        New-Item -ItemType Directory -Force -Path $dest | Out-Null
        $src = Join-Path $Assets $sub
        if (Test-Path $src) {
            Get-ChildItem $src -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
                $rel = $_.FullName.Substring($src.Length).TrimStart("\", "/")
                $target = Join-Path $dest $rel
                $targetDir = Split-Path $target -Parent
                if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Force -Path $targetDir | Out-Null }
                Copy-Item -Force $_.FullName $target
            }
        }
    }
    Write-Host ""
    Write-Host "Copied into project: $ProjectAssets" -ForegroundColor Green
}

if ($fail -gt 0) { exit 2 }
