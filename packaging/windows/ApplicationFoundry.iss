#define AppName "Application Foundry"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
[Setup]
UninstallDisplayIcon={app}\app-icon.ico
SetupIconFile=app-icon.ico
AppId={{AF4615EC-31A8-41C0-9662-A5B89418C716}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\Application Foundry
DefaultGroupName={#AppName}
OutputDir=..\..\artifacts
OutputBaseFilename=ApplicationFoundry-{#AppVersion}-windows-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
[Files]
Source: "app-icon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Application Foundry"; Filename: "{app}\ApplicationFoundry.exe"; IconFilename: "{app}\app-icon.ico"
