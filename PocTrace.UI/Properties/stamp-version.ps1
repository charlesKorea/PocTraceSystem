param(
    [Parameter(Mandatory = $true)][string]$Dir,
    [switch]$Bump
)

$txt = Join-Path $Dir "AppVersion.txt"
$cs = Join-Path $Dir "AppVersion.g.cs"
if (-not (Test-Path $txt)) {
    Set-Content -Path $txt -Value "1.001" -Encoding ascii
}

function Format-Version([int]$major, [int]$minor) {
    return "{0}.{1:000}" -f $major, $minor
}

function Read-Version([string]$raw) {
    $raw = $raw.Trim()
    $parts = $raw.Split(".")
    $major = 1
    $minor = 1
    if ($parts.Length -ge 1) { [void][int]::TryParse($parts[0], [ref]$major) }
    if ($parts.Length -ge 2) { [void][int]::TryParse($parts[1], [ref]$minor) }
    if ($major -lt 0) { $major = 0 }
    if ($major -gt 9) { $major = 9 }
    if ($minor -lt 0) { $minor = 0 }
    if ($minor -gt 999) { $minor = 999 }
    return @($major, $minor)
}

$current = Read-Version (Get-Content -Path $txt -Raw)
$major = $current[0]
$minor = $current[1]
$text = Format-Version $major $minor

if ($Bump) {
    $minor++
    if ($minor -gt 999) {
        $minor = 0
        $major++
        if ($major -gt 9) { $major = 9; $minor = 999 }
    }
    $next = Format-Version $major $minor
    Set-Content -Path $txt -Value $next -Encoding ascii
    exit 0
}

$body = @"
using System.Reflection;

[assembly: AssemblyInformationalVersion("$text")]

namespace PocTrace.UI
{
    internal static class AppVersion
    {
        public const string Text = "$text";
    }
}
"@

$existing = ""
if (Test-Path $cs) { $existing = Get-Content -Path $cs -Raw }
if ($existing -ne $body) {
    Set-Content -Path $cs -Value $body -Encoding ascii
}
