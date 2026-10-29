# Включает (или с -Uninstall выключает) автосборку Ритма после pull/merge/checkout/rebase.
param([switch]$Uninstall)
$ErrorActionPreference='Stop'
$root=git -C $PSScriptRoot rev-parse --show-toplevel
if($LASTEXITCODE -ne 0){throw 'Не найден git-репозиторий'}
if($Uninstall){
 git -C $root config --unset core.hooksPath
 Write-Output 'Автосборка выключена (core.hooksPath сброшен).'
 return
}
git -C $root config core.hooksPath .githooks
if($LASTEXITCODE -ne 0){throw 'Не удалось задать core.hooksPath'}
Write-Output 'Автосборка включена: после git pull/merge/checkout/rebase изменённые исходники desktop пересобираются через desktop/build.ps1.'
Write-Output 'Пропустить один раз: $env:RITM_SKIP_AUTOBUILD=1 (или RITM_SKIP_AUTOBUILD=1 git pull в Git Bash).'
