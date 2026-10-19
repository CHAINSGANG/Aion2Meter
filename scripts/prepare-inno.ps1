param(
    [string]$Version = '7.1.0'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$toolsRoot = Join-Path $repositoryRoot 'artifacts\tools'
$innoRoot = Join-Path $toolsRoot "inno-$Version"
$compilerPath = Join-Path $innoRoot 'ISCC.exe'
$installerPath = Join-Path $toolsRoot "innosetup-$Version-x64.exe"
$releaseTag = 'is-' + $Version.Replace('.', '_')
$downloadUrl = "https://github.com/jrsoftware/issrc/releases/download/$releaseTag/innosetup-$Version-x64.exe"

if (Test-Path -LiteralPath $compilerPath) {
    Write-Output $compilerPath
    exit 0
}

New-Item -ItemType Directory -Path $toolsRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath $installerPath)) {
    Invoke-WebRequest -Uri $downloadUrl -OutFile $installerPath
}

$signature = Get-AuthenticodeSignature -LiteralPath $installerPath
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Pyrsys B\.V\.') {
    throw "Inno Setup signature validation failed: $($signature.Status), $($signature.SignerCertificate.Subject)"
}

$arguments = @(
    '/PORTABLE=1',
    '/VERYSILENT',
    '/CURRENTUSER',
    '/NORESTART',
    '/SUPPRESSMSGBOXES',
    "/DIR=`"$innoRoot`""
)
$process = Start-Process -FilePath $installerPath -ArgumentList $arguments -Wait -PassThru
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $compilerPath)) {
    throw "Inno Setup portable installation failed with exit code $($process.ExitCode)"
}

Write-Output $compilerPath
