param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',
    [string]$Version = '1.0.0'
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must have the form 1.0.0.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$localSdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnetExe = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_ROOT = Split-Path -Parent $dotnetExe
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$packageRoot = Join-Path $projectRoot '.tools\packages'
$distRoot = Join-Path $projectRoot 'dist'
$name = "AI-Guard-Lock-$Version-$Runtime"
$destination = Join-Path $distRoot $name
$archive = Join-Path $distRoot "$name.zip"
if ((Test-Path -LiteralPath $destination) -or (Test-Path -LiteralPath $archive)) {
    throw 'This release already exists. Use a new version or explicitly remove the old release before rebuilding.'
}
[void][IO.Directory]::CreateDirectory($destination)
Push-Location $projectRoot
try {
    & $dotnetExe publish 'src/AiGuardLock/AiGuardLock.csproj' -c Release -r $Runtime --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false `
        -p:DebugType=None -p:DebugSymbols=false -p:Version=$Version --configfile NuGet.Config `
        --packages $packageRoot -o $destination
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath 'docs/PORTABLE-JA.txt' -Destination (Join-Path $destination 'README-JA.txt')
    Copy-Item -LiteralPath 'scripts/portable-shortcut.ps1' -Destination (Join-Path $destination 'Create-Desktop-Shortcut.ps1')
    Copy-Item -LiteralPath 'scripts/portable-shortcut.cmd' -Destination (Join-Path $destination 'Create-Desktop-Shortcut.cmd')
    $licenseRoot = Join-Path $destination 'LICENSES'
    [void][IO.Directory]::CreateDirectory($licenseRoot)
    foreach ($package in @("microsoft.netcore.app.runtime.$Runtime", "microsoft.windowsdesktop.app.runtime.$Runtime")) {
        $versionsRoot = Join-Path $packageRoot $package
        $latest = Get-ChildItem -LiteralPath $versionsRoot -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
        if (-not $latest) { throw "Runtime package not found: $package" }
        foreach ($notice in (Get-ChildItem -LiteralPath $latest.FullName -File | Where-Object { $_.Name -match 'LICENSE|THIRD.PARTY' })) {
            Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $licenseRoot "$package-$($latest.Name)-$($notice.Name)")
        }
    }
    $exe = Join-Path $destination 'AiGuardLock.exe'
    $hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    "$hash  AiGuardLock.exe" | Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.txt') -Encoding ascii
    Compress-Archive -LiteralPath $destination -DestinationPath $archive -CompressionLevel Optimal
    $zipHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    "$zipHash  $name.zip" | Set-Content -LiteralPath "$archive.sha256" -Encoding ascii
    Get-Item -LiteralPath $archive | Select-Object FullName, Length
} finally { Pop-Location }
