param(
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$sourcePath = Join-Path $PSScriptRoot 'app'
$installPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Aion2Meter'
$executablePath = Join-Path $installPath 'Aion2Meter.exe'
$npcapVersion = '1.89'
$npcapUrl = "https://npcap.com/dist/npcap-$npcapVersion.exe"
$npcapSha256 = '8AED85E900D783D1308506E919587D3E540451947AF8A82F2D04F819E44305CC'
$npcapPublishers = @('Nmap Software LLC', 'Insecure.Com LLC')

function Test-NpcapInstalled {
    return (Test-Path -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Services\npcap') -or
        [bool](Get-Service -Name 'npcap' -ErrorAction SilentlyContinue)
}

if (-not (Test-Path -LiteralPath (Join-Path $sourcePath 'Aion2Meter.exe'))) {
    throw 'Папка app повреждена или архив не был полностью распакован.'
}

if (Get-Process -Name 'Aion2Meter' -ErrorAction SilentlyContinue) {
    throw 'Сначала закройте Aion II Meter, затем повторите установку.'
}

New-Item -ItemType Directory -Path $installPath -Force | Out-Null
Copy-Item -Path (Join-Path $sourcePath '*') -Destination $installPath -Recurse -Force

$shell = New-Object -ComObject WScript.Shell
$desktopShortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Aion II Meter.lnk'))
$desktopShortcut.TargetPath = $executablePath
$desktopShortcut.WorkingDirectory = $installPath
$desktopShortcut.IconLocation = "$executablePath,0"
$desktopShortcut.Save()

$startMenuDirectory = Join-Path ([Environment]::GetFolderPath('Programs')) 'Aion II Meter'
New-Item -ItemType Directory -Path $startMenuDirectory -Force | Out-Null
$startMenuShortcut = $shell.CreateShortcut((Join-Path $startMenuDirectory 'Aion II Meter.lnk'))
$startMenuShortcut.TargetPath = $executablePath
$startMenuShortcut.WorkingDirectory = $installPath
$startMenuShortcut.IconLocation = "$executablePath,0"
$startMenuShortcut.Save()

Write-Host "Aion II Meter установлен: $installPath" -ForegroundColor Green
if (-not (Test-NpcapInstalled)) {
    $npcapTempDirectory = Join-Path ([IO.Path]::GetTempPath()) 'Aion2Meter'
    $npcapInstaller = Join-Path $npcapTempDirectory "npcap-$npcapVersion.exe"

    New-Item -ItemType Directory -Path $npcapTempDirectory -Force | Out-Null
    try {
        Write-Host "Скачивание официального Npcap $npcapVersion..." -ForegroundColor Cyan
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $npcapUrl -OutFile $npcapInstaller -UseBasicParsing

        $actualHash = (Get-FileHash -LiteralPath $npcapInstaller -Algorithm SHA256).Hash
        if ($actualHash -ne $npcapSha256) {
            throw "Контрольная сумма Npcap не совпала. Файл не будет запущен."
        }

        $signature = Get-AuthenticodeSignature -LiteralPath $npcapInstaller
        $trustedPublisher = $false
        if ($null -ne $signature.SignerCertificate) {
            foreach ($publisher in $npcapPublishers) {
                if ($signature.SignerCertificate.Subject -like "*$publisher*") {
                    $trustedPublisher = $true
                    break
                }
            }
        }
        if ($signature.Status -ne 'Valid' -or -not $trustedPublisher) {
            throw 'Цифровая подпись установщика Npcap не прошла проверку.'
        }

        Write-Host 'Запуск официального мастера Npcap...' -ForegroundColor Cyan
        $npcapProcess = Start-Process -FilePath $npcapInstaller -Verb RunAs -Wait -PassThru
        if ($npcapProcess.ExitCode -notin @(0, 3010)) {
            throw "Установка Npcap завершилась с кодом $($npcapProcess.ExitCode)."
        }
        if (-not (Test-NpcapInstalled)) {
            throw 'Npcap завершил установку, но драйвер не обнаружен.'
        }
    }
    finally {
        Remove-Item -LiteralPath $npcapInstaller -Force -ErrorAction SilentlyContinue
    }
}

if (-not $NoLaunch) {
    Start-Process -FilePath $executablePath -WorkingDirectory $installPath
}
