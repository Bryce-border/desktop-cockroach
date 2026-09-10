param([string]$OutputDirectory = 'bin')
$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkDir 'csc.exe'
$outputDir = Join-Path $projectDir $OutputDirectory
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$references = @('System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Xaml.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') | ForEach-Object { '/reference:' + (Join-Path $frameworkDir $_) }
$references += @('WindowsBase.dll', 'PresentationCore.dll', 'PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path (Join-Path $frameworkDir 'WPF') $_) }
$sources = Get-ChildItem (Join-Path $projectDir 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
$assets = Get-ChildItem (Join-Path $projectDir 'assets\pets') -Filter 'pet-?.png' | ForEach-Object { '/resource:' + $_.FullName + ',' + $_.Name }
& $compilerPath /nologo /target:winexe /platform:anycpu /optimize+ /main:DesktopRoach.Program ('/out:' + (Join-Path $outputDir 'DesktopRoach.exe')) ('/resource:' + (Join-Path $projectDir 'src\ControlWindow.xaml') + ',ControlWindow.xaml') @assets @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built: $outputDir\DesktopRoach.exe"
