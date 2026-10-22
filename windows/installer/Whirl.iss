; Inno Setup script for Whirl. Built by CI:  iscc /DAppVersion=1.0.0 /DSourceDir=..\publish Whirl.iss
; Installs for the current user only — no administrator rights, nothing outside the user's profile.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

[Setup]
AppId={{dab2fe01-7731-423b-b7e8-6d42f4dcbc8b}
AppName=Whirl
AppVersion={#AppVersion}
AppVerName=Whirl {#AppVersion}
AppPublisher=Whirl contributors
AppPublisherURL=https://github.com/nico43245/Whirl
AppSupportURL=https://github.com/nico43245/Whirl/issues
DefaultDirName={localappdata}\Programs\Whirl
DefaultGroupName=Whirl
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\..\dist
OutputBaseFilename=Whirl-{#AppVersion}-Windows-Setup
SetupIconFile=..\src\Whirl\Assets\Whirl.ico
UninstallDisplayIcon={app}\Whirl.exe
LicenseFile=..\..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Tasks]
Name: "autostart"; Description: "Start Whirl when I sign in to Windows"; GroupDescription: "Options:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Options:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Whirl"; Filename: "{app}\Whirl.exe"
Name: "{userdesktop}\Whirl"; Filename: "{app}\Whirl.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Whirl"; \
    ValueData: """{app}\Whirl.exe"" --background"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\Whirl.exe"; Description: "Start Whirl now"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM Whirl.exe /F"; Flags: runhidden; RunOnceId: "StopWhirl"

[UninstallDelete]
; Settings live in %APPDATA%\Whirl — remove them so an uninstall leaves nothing behind.
Type: filesandordirs; Name: "{userappdata}\Whirl"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // Also clear "Start with Windows" if it was turned on later from Preferences.
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Whirl');
end;
