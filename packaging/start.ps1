# StartWise launcher (prototype): compiles src\*.cs in memory with the C# compiler of .NET Framework
# and runs the MCP server inside powershell.exe. No StartWise exe is written to disk.
# Test:  powershell -NoProfile -ExecutionPolicy Bypass -File packaging\start.ps1 --selftest
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest)
$ErrorActionPreference = 'Stop'

# Layout: package = server\start.ps1 + server\src\ ; repo = packaging\start.ps1 + src\
$here = $PSScriptRoot
$src = Join-Path $here 'src'
if (-not (Test-Path $src)) { $src = Join-Path (Split-Path -Parent $here) 'src' }
$env:STARTWISE_HOME = $here

$api = $env:TIA_API_DIR
if (-not $api) { $api = Join-Path $env:ProgramFiles 'Siemens\Automation\Portal V21\PublicAPI\V21\net48' }

$cp = New-Object System.CodeDom.Compiler.CompilerParameters
$cp.GenerateExecutable = $false
$cp.GenerateInMemory = $true
$cp.IncludeDebugInformation = $false
$defs = ''
if (-not (Test-Path (Join-Path $api 'Siemens.Engineering.WinCCUnified.dll'))) { $defs = ' /define:NO_UNIFIED' }
$cp.CompilerOptions = '/optimize+ /platform:x64' + $defs
foreach ($r in 'System.dll','System.Core.dll','System.Xml.dll','System.Data.dll','System.Web.Extensions.dll','System.Drawing.dll',
                'System.Security.dll','System.Xml.Linq.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll') {
    [void]$cp.ReferencedAssemblies.Add($r)
}
foreach ($n in 'Base','Step7','WinCC','WinCC.Extension','WinCCUnified','Safety','Startdrive') {
    $f = Join-Path $api "Siemens.Engineering.$n.dll"
    if (Test-Path $f) { [void]$cp.ReferencedAssemblies.Add($f) }
}

$files = @(Get-ChildItem (Join-Path $src '*.cs') | ForEach-Object { $_.FullName })
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
$res = $provider.CompileAssemblyFromFile($cp, $files)
$errs = @($res.Errors | Where-Object { -not $_.IsWarning })
if ($errs.Count -gt 0) {
    foreach ($e in $errs) { [Console]::Error.WriteLine("$($e.FileName)($($e.Line)): $($e.ErrorText)") }
    exit 1
}

$main = $res.CompiledAssembly.GetType('TiaAssistant.Program').GetMethod('Main')
$argv = [string[]]@($Rest | Where-Object { $_ })
exit $main.Invoke($null, (, $argv))
