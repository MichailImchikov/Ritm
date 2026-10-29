$ErrorActionPreference='Stop'
$framework='C:/Windows/Microsoft.NET/Framework64/v4.0.30319'
$outputPath=Join-Path $PSScriptRoot '../../outputs/Ritm'
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$references=@('System.dll','System.Core.dll','System.Net.Http.dll','System.Security.dll','System.Xaml.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll')
$compileArgs=@('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output','/codepage:65001',('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')),('/out:'+(Join-Path $outputPath 'Ritm.exe')))
if(Test-Path (Join-Path $PSScriptRoot 'ritm.ico')){$compileArgs+=('/win32icon:'+(Join-Path $PSScriptRoot 'ritm.ico'))}
foreach($reference in $references){$compileArgs+=('/reference:'+(Join-Path $framework $reference))}
Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object {$compileArgs+=$_.FullName}
& (Join-Path $framework 'csc.exe') @compileArgs
if($LASTEXITCODE -ne 0){throw 'Compilation failed'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Ritm.exe.config') -Destination $outputPath -Force
Write-Output (Join-Path $outputPath 'Ritm.exe')


