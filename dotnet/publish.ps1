$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

Write-Host '==> Building the Vue SPA into ProphetOps.Api/wwwroot'
Push-Location (Join-Path $root 'client')
try {
    npm ci
    npm run build
} finally {
    Pop-Location
}

Write-Host '==> Publishing the API (self-contained win-x64) into publish/'
$out = Join-Path $root 'publish'
& $dotnet publish (Join-Path $root 'ProphetOps.Api\ProphetOps.Api.csproj') `
    -c Release -r win-x64 --self-contained true -o $out

Write-Host '==> Building the installer'
$setup = Join-Path $root 'ProphetOps.Setup\bin\handoff-setup'
& $dotnet publish (Join-Path $root 'ProphetOps.Setup\ProphetOps.Setup.csproj') `
    -c Release -r win-x64 --self-contained true -o $setup

Write-Host '==> Assembling handoff/'
# What goes on the USB drive the agency keeps. The installer sits beside an app/ folder holding
# the published application, so one double-click sets the whole PC up.
$handoff = Join-Path $root 'handoff'
if (Test-Path $handoff) { Remove-Item $handoff -Recurse -Force }
New-Item -ItemType Directory -Path $handoff | Out-Null

Copy-Item (Join-Path $setup 'ProphetOps-Setup.exe') $handoff
Copy-Item $out (Join-Path $handoff 'app') -Recurse

# A database left over from local testing must never reach the agency: it would arrive holding
# sample bookings and look like real records.
Get-ChildItem (Join-Path $handoff 'app') -Filter 'prophetops.db*' | Remove-Item -Force
$stale = Join-Path $handoff 'app\backups'
if (Test-Path $stale) { Remove-Item $stale -Recurse -Force }

Set-Content -Path (Join-Path $handoff 'READ ME FIRST.txt') -Encoding utf8 -Value @'
ProphetOps
Decision support for Renan-Tina Travel and Tours

TO INSTALL
    Double-click  ProphetOps-Setup.exe

    Windows will ask for permission to make changes. Choose Yes. Installation
    takes about a minute, and the system opens in your browser when it is done.

AFTER INSTALLING
    ProphetOps starts on its own every time the PC is switched on. You do not
    need to open anything. It is running even before anyone signs in to Windows.

    On this PC          http://localhost:5099
    On other devices    the installer prints the address to use

IF YOU SEE "Windows protected your PC"
    Click "More info", then "Run anyway". This appears because the installer
    is not signed with a paid certificate. Nothing is wrong.

TO REMOVE IT
    Run the installer again from a Command Prompt with --uninstall

BACKUPS
    A copy of the database is saved every day to the backups folder inside
    C:\ProphetOps. Copy that folder to a USB drive or cloud folder regularly:
    a backup on the same PC will not survive that PC being lost or stolen.
'@

Write-Host ''
Write-Host "Done."
Write-Host "  Standalone app   $out"
Write-Host "  Handoff kit      $handoff"
Write-Host ''
Write-Host 'Copy the handoff folder to a USB drive and run ProphetOps-Setup.exe on the agency PC.'
