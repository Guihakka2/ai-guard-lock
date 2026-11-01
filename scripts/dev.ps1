param(
    [ValidateSet('Build', 'Run', 'Test')]
    [string]$Action = 'Build'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$localSdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (Test-Path -LiteralPath $localSdk) {
    $dotnetExe = $localSdk
    $env:DOTNET_ROOT = Split-Path -Parent $localSdk
} else {
    $dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source
}
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
Push-Location $projectRoot
try {
    & $dotnetExe restore AiGuardLock.slnx --configfile NuGet.Config --packages .tools/packages
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & $dotnetExe build AiGuardLock.slnx --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if ($Action -eq 'Run') {
        & $dotnetExe 'src/AiGuardLock/bin/Release/net10.0-windows10.0.22000.0/AiGuardLock.dll'
        if ($LASTEXITCODE -ne 0) { throw 'Application exited with an error.' }
    } elseif ($Action -eq 'Test') {
        & $dotnetExe 'tests/AiGuardLock.Tests/bin/Release/net10.0-windows10.0.22000.0/AiGuardLock.Tests.dll'
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    }
} finally {
    Pop-Location
}
