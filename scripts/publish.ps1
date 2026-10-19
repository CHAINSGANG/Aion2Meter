param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\Aion2Meter\Aion2Meter.csproj'
$releaseRoot = Join-Path $repositoryRoot 'artifacts\release'
$packageName = "Aion2Meter-$Runtime"
$stagePath = Join-Path $releaseRoot $packageName
$appPath = Join-Path $stagePath 'app'
$archivePath = Join-Path $releaseRoot "$packageName.zip"
$checksumPath = "$archivePath.sha256"

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
if (Test-Path -LiteralPath $stagePath) {
    Remove-Item -LiteralPath $stagePath -Recurse -Force
}
New-Item -ItemType Directory -Path $appPath -Force | Out-Null

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $appPath `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'Install.cmd') -Destination $stagePath
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\install.ps1') -Destination $stagePath
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'packaging\README.txt') -Destination $stagePath

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
Compress-Archive -Path (Join-Path $stagePath '*') -DestinationPath $archivePath -CompressionLevel Optimal

$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumPath -Value "$hash  $packageName.zip" -Encoding ascii

Write-Host "Release package: $archivePath"
Write-Host "SHA-256: $hash"
