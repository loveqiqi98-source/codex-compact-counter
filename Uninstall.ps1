$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\CodexCompactCounter'))
$targetExe = Join-Path $destination 'CodexCompactCounter.exe'
foreach ($process in @(Get-Process CodexCompactCounter -ErrorAction SilentlyContinue | Where-Object Path -EQ $targetExe)) { Stop-Process -Id $process.Id; $process.WaitForExit() }
foreach ($shortcut in @((Join-Path ([Environment]::GetFolderPath('Startup')) 'Codex Compact Counter.lnk'),(Join-Path ([Environment]::GetFolderPath('Programs')) 'Codex 压缩计数器.lnk'))) {
    if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut }
}
# Remove only files installed by this package; preserve any unrelated files.
foreach ($name in @('CodexCompactCounter.exe','README.md','LICENSE','Uninstall.ps1','Uninstall.cmd')) {
    $file = Join-Path $destination $name
    if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
}
if ((Test-Path -LiteralPath $destination) -and @(Get-ChildItem -LiteralPath $destination -Force).Count -eq 0) { Remove-Item -LiteralPath $destination }
Write-Host '已卸载并取消自动启动。计数历史保留在 LocalAppData\CodexCompactCounter。'
