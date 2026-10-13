#define ProductName "Oberon Remote Extended"
#ifndef ProductVersion
  #define ProductVersion "1.0.0-preview.7"
#endif

[Setup]
AppId={{3D9E95A5-7429-4FAE-8A3A-1E0C82D4B9CF}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppVerName={#ProductName} {#ProductVersion}
AppPublisher=katakishi0
AppPublisherURL=https://github.com/katakishi0/OberonRemote
AppSupportURL=https://github.com/katakishi0/OberonRemote/issues
AppUpdatesURL=https://github.com/katakishi0/OberonRemote/releases
DefaultDirName={localappdata}\Programs\Oberon Extended
DefaultGroupName=Oberon Extended
DisableProgramGroupPage=yes
OutputDir=..\..\artifacts\Installer
OutputBaseFilename=Oberon_Extended_Setup_Windows_x64
SetupIconFile=..\Assets\oberon-extended.ico
UninstallDisplayIcon={app}\Oberon.Extended.exe
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\..\LICENSE
ShowLanguageDialog=no
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no
ChangesEnvironment=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\..\artifacts\OberonExtended\*"; DestDir: "{app}"; Excludes: "PREVIEW.png"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Oberon Remote Extended"; Filename: "{app}\Oberon.Extended.exe"
Name: "{autodesktop}\Oberon Remote Extended"; Filename: "{app}\Oberon.Extended.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Oberon.Extended.exe"; Description: "{cm:LaunchProgram,Oberon Extended}"; Flags: nowait postinstall skipifsilent
