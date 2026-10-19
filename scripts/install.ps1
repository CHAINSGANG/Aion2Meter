param(
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$sourcePath = Join-Path $PSScriptRoot 'app'
$installPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Aion2Meter'
$executablePath = Join-Path $installPath 'Aion2Meter.exe'

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

$npcapInstalled = (Test-Path -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Services\npcap') -or
    [bool](Get-Service -Name 'npcap' -ErrorAction SilentlyContinue)

Write-Host "Aion II Meter установлен: $installPath" -ForegroundColor Green
if (-not $npcapInstalled) {
    Write-Warning 'Npcap не найден. Он устанавливается отдельно из-за условий лицензии.'
    Start-Process 'https://npcap.com/#download'
    Write-Host 'Установите Npcap, затем запустите Aion II Meter с ярлыка.'
    exit 0
}

if (-not $NoLaunch) {
    Start-Process -FilePath $executablePath -WorkingDirectory $installPath
}
