; Installer per MonitorExtender.Server, compilato con Inno Setup 6 (ISCC.exe).
; Non si compila a mano: usa tools\build-installer.ps1, che passa MyAppVersion e SourceDir.
;
;   powershell -File tools\build-installer.ps1 -Version 1.0.0
;
; Produce dist\MonitorExtenderSetup-<versione>.exe

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist\MonitorExtender-" + MyAppVersion + "-win-x64"
#endif

[Setup]
; Fissato una volta per sempre: non generarne uno nuovo, altrimenti Windows non riconosce
; piu' gli aggiornamenti come lo stesso programma.
AppId={{42F6590E-F2EA-42AD-B434-B2E3CC06E09A}
AppName=MonitorExtender
AppVersion={#MyAppVersion}
AppPublisher=Giuseppe Mantello
AppPublisherURL=https://github.com/Gioixxx/MonitorExtender
DefaultDirName={localappdata}\MonitorExtender
DefaultGroupName=MonitorExtender
DisableProgramGroupPage=yes
; Nessun privilegio di amministratore per l'installer stesso: autostart.ps1 gira gia' senza
; admin per scelta (i servizi Windows non possono catturare lo schermo), e setup-network.ps1
; si autoeleva da solo con un proprio prompt UAC quando serve. Chiedere admin qui in piu'
; darebbe due UAC scollegati per la stessa installazione.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=MonitorExtenderSetup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
LicenseFile=..\LICENSE
SetupIconFile=..\src\MonitorExtender.Server\icon.ico
; Gestisce da solo l'eseguibile bloccato da un'istanza gia' in esecuzione, sia
; nell'installazione/aggiornamento sia nella disinstallazione.
CloseApplications=yes
CloseApplicationsFilter=MonitorExtender.Server.exe
RestartApplications=no
UninstallDisplayIcon={app}\MonitorExtender.Server.exe

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "network"; Description: "Configura la rete per il Wi-Fi (apre una finestra separata da autorizzare)"
Name: "network\trust"; Description: "Considera questa rete come attendibile, anche se Windows la segna come pubblica"; Flags: unchecked
Name: "autostart"; Description: "Avvia MonitorExtender automaticamente all'accesso"
Name: "desktopicon"; Description: "Crea un'icona sul desktop"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\MonitorExtender"; Filename: "{app}\MonitorExtender.Server.exe"
Name: "{autodesktop}\MonitorExtender"; Filename: "{app}\MonitorExtender.Server.exe"; Tasks: desktopicon

[Run]
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -NoProfile -File ""{app}\tools\setup-network.ps1"" -Port 8080 -DiscoveryPort 8079{code:TrustNetworkArg}"; Tasks: network; StatusMsg: "Configurazione della rete..."; Flags: waituntilterminated runhidden
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -NoProfile -File ""{app}\tools\autostart.ps1"" -Exe ""{app}\MonitorExtender.Server.exe"""; Tasks: autostart; StatusMsg: "Registrazione avvio automatico..."; Flags: waituntilterminated runhidden
Filename: "{app}\MonitorExtender.Server.exe"; Description: "Avvia MonitorExtender adesso"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Idempotenti entrambi: si possono richiamare sempre, a prescindere da quali caselle
; erano state scelte in installazione.
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -NoProfile -File ""{app}\tools\autostart.ps1"" -Remove"; RunOnceId: "RemoveAutostart"; Flags: waituntilterminated runhidden
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -NoProfile -File ""{app}\tools\setup-network.ps1"" -Remove"; RunOnceId: "RemoveNetwork"; Flags: waituntilterminated runhidden

[Code]
function TrustNetworkArg(Param: String): String;
begin
  if WizardIsTaskSelected('network\trust') then
    Result := ' -TrustNetwork'
  else
    Result := '';
end;
