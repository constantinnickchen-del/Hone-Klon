; ConstiBoost PRO installer (Inno Setup 6)
; Build: iscc /DMyAppVersion=2.2.0 /DSourceDir=..\publish\win-x64 installer\ConstiBoost.iss
; Per-user install – no administrator rights needed (matches ConstiBoost's "admin only on demand" design).

#ifndef MyAppVersion
  #define MyAppVersion "2.2.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\win-x64"
#endif

#define MyAppName "ConstiBoost PRO"
#define MyAppExe "ConstiBoost.exe"

[Setup]
AppId={{6B1E4C1A-3B5E-4C8E-9F3A-2D7C1B8E5A10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=ConstiBoost
DefaultDirName={localappdata}\Programs\ConstiBoost
DefaultGroupName=ConstiBoost
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=ConstiBoost-Setup-{#MyAppVersion}
SetupIconFile=..\ConstiBoost\Resources\ConstiBoost.ico
UninstallDisplayIcon={app}\{#MyAppExe}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=yes
MinVersion=10.0.17763

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "Start ConstiBoost with Windows (in the tray)"; GroupDescription: "Options:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\ConstiBoost PRO"; Filename: "{app}\{#MyAppExe}"
Name: "{userdesktop}\ConstiBoost PRO"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ConstiBoost"; ValueData: """{app}\{#MyAppExe}"" --minimized"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,ConstiBoost PRO}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Make sure no ConstiBoost instance keeps files locked.
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExe} /F"; Flags: runhidden; RunOnceId: "KillConstiBoost"

; Settings, backups and logs in %LOCALAPPDATA%\ConstiBoost are intentionally kept on uninstall
; so that restoring backups stays possible after a reinstall.
