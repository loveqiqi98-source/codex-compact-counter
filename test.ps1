param([string]$TestDirectory = (Join-Path $env:TEMP ('CodexCompactCounter-test-' + [Guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $TestDirectory -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $TestDirectory 'Tests.exe'
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName
& $compiler /nologo /optimize+ /codepage:65001 /main:Tests /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ('/out:' + $exe) $sourceFiles (Join-Path $PSScriptRoot 'tests\Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '测试编译失败' }
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = $exe
$start.Arguments = '"' + (Join-Path $TestDirectory 'fixtures') + '"'
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$testProcess = [Diagnostics.Process]::Start($start)
$output = $testProcess.StandardOutput.ReadToEnd()
$errors = $testProcess.StandardError.ReadToEnd()
$testProcess.WaitForExit()
Write-Host $output
if ($errors) { Write-Host $errors }
if ($testProcess.ExitCode -ne 0) { throw '测试失败' }
Write-Host ('模拟文件和结果保留在：' + $TestDirectory)
