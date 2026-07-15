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

Write-Host ''
Write-Host "Done. Standalone app published to: $out"
Write-Host 'Run it with:   .\publish\ProphetOps.Api.exe'
