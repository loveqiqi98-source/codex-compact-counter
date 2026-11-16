$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '需要 Windows .NET Framework 4.8（Windows 10/11 通常已自带）。' }
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ('/out:' + (Join-Path $PSScriptRoot 'CodexCompactCounter.exe')) $sourceFiles
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
Write-Host '已生成 CodexCompactCounter.exe'
