$ErrorActionPreference = 'Stop'
$destination = Join-Path $env:LOCALAPPDATA 'Programs\CodexCompactCounter'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$targetExe = Join-Path $destination 'CodexCompactCounter.exe'
$sourceExe = Join-Path $PSScriptRoot 'CodexCompactCounter.exe'
foreach ($process in @(Get-Process CodexCompactCounter -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $targetExe -or $_.Path -eq $sourceExe })) { Stop-Process -Id $process.Id; $process.WaitForExit() }
# Replace by rename so an Explorer/icon handle cannot lock an in-place update.
if ([IO.Path]::GetFullPath($sourceExe) -ne [IO.Path]::GetFullPath($targetExe)) {
    $stagedExe = $targetExe + '.new'
    Copy-Item -LiteralPath $sourceExe -Destination $stagedExe -Force
    $previousExe = $null
    if (Test-Path -LiteralPath $targetExe) {
        $previousExe = Join-Path $destination ('CodexCompactCounter.previous-' + [Guid]::NewGuid().ToString('N') + '.exe')
        Move-Item -LiteralPath $targetExe -Destination $previousExe
    }
    try { Move-Item -LiteralPath $stagedExe -Destination $targetExe }
    catch { if ($previousExe) { Move-Item -LiteralPath $previousExe -Destination $targetExe }; throw }
    if ($previousExe) { Remove-Item -LiteralPath $previousExe -ErrorAction SilentlyContinue }
}
foreach ($file in @('README.md','LICENSE','Uninstall.ps1','Uninstall.cmd')) {
    $sourceFile = Join-Path $PSScriptRoot $file
    if ((Test-Path -LiteralPath $sourceFile) -and [IO.Path]::GetFullPath($sourceFile) -ne [IO.Path]::GetFullPath((Join-Path $destination $file))) { Copy-Item -LiteralPath $sourceFile -Destination $destination -Force }
}
$docSource = Join-Path $PSScriptRoot 'docs'
if (Test-Path -LiteralPath $docSource) {
    $docTarget = Join-Path $destination 'docs'
    New-Item -ItemType Directory -Path $docTarget -Force | Out-Null
    foreach ($name in @('card.png','menu.png','history.png','export.png','notice.png')) {
        $from = Join-Path $docSource $name
        if ((Test-Path -LiteralPath $from) -and [IO.Path]::GetFullPath($from) -ne [IO.Path]::GetFullPath((Join-Path $docTarget $name))) { Copy-Item -LiteralPath $from -Destination $docTarget -Force }
    }
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
$startupShortcut.WindowStyle = 1
$startupShortcut.Description = 'Show the compaction counter while Codex is open.'
$startupShortcut.Save()
# ShellExecute starts the installed user application; no terminal stays open.
Start-Process -FilePath $targetExe -ArgumentList '--watch' -WindowStyle Normal
Write-Host ('已安装到：' + $destination)
Write-Host '已启用随登录启动。打开 Codex 时显示小窗，关闭 Codex 时小窗自动关闭。'
