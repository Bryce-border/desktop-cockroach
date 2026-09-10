param([string]$OutputDirectory = 'bin')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $OutputDirectory
$packageDir = Join-Path $PSScriptRoot 'artifacts\DesktopRoach-0.4.1-win'
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path (Join-Path $PSScriptRoot $OutputDirectory) 'DesktopRoach.exe') -Destination $packageDir
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $packageDir
Compress-Archive -Path (Join-Path $packageDir '*') -DestinationPath (Join-Path $PSScriptRoot 'artifacts\DesktopRoach-0.4.1-win.zip') -Force
Write-Output (Join-Path $PSScriptRoot 'artifacts\DesktopRoach-0.4.1-win.zip')
