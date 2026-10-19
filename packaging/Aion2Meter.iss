#ifndef RepoRoot
  #define RepoRoot ".."
#endif
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "Aion II Meter"
#define AppExeName "Aion2Meter.exe"
#define NpcapVersion "1.89"
#define NpcapInstallerName "npcap-1.89.exe"
#define NpcapDownloadUrl "https://npcap.com/dist/npcap-1.89.exe"
#define NpcapSha256 "8aed85e900d783d1308506e919587d3e540451947af8a82f2d04f819e44305cc"
#define NpcapInstallerSize 1325224

[Setup]
AppId={{9F74F798-26AA-46CF-825E-22E9F2BD9E4D}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=CHAINSGANG
AppPublisherURL=https://github.com/CHAINSGANG/Aion2Meter
AppSupportURL=https://github.com/CHAINSGANG/Aion2Meter/issues
AppUpdatesURL=https://github.com/CHAINSGANG/Aion2Meter/releases
DefaultDirName={autopf}\Aion2Meter
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#RepoRoot}\artifacts\release
OutputBaseFilename=Aion2Meter-Setup-v{#AppVersion}
SetupIconFile={#RepoRoot}\src\Aion2Meter\Assets\Brand\Aion2Meter.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=105
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany=CHAINSGANG
VersionInfoDescription=Aion II Meter Installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Ярлыки:"; Flags: checkedonce

[Files]
Source: "{#RepoRoot}\artifacts\release\Aion2Meter-win-x64\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#NpcapDownloadUrl}"; DestDir: "{tmp}\Aion2Meter"; DestName: "{#NpcapInstallerName}"; \
  Hash: "{#NpcapSha256}"; ExternalSize: {#NpcapInstallerSize}; \
  Flags: external download deleteafterinstall ignoreversion; Check: not IsNpcapInstalled

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{group}\Удалить {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\Aion2Meter\{#NpcapInstallerName}"; StatusMsg: "Установка официального драйвера Npcap..."; Flags: waituntilterminated; Check: not IsNpcapInstalled
Filename: "{app}\{#AppExeName}"; Description: "Запустить {#AppName}"; WorkingDir: "{app}"; Flags: postinstall nowait skipifsilent runascurrentuser; Check: IsNpcapInstalled

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"

[Code]
function IsNpcapInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM64, 'SYSTEM\CurrentControlSet\Services\npcap') or
            RegKeyExists(HKLM32, 'SYSTEM\CurrentControlSet\Services\npcap');
end;

procedure InitializeWizard;
begin
  if not IsNpcapInstalled then
    WizardForm.WelcomeLabel2.Caption := WizardForm.WelcomeLabel2.Caption + #13#10#13#10 +
      'Npcap пока не установлен. Мастер сам скачает Npcap {#NpcapVersion} с официального сервера, проверит SHA-256 и откроет его установку. Браузер не потребуется.';
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpFinished) and (not IsNpcapInstalled) then
    WizardForm.FinishedLabel.Caption :=
      'Aion II Meter установлен, но установка Npcap не была завершена. ' +
      'Запустите этот установщик ещё раз и завершите мастер Npcap — без драйвера захват трафика не работает.';
end;
