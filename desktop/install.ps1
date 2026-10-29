$ErrorActionPreference='Stop'
$sourceDirectory=Join-Path $PSScriptRoot '../../outputs/Ritm'
$releaseVersion=(Get-Item -LiteralPath (Join-Path $sourceDirectory 'Ritm.exe')).VersionInfo.FileVersion
$applicationDirectory=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('RitmPlanner/App/'+$releaseVersion)
New-Item -ItemType Directory -Path $applicationDirectory -Force | Out-Null
foreach($fileName in @('Ritm.exe','Ritm.exe.config')){Copy-Item -LiteralPath (Join-Path $sourceDirectory $fileName) -Destination (Join-Path $applicationDirectory $fileName) -Force}
$executable=Join-Path $applicationDirectory 'Ritm.exe'
$shortcutShell=New-Object -ComObject WScript.Shell
foreach($folder in @([Environment]::GetFolderPath('Desktop'),[Environment]::GetFolderPath('Programs'))){
 $shortcut=$shortcutShell.CreateShortcut((Join-Path $folder 'Ритм.lnk'))
 $shortcut.TargetPath=$executable
 $shortcut.WorkingDirectory=$applicationDirectory
 $shortcut.IconLocation=$executable+',0'
 $shortcut.Description='Ритм — задачи и планы для Windows'
 $shortcut.Save()
}
Write-Output $executable
