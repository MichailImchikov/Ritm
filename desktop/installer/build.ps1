$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$out = Join-Path $root 'outputs/installer'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$framework = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
$refs = @('System.dll','System.Core.dll','System.Net.Http.dll','System.Security.dll','System.Xaml.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','Microsoft.CSharp.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$exe = Join-Path $out 'Ritm.exe'
$args = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output','/codepage:65001',('/win32manifest:'+(Join-Path $root 'desktop/app.manifest')),('/win32icon:'+(Join-Path $root 'desktop/ritm.ico')),('/out:'+$exe)) + $refs
$args += @(Get-ChildItem (Join-Path $root 'desktop') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $csc $args
if ($LASTEXITCODE -ne 0) { throw 'App compilation failed' }
$uninstall = Join-Path $root 'Удалить Ритм.exe'
& $csc /nologo /target:winexe /codepage:65001 /utf8output ("/win32icon:"+(Join-Path $root 'desktop/ritm.ico')) ("/out:"+$uninstall) $refs (Join-Path $PSScriptRoot 'Uninstall.cs')
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller compilation failed' }
$setup = Join-Path $root 'Установить Ритм.exe'
& $csc /nologo /target:winexe /codepage:65001 /utf8output ("/win32icon:"+(Join-Path $root 'desktop/ritm.ico')) ("/out:"+$setup) ("/resource:$exe,Ritm.exe") ("/resource:$(Join-Path $root 'desktop/Ritm.exe.config'),Ritm.exe.config") $refs (Join-Path $PSScriptRoot 'Setup.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
Write-Output "Созданы: $setup и $uninstall"
