; Inno Setup script for FPM (Windows PC Manager)
; Canonical AppId preserved for seamless in-place upgrade.

#define MyAppName "FPM"
#define MyAppVersion "3.23.3.0"
#define MyAppPublisher "FPM"
#define MyAppURL "https://github.com/farmanullah1/FPM"
#define MyAppExeName "FPM.exe"
#define MyAppHelperExeName "FPM.Helper.exe"

[Setup]
AppId={{8A4F2C1E-74B2-4E8F-96E8-9F2B0F1C3A5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=no
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\publish\installer
OutputBaseFilename=FPM-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\app\src\PCManager\Assets\fpm.ico
LicenseFile=..\LICENSE
InfoBeforeFile=InfoBefore.txt
InfoAfterFile=InfoAfter.txt
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName};{#MyAppHelperExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "startuprun"; Description: "Start FPM with Windows"; GroupDescription: "Windows Integration:"; Flags: unchecked
Name: "toolbaronly"; Description: "Show the floating toolbar at sign-in"; GroupDescription: "Windows Integration:"; Flags: unchecked
Name: "scheduledscan"; Description: "Create a scan-only scheduled task under \\FPM\\"; GroupDescription: "Windows Integration:"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "FPM"; ValueData: """{app}\{#MyAppExeName}"" --no-splash"; Flags: uninsdeletevalue; Tasks: startuprun
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "FPM"; ValueData: """{app}\{#MyAppExeName}"" --toolbar-only"; Flags: uninsdeletevalue; Tasks: toolbaronly
Root: HKCU; Subkey: "Software\FPM"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekeyifempty

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  TaskCommand: String;
  ResultCode: Integer;

procedure RequestGracefulExit;
var
  ResultCode: Integer;
  OldExe: String;
begin
  OldExe := ExpandConstant('{app}\FPM.exe');
  if FileExists(OldExe) then
    Exec(OldExe, '--exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    RequestGracefulExit;
  end;

  if (CurStep = ssPostInstall) and IsTaskSelected('scheduledscan') then
  begin
    TaskCommand := '/TR "' + ExpandConstant('{app}\FPM.exe') + '" --scan';
    Exec('schtasks.exe',
      '/Create /F /TN "\FPM\FPM Scan" ' + TaskCommand + ' /SC DAILY',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RequestGracefulExit;
    Exec('schtasks.exe', '/Delete /F /TN "\FPM\FPM Scan"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    if MsgBox('Delete FPM user data from %LOCALAPPDATA%\FPM?', mbConfirmation,
      MB_YESNO or MB_DEFBUTTON2) = IDYES then
    begin
      DelTree(ExpandConstant('{localappdata}\FPM'), True, True, True);
    end;
  end;
end;
