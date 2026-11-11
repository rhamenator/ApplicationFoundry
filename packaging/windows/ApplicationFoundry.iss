#define AppName "Application Foundry"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
[Setup]
AppId={{AF4615EC-31A8-41C0-9662-A5B89418C716}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\Application Foundry
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\ApplicationFoundry.exe
OutputDir=..\..\artifacts
OutputBaseFilename=ApplicationFoundry-{#AppVersion}-windows-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
[Files]
Source: "..\..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Application Foundry"; Filename: "{app}\ApplicationFoundry.exe"; Parameters: "--desktop"
[Run]
Filename: "{app}\ApplicationFoundry.exe"; Parameters: "--desktop"; Description: "Launch Application Foundry"; Flags: nowait postinstall skipifsilent
