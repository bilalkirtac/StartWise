# Packs StartWise as a Claude Desktop extension (.mcpb = zip with manifest.json at the root).
# Run after build.cmd (V21):   powershell -ExecutionPolicy Bypass -File packaging\pack.ps1
# Output: dist\StartWise-<version>.mcpb  -> double-click it (or drag it into Claude Desktop) to install.
# The package carries the C# sources and start.ps1: Windows PowerShell compiles them in memory at start-up,
# so no unsigned StartWise exe is written to the user's disk. Plus manifest, icon and knowledge\ (without generators).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
# Check exactly what ships: start.ps1 compiles src\*.cs in memory and runs the self test.
# No exe is needed (antivirus may remove an unsigned bin\V21\TiaAssistant.exe).
$check = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'start.ps1') --selftest 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { throw "In-memory build / self test failed:`n$check" }
Write-Host ('Self test (in-memory build): ' + $check.Trim())

# Version from the source (ServerVersion) so the package and the exe always agree.
$prog = Get-Content (Join-Path $root 'src\Program.cs') -Raw
$ver = [regex]::Match($prog, 'ServerVersion\s*=\s*"([^"]+)"').Groups[1].Value
if (-not $ver) { throw 'ServerVersion not found in src\Program.cs' }

# Plain text replace (no JSON round trip: Windows PowerShell's ConvertTo-Json rewrites arrays and escapes).
$manifest = Get-Content (Join-Path $PSScriptRoot 'mcpb\manifest.json') -Raw -Encoding UTF8
$manifest = [regex]::Replace($manifest, '"version"\s*:\s*"[^"]*"', ('"version": "' + $ver + '"'), 1)

# Claude plugin: version and release URL of the bundled MCP server follow ServerVersion.
$pj = Join-Path $root 'plugins\startwise\.claude-plugin\plugin.json'
if (Test-Path $pj) {
    $p = Get-Content $pj -Raw -Encoding UTF8
    $p = [regex]::Replace($p, '"version"\s*:\s*"[^"]*"', ('"version": "' + $ver + '"'), 1)
    $p = [regex]::Replace($p, 'releases/download/v[^/]+/StartWise-[^"]+\.mcpb', ('releases/download/v' + $ver + '/StartWise-' + $ver + '.mcpb'))
    [System.IO.File]::WriteAllText($pj, $p, (New-Object System.Text.UTF8Encoding($false)))
}

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$out = Join-Path $dist "StartWise-$ver.mcpb"
if (Test-Path $out) { Remove-Item $out }

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($out, 'Create')
try {
    function Add-Entry([string]$file, [string]$name) {
        # forward slashes: Windows PowerShell's Compress-Archive writes '\' which other tools read as part of the name
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file, $name.Replace('\', '/'), 'Optimal')
    }
    $e = $zip.CreateEntry('manifest.json')
    $w = New-Object System.IO.StreamWriter($e.Open(), (New-Object System.Text.UTF8Encoding($false)))
    $w.Write($manifest); $w.Close()

    Add-Entry (Join-Path $PSScriptRoot 'mcpb\icon.png') 'icon.png'
    $pdf = Join-Path $PSScriptRoot 'StartWise-Kurulum.pdf'
    if (Test-Path $pdf) { Add-Entry $pdf 'StartWise-Kurulum.pdf' }
    Add-Entry (Join-Path $PSScriptRoot 'start.ps1') 'server/start.ps1'
    Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' -File | ForEach-Object { Add-Entry $_.FullName ('server/src/' + $_.Name) }

    $k = Join-Path $root 'knowledge'
    Get-ChildItem $k -Recurse -File | Where-Object { $_.FullName -notmatch '\\generators\\' } | ForEach-Object {
        Add-Entry $_.FullName ('knowledge/' + $_.FullName.Substring($k.Length + 1))
    }
}
finally { $zip.Dispose() }

# the install guide also goes next to the package: users read it before installing
if (Test-Path (Join-Path $PSScriptRoot 'StartWise-Kurulum.pdf')) { Copy-Item (Join-Path $PSScriptRoot 'StartWise-Kurulum.pdf') $dist -Force }

$size = [math]::Round((Get-Item $out).Length / 1KB)
Write-Host "PACKED: $out ($size KB, version $ver)"

# SHA-256 checksums for the release page: users and IT can check that the files are the ones built here.
# Format is the one 'sha256sum -c' reads: "<hash>  <file name>".
$sums = Join-Path $dist "StartWise-$ver.sha256"
$lines = foreach ($f in @($out)) {
    (Get-FileHash -Algorithm SHA256 $f).Hash.ToLowerInvariant() + '  ' + (Split-Path $f -Leaf)
}
[System.IO.File]::WriteAllText($sums, (($lines -join "`n") + "`n"), (New-Object System.Text.UTF8Encoding($false)))
Write-Host ''
Write-Host "SHA-256 ($(Split-Path $sums -Leaf)):"
$lines | ForEach-Object { Write-Host "  $_" }
Write-Host 'Install: Claude Desktop > Settings > Extensions > Advanced settings > Install Extension... (see StartWise-Kurulum.pdf).'
Write-Host ('Release: create GitHub release v' + $ver + ' and attach ' + (Split-Path $out -Leaf) + ' and ' + (Split-Path $sums -Leaf) + '; paste the SHA-256 lines into the release notes.')
Write-Host 'Before testing the package on this PC, remove the hand-made "startwise-v21" entry from claude_desktop_config.json, or the tools appear twice.'
