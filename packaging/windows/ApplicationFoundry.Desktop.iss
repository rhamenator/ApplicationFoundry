#define AppName "Application Foundry Desktop Prototype"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
[Setup]
AppId={{8F2961EA-E064-440D-8624-8154D639EECF}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\Application Foundry Desktop Prototype
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\ApplicationFoundry.Desktop.exe
OutputDir=..\..\artifacts
OutputBaseFilename=ApplicationFoundry-{#AppVersion}-windows-desktop-prototype-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
[Files]
Source: "..\..\publish-desktop\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Application Foundry Desktop Prototype"; Filename: "{app}\ApplicationFoundry.Desktop.exe"
[Run]
Filename: "{app}\ApplicationFoundry.Desktop.exe"; Description: "Launch Application Foundry Desktop Prototype"; Flags: nowait postinstall skipifsilent
