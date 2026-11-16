$ErrorActionPreference = 'Stop'
$destination = Join-Path $env:LOCALAPPDATA 'Programs\CodexCompactCounter'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$targetExe = Join-Path $destination 'CodexCompactCounter.exe'
$sourceExe = Join-Path $PSScriptRoot 'CodexCompactCounter.exe'
foreach ($process in @(Get-Process CodexCompactCounter -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $targetExe -or $_.Path -eq $sourceExe })) { Stop-Process -Id $process.Id; $process.WaitForExit() }
foreach ($file in @('CodexCompactCounter.exe','README.md','LICENSE','Uninstall.ps1','Uninstall.cmd')) {
    $sourceFile = Join-Path $PSScriptRoot $file
    if ((Test-Path -LiteralPath $sourceFile) -and [IO.Path]::GetFullPath($sourceFile) -ne [IO.Path]::GetFullPath((Join-Path $destination $file))) { Copy-Item -LiteralPath $sourceFile -Destination $destination -Force }
}
$shell = New-Object -ComObject WScript.Shell
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Codex 压缩计数器.lnk'
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $destination 'CodexCompactCounter.exe'
$shortcut.WorkingDirectory = $destination
$shortcut.Save()
$startupPath = Join-Path ([Environment]::GetFolderPath('Startup')) 'Codex Compact Counter.lnk'
$startupShortcut = $shell.CreateShortcut($startupPath)
$startupShortcut.TargetPath = $targetExe
$startupShortcut.Arguments = '--watch'
$startupShortcut.WorkingDirectory = $destination
$startupShortcut.WindowStyle = 7
$startupShortcut.Description = 'Show the compaction counter while Codex is open.'
$startupShortcut.Save()
# ShellExecute starts the installed user application; no terminal stays open.
Start-Process -FilePath $targetExe -ArgumentList '--watch' -WindowStyle Hidden
Write-Host ('已安装到：' + $destination)
Write-Host '已启用随登录启动。打开 Codex 时显示小窗，关闭 Codex 时小窗自动关闭。'
