param(
    [ValidateSet('linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64', 'all')]
    [string]$Runtime = 'all',
    [string]$Version = '1.0.2'
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must have the form 1.0.0.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$localSdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnetExe = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$pythonExe = (Get-Command python -ErrorAction Stop).Source
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$runtimes = if ($Runtime -eq 'all') { @('linux-x64', 'linux-arm64', 'osx-arm64', 'osx-x64') } else { @($Runtime) }
Push-Location $projectRoot
try {
    foreach ($rid in $runtimes) {
        $destination = Join-Path $projectRoot ".artifacts\cross-publish\$Version\$rid"
        if (Test-Path -LiteralPath $destination) { throw "Publish directory exists: $destination" }
        & $dotnetExe publish src/AiGuardLock.CrossPlatform/AiGuardLock.CrossPlatform.csproj -c Release -r $rid `
            --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:UseAppHost=true `
            -p:DebugType=None -p:DebugSymbols=false -p:Version=$Version --configfile NuGet.Config `
            --artifacts-path ".artifacts/publish-build/$Version/$rid" --packages .tools/packages -o $destination
        if ($LASTEXITCODE -ne 0) { throw "Publish failed for $rid" }
        & $pythonExe scripts/package-cross.py --runtime $rid --version $Version --publish $destination `
            --assets ".artifacts/publish-build/$Version/$rid/obj/AiGuardLock.CrossPlatform/project.assets.json"
        if ($LASTEXITCODE -ne 0) { throw "Packaging failed for $rid" }
    }
} finally { Pop-Location }
