#define Configuration GetEnv('CONFIGURATION')
#if Configuration == ""
#define Configuration "Release"
#endif

#define Version GetEnv('BUILD_VERSION')
#if Version == ""
#define Version "x.x.x.x"
#endif

[Setup]
AppName=Nogasm Chart
AppVersion={#Version}
AppPublisher=blackspherefollower
AppPublisherURL=https://github.com/blackspherefollower/NogasmChart
AppId={{0a5b92f6-51e8-11e9-aa5c-0bbc873d2d19}
UsePreviousAppDir=yes
DefaultDirName={pf}\NogasmChart
Uninstallable=yes
Compression=lzma2
SolidCompression=yes
OutputBaseFilename=nogasm-chart-installer
OutputDir=.\installer
LicenseFile=LICENSE


[Files]
Source: "NogasmChart\bin\{#Configuration}\net47\*.exe"; DestDir: "{app}"
Source: "NogasmChart\bin\{#Configuration}\net47\*.dll"; DestDir: "{app}"
Source: "NogasmChart\bin\{#Configuration}\net47\*.config"; DestDir: "{app}"
Source: "README.md"; DestDir: "{app}"; DestName: "Readme.txt"
Source: "LICENSE"; DestDir: "{app}"; DestName: "License.txt"

// [Run]
// Filename: "{app}\Readme.txt"; Description: "View the README file"; Flags: postinstall shellexec unchecked

[Code]

// Uninstall on install code taken from https://stackoverflow.com/a/2099805/4040754
////////////////////////////////////////////////////////////////////
function GetUninstallString(): String;
var
  sUnInstPath: String;
  sUnInstallString: String;
begin
  sUnInstPath := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#emit SetupSetting("AppId")}_is1');
  sUnInstallString := '';
  if not RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString) then
    RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;


/////////////////////////////////////////////////////////////////////
function IsUpgrade(): Boolean;
begin
  Result := (GetUninstallString() <> '');
end;


/////////////////////////////////////////////////////////////////////
function UnInstallOldVersion(): Integer;
var
  sUnInstallString: String;
  iResultCode: Integer;
begin
// Return Values:
// 1 - uninstall string is empty
// 2 - error executing the UnInstallString
// 3 - successfully executed the UnInstallString

  // default return value
  Result := 0;

  // get the uninstall string of the old app
  sUnInstallString := GetUninstallString();
  if sUnInstallString <> '' then begin
    sUnInstallString := RemoveQuotes(sUnInstallString);
    if Exec(sUnInstallString, '/SILENT /NORESTART /SUPPRESSMSGBOXES','', SW_HIDE, ewWaitUntilTerminated, iResultCode) then
      Result := 3
    else
      Result := 2;
  end else
    Result := 1;
end;

/////////////////////////////////////////////////////////////////////
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep=ssInstall) then
  begin
    if (IsUpgrade()) then
    begin
      UnInstallOldVersion();
    end;
  end;
end;
