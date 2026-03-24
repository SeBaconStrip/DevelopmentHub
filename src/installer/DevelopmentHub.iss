#define MyAppName "DevelopmentHub"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "DevelopmentHub"
#define MyAppExeName "DevelopmentHub.exe"
#define MyAppIconFile "DeveloperHubIcon.ico"
#define PublishDir "..\..\publish"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=output
OutputBaseFilename=DevelopmentHub-Setup-{#MyAppVersion}
SetupIconFile={#PublishDir}\{#MyAppIconFile}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "startmenu"; Description: "Startmenü-Eintrag erstellen"; GroupDescription: "Verknüpfungen:"
Name: "desktopicon"; Description: "Desktop-Verknüpfung erstellen"; GroupDescription: "Verknüpfungen:"; Flags: unchecked
Name: "autostart"; Description: "DevelopmentHub beim Windows-Start automatisch starten"; GroupDescription: "Autostart:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppIconFile}"; Tasks: startmenu
Name: "{userprograms}\{#MyAppName} deinstallieren"; Filename: "{uninstallexe}"; Tasks: startmenu
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppIconFile}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} jetzt starten"; Flags: nowait postinstall skipifsilent
