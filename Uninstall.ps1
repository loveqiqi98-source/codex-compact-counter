$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\CodexCompactCounter'))
$targetExe = Join-Path $destination 'CodexCompactCounter.exe'
foreach ($process in @(Get-Process CodexCompactCounter -ErrorAction SilentlyContinue | Where-Object Path -EQ $targetExe)) { Stop-Process -Id $process.Id; $process.WaitForExit() }
foreach ($shortcut in @((Join-Path ([Environment]::GetFolderPath('Startup')) 'Codex Compact Counter.lnk'),(Join-Path ([Environment]::GetFolderPath('Programs')) 'Codex 压缩计数器.lnk'))) {
    if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut }
}
# Remove only files installed by this package; preserve any unrelated files.
foreach ($name in @('CodexCompactCounter.exe','CodexCompactCounter.exe.new','README.md','LICENSE','Uninstall.ps1','Uninstall.cmd')) {
    $file = Join-Path $destination $name
    if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
}
$docTarget = Join-Path $destination 'docs'
if (Test-Path -LiteralPath $docTarget) {
    foreach ($name in @('card.png','menu.png','history.png','export.png','notice.png')) { $file = Join-Path $docTarget $name; if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
    if (@(Get-ChildItem -LiteralPath $docTarget -Force).Count -eq 0) { Remove-Item -LiteralPath $docTarget }
}
Get-ChildItem -LiteralPath $destination -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^CodexCompactCounter\.previous-(?:[0-9a-f]{32}|[0-9]{14})\.exe$' } | ForEach-Object { Remove-Item -LiteralPath $_.FullName -ErrorAction SilentlyContinue }
if ((Test-Path -LiteralPath $destination) -and @(Get-ChildItem -LiteralPath $destination -Force).Count -eq 0) { Remove-Item -LiteralPath $destination }
Write-Host '已卸载并取消自动启动。计数历史保留在 LocalAppData\CodexCompactCounter。'
