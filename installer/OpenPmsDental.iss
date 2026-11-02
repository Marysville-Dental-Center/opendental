; Open PMS - Dental: the workstation installer (Inno Setup 6).
; Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
;
; Compile it with tools\Build-Release.ps1. The script builds Release x86, checks the files, stages them in one folder and passes that
; folder as StagingDir, and the information page text (installer\setup-info.txt with NOTICE) as InfoFile. Everything in the staging
; folder is installed as it is; this script adds only FreeDentalConfig.xml and the uninstaller.
;
; Command line (besides Inno Setup's own switches, such as /SILENT, /VERYSILENT, /SUPPRESSMSGBOXES, /DIR=, /NOICONS, /TASKS=, /LOG=):
;   /DBSERVER=name      database server for FreeDentalConfig.xml (host, or host:port for a port other than 3306). Default localhost.
;   /DBNAME=name        database. Default opendental.
;   /DBUSER=name        database user. Default root. No password is ever written: the program asks for it at its first start.
;   /PREREQS=ask|download|skip
;                       a missing Visual C++ 2015-2022 x86 runtime or WebView2 runtime: ask (the default; a silent install skips),
;                       download and install them from Microsoft, or skip them with a warning.
;   /CURRENTUSER        install without administrator rights, for tests only (no Event Log source; pass /DIR= too).
; Setup never starts the program, silent or not. The office switch-over (adopting an Open Dental database) is not part of this setup.

#define AppVersion "0.0"
#define AppName "Open PMS - Dental"
#define ExeName "OpenPmsDental.exe"
#define ConfigFileName "FreeDentalConfig.xml"
#define EventLogSource "OpenPMS-Dental"
#define SourceUrl "https://github.com/Marysville-Dental-Center/opendental"
#ifndef StagingDir
  #define StagingDir "bin\staging"
#endif
#ifndef InfoFile
  #define InfoFile "bin\setup-info.txt"
#endif

[Setup]
; A new id of our own: this setup never upgrades or removes an Open Dental installation.
AppId={{D5640A25-F302-4CAD-96DC-84AAA8B22F98}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Open PMS - Dental contributors
AppPublisherURL={#SourceUrl}
AppSupportURL={#SourceUrl}/issues
AppUpdatesURL={#SourceUrl}/releases
AppCopyright=Copyright (C) 2003 and later years, Jordan Sparks, D.M.D., Open Dental Software, Inc. and contributors. Changes in Open PMS - Dental: Copyright (C) 2026 and later years, Open PMS - Dental contributors. GNU GPL v2 or later.
VersionInfoVersion=0.0.0.0
VersionInfoTextVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoProductVersion=0.0.0.0
VersionInfoProductTextVersion={#AppVersion}
VersionInfoCompany=Open PMS - Dental contributors
VersionInfoDescription={#AppName} Setup
VersionInfoCopyright=Open PMS - Dental contributors; a fork of Open Dental (c) Open Dental Software, Inc. and contributors. GNU GPL v2 or later.
; Never Open Dental's folder. The release channel and UpdateFileCopier work on this per-machine folder (docs/release-process.md).
DefaultDirName={commonpf32}\Open PMS - Dental
UsePreviousAppDir=yes
DirExistsWarning=no
DisableProgramGroupPage=yes
AllowNoIcons=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
; A release publishes every top-level file of the program folder to every workstation, so the uninstaller lives in a sub folder.
UninstallFilesDir={app}\uninstall
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#ExeName}
LicenseFile={#StagingDir}\LICENSE
InfoBeforeFile={#InfoFile}
OutputDir=bin
OutputBaseFilename=OpenPmsDental-{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Windows 10 version 1903 or later, or Windows 11 (README.md, Workstations).
MinVersion=10.0.18362
CloseApplications=yes
; Never start the program after setup, not even one that setup closed.
RestartApplications=no
SetupLogging=yes
ShowLanguageDialog=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#StagingDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Written by the [Code] below into {tmp} only when the program folder has no FreeDentalConfig.xml. Users may change this one file (the
; program saves the connection there); the folder itself keeps the default rights.
Source: "{tmp}\{#ConfigFileName}"; DestDir: "{app}"; Flags: external onlyifdoesntexist; Permissions: users-modify; Check: ShouldInstallConfig

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#ExeName}"; WorkingDir: "{app}"; Check: not WizardNoIcons
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#ExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
; The program's Event Log source (ProgramNames.EventLogSource). Registering a source needs administrator rights, so only an
; administrative install does it; the program's writes then need none.
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\EventLog\Application\{#EventLogSource}"; ValueType: expandsz; ValueName: "EventMessageFile"; ValueData: "%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\EventLogMessages.dll"; Flags: uninsdeletekey; Check: IsAdminInstallMode

[Code]
const
  NetFx48Release = 528040;
  VcRedistUrl = 'https://aka.ms/vs/17/release/vc_redist.x86.exe';
  VcRedistFile = 'vc_redist.x86.exe';
  VcRedistKey = 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86';
  WebView2Url = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703';
  WebView2File = 'MicrosoftEdgeWebview2Setup.exe';
  WebView2Key = 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  VcRedistName = 'Microsoft Visual C++ 2015-2022 Redistributable (x86)';
  WebView2Name = 'Microsoft Edge WebView2 Runtime';

var
  ConfigPage: TInputQueryWizardPage;
  PrereqPage: TInputOptionWizardPage;
  DownloadPage: TDownloadWizardPage;
  ProgressPage: TOutputProgressWizardPage;
  NeedVcRedist: Boolean;
  NeedWebView2: Boolean;
  DownloadPrereqs: Boolean;
  ConfigWanted: Boolean;
  PrereqWarnings: String;

{ ---------- Prerequisites ---------- }

function IsNetFx48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= NetFx48Release);
  if (not Result) and IsWin64 then
    Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= NetFx48Release);
end;

function IsVcRedistInstalled: Boolean;
var
  Installed, Major: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM32, VcRedistKey, 'Installed', Installed) and (Installed = 1)
    and RegQueryDWordValue(HKLM32, VcRedistKey, 'Major', Major) and (Major >= 14);
end;

function HasWebView2Version(const RootKey: Integer): Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(RootKey, WebView2Key, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0');
end;

function IsWebView2Installed: Boolean;
begin
  { Microsoft's documented check: the pv value of the runtime's client key, per machine (the 32-bit view) or per user. }
  Result := HasWebView2Version(HKLM32) or HasWebView2Version(HKCU);
  if (not Result) and IsWin64 then
    Result := HasWebView2Version(HKLM64);
end;

function MissingPrereqText: String;
begin
  Result := '';
  if NeedVcRedist then
    Result := Result + '- ' + VcRedistName + ': the DICOM codecs and Direct2dWrapper.dll need it.' + #13#10;
  if NeedWebView2 then
    Result := Result + '- ' + WebView2Name + ': every window that shows web content needs it.' + #13#10;
end;

procedure AddPrereqWarning(const Text: String);
begin
  Log('Prerequisite: ' + Text);
  PrereqWarnings := PrereqWarnings + #13#10 + '- ' + Text;
end;

{ True when Windows trusts the file's Authenticode signature and Microsoft Corporation signed it. }
function IsSignedByMicrosoft(const FileName: String): Boolean;
var
  ResultCode: Integer;
  Quoted: String;
begin
  Quoted := FileName;
  StringChangeEx(Quoted, '''', '''''', True);
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$s = Get-AuthenticodeSignature -LiteralPath ''' + Quoted + '''; ' +
    'if (($s.Status -eq ''Valid'') -and ($s.SignerCertificate.Subject -like ''*O=Microsoft Corporation*'')) { exit 0 } else { exit 1 }"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
  if not Result then
    Log('The signature check of ' + FileName + ' failed (' + IntToStr(ResultCode) + ').');
end;

function DownloadPrereq(const Url, FileName, Name: String): Boolean;
begin
  Result := False;
  try
    if WizardSilent then
      DownloadTemporaryFile(Url, FileName, '', nil)
    else begin
      DownloadPage.Clear;
      DownloadPage.Add(Url, FileName, '');
      DownloadPage.Show;
      try
        DownloadPage.Download;
      finally
        DownloadPage.Hide;
      end;
    end;
    Result := True;
  except
    AddPrereqWarning(Name + ': the download from ' + Url + ' failed (' + GetExceptionMessage + ').');
  end;
end;

function RunPrereq(const FileName, Params, Name: String): Boolean;
var
  ResultCode: Integer;
  Path: String;
  Started: Boolean;
begin
  Result := False;
  Path := ExpandConstant('{tmp}\') + FileName;
  if not IsSignedByMicrosoft(Path) then begin
    AddPrereqWarning(Name + ': the downloaded file has no valid Microsoft signature, so it was not run.');
    Exit;
  end;
  if not WizardSilent then begin
    ProgressPage.SetText('Installing ' + Name + '...', '');
    ProgressPage.Show;
  end;
  try
    Started := Exec(Path, Params, '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
  finally
    if not WizardSilent then
      ProgressPage.Hide;
  end;
  if not Started then begin
    AddPrereqWarning(Name + ': its installer could not be started (' + SysErrorMessage(ResultCode) + ').');
    Exit;
  end;
  Log(Name + ': installer exit code ' + IntToStr(ResultCode));
  { 0 done; 3010 and 1641 done, a restart is needed; 1638 a newer version is already installed. }
  Result := (ResultCode = 0) or (ResultCode = 3010) or (ResultCode = 1641) or (ResultCode = 1638);
  if not Result then
    AddPrereqWarning(Name + ': its installer ended with code ' + IntToStr(ResultCode) + '.');
end;

{ Nested ifs, not "and": a component that is present must never be downloaded, whatever the boolean evaluation. }
procedure InstallPrereqs;
begin
  if NeedVcRedist then begin
    if DownloadPrereq(VcRedistUrl, VcRedistFile, VcRedistName) then begin
      if RunPrereq(VcRedistFile, '/install /quiet /norestart', VcRedistName) then
        NeedVcRedist := not IsVcRedistInstalled;
    end;
  end;
  if NeedWebView2 then begin
    if DownloadPrereq(WebView2Url, WebView2File, WebView2Name) then begin
      if RunPrereq(WebView2File, '/silent /install', WebView2Name) then
        NeedWebView2 := not IsWebView2Installed;
    end;
  end;
  if NeedVcRedist then
    AddPrereqWarning(VcRedistName + ' is still missing. Install it from ' + VcRedistUrl);
  if NeedWebView2 then
    AddPrereqWarning(WebView2Name + ' is still missing. Install it from ' + WebView2Url);
end;

{ ---------- The program folder ---------- }

function FolderIsEmpty(const Dir: String): Boolean;
var
  FindRec: TFindRec;
begin
  Result := True;
  if FindFirst(AddBackslash(Dir) + '*', FindRec) then begin
    try
      repeat
        if (FindRec.Name <> '.') and (FindRec.Name <> '..') then begin
          Result := False;
          Break;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

{ Uninstalling removes the whole program folder, so the folder must be new, empty, or an earlier installation of this program. }
function CheckInstallFolder(const Dir: String): String;
begin
  Result := '';
  if Length(RemoveBackslash(Dir)) <= 2 then
    Result := 'Choose a folder, not the root of a drive: uninstalling removes the whole program folder.'
  else if DirExists(Dir) and (not FolderIsEmpty(Dir)) and (not FileExists(AddBackslash(Dir) + '{#ExeName}')) then
    Result := 'The folder ' + Dir + ' already holds other files. Choose a new or empty folder, or the folder of an earlier ' +
      '{#AppName} installation: uninstalling removes the whole program folder.';
end;

function IsLetterName(const Name: String): Boolean;
begin
  Result := (Length(Name) = 1) and (Pos(Uppercase(Name), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ') > 0);
end;

{ An A to Z images folder holds one folder per letter (A\, B\, ...) for the patients' folders. }
function HasLetterSubfolder(const Dir: String): Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;
  if FindFirst(AddBackslash(Dir) + '*', FindRec) then begin
    try
      repeat
        if ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and IsLetterName(FindRec.Name) then begin
          Result := True;
          Break;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

{ Removes everything in the program folder, including files the self-update added after setup, except a folder that looks like an
  A to Z images folder (an office could have pointed DocPath into the program folder). The database is never touched. }
procedure RemoveProgramFolder;
var
  AppDir, Path, Kept: String;
  FindRec: TFindRec;
  Names: TStringList;
  Folders: TStringList;
  I: Integer;
begin
  AppDir := RemoveBackslash(ExpandConstant('{app}'));
  if (Length(AppDir) <= 2) or (not DirExists(AppDir)) then
    Exit;
  Names := TStringList.Create;
  Folders := TStringList.Create;
  try
    if FindFirst(AppDir + '\*', FindRec) then begin
      try
        repeat
          if (FindRec.Name <> '.') and (FindRec.Name <> '..') then begin
            if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
              Folders.Add(FindRec.Name)
            else
              Names.Add(FindRec.Name);
          end;
        until not FindNext(FindRec);
      finally
        FindClose(FindRec);
      end;
    end;
    for I := 0 to Names.Count - 1 do begin
      Path := AppDir + '\' + Names[I];
      if not DeleteFile(Path) then
        Log('Could not remove ' + Path);
    end;
    Kept := '';
    for I := 0 to Folders.Count - 1 do begin
      Path := AppDir + '\' + Folders[I];
      if IsLetterName(Folders[I]) or HasLetterSubfolder(Path) then begin
        Log('Kept, it looks like an A to Z images folder: ' + Path);
        Kept := Kept + #13#10 + Path;
      end
      else if not DelTree(Path, True, True, True) then
        Log('Could not remove ' + Path);
    end;
  finally
    Folders.Free;
    Names.Free;
  end;
  if not RemoveDir(AppDir) then
    Log('The program folder is not empty yet: ' + AppDir);
  if Kept <> '' then
    SuppressibleMsgBox('These folders look like an A to Z images folder, so they were not removed:' + Kept, mbInformation, MB_OK, IDOK);
end;

{ ---------- FreeDentalConfig.xml ---------- }

function XmlEscape(const S: String): String;
begin
  Result := S;
  StringChangeEx(Result, '&', '&amp;', True);
  StringChangeEx(Result, '<', '&lt;', True);
  StringChangeEx(Result, '>', '&gt;', True);
  StringChangeEx(Result, '"', '&quot;', True);
  StringChangeEx(Result, '''', '&apos;', True);
end;

{ '' when the value may go into the connection settings, otherwise why not. }
function CheckConnectionValue(const Caption, Value: String; const IsDatabase: Boolean): String;
var
  I: Integer;
  C: Char;
begin
  Result := '';
  if Value = '' then begin
    Result := Caption + ' is empty.';
    Exit;
  end;
  for I := 1 to Length(Value) do begin
    C := Value[I];
    if (Ord(C) < 32) or (C = ';') or (C = ' ') then begin
      Result := Caption + ' may not contain spaces, semicolons or control characters.';
      Exit;
    end;
    if IsDatabase and not (((C >= 'a') and (C <= 'z')) or ((C >= 'A') and (C <= 'Z')) or ((C >= '0') and (C <= '9')) or (C = '_') or (C = '$') or (C = '-')) then begin
      Result := Caption + ' may contain only letters, digits, _, $ and -.';
      Exit;
    end;
  end;
end;

function ConfigServer: String;
begin
  Result := Trim(ConfigPage.Values[0]);
end;

function ConfigDatabase: String;
begin
  Result := Trim(ConfigPage.Values[1]);
end;

function ConfigUser: String;
begin
  Result := Trim(ConfigPage.Values[2]);
end;

function CheckConfigValues: String;
begin
  Result := CheckConnectionValue('Server', ConfigServer, False);
  if Result = '' then
    Result := CheckConnectionValue('Database', ConfigDatabase, True);
  if Result = '' then
    Result := CheckConnectionValue('User', ConfigUser, False);
end;

{ The same layout the program writes (CentralConnections.TrySaveConnectionSettings), with no password. NoShowOnStartup=False makes the
  program show Choose Database at its first start, where the user enters the password. }
procedure WriteConfigToTemp;
var
  Lines: TArrayOfString;
  FileName: String;
begin
  SetArrayLength(Lines, 13);
  Lines[0] := '<?xml version="1.0" encoding="utf-8"?>';
  Lines[1] := '<ConnectionSettings>';
  Lines[2] := '    <DatabaseConnection>';
  Lines[3] := '        <ComputerName>' + XmlEscape(ConfigServer) + '</ComputerName>';
  Lines[4] := '        <Database>' + XmlEscape(ConfigDatabase) + '</Database>';
  Lines[5] := '        <User>' + XmlEscape(ConfigUser) + '</User>';
  Lines[6] := '        <Password></Password>';
  Lines[7] := '        <MySQLPassHash></MySQLPassHash>';
  Lines[8] := '        <NoShowOnStartup>False</NoShowOnStartup>';
  Lines[9] := '    </DatabaseConnection>';
  Lines[10] := '    <DatabaseType>MySql</DatabaseType>';
  Lines[11] := '    <UseDynamicMode>False</UseDynamicMode>';
  Lines[12] := '</ConnectionSettings>';
  FileName := ExpandConstant('{tmp}\{#ConfigFileName}');
  if not SaveStringsToUTF8FileWithoutBOM(FileName, Lines, False) then
    RaiseException('Could not write ' + FileName);
  Log('Wrote {#ConfigFileName}: server ' + ConfigServer + ', database ' + ConfigDatabase + ', user ' + ConfigUser);
end;

function ShouldInstallConfig: Boolean;
begin
  Result := ConfigWanted;
end;

function ConfigExistsInFolder: Boolean;
begin
  Result := FileExists(AddBackslash(WizardDirValue) + '{#ConfigFileName}');
end;

{ ---------- Setup events ---------- }

function InitializeSetup: Boolean;
begin
  Result := True;
  if not IsNetFx48Installed then begin
    Log('.NET Framework 4.8 is missing (NDP\v4\Full Release below 528040).');
    SuppressibleMsgBox('{#AppName} needs the Microsoft .NET Framework 4.8, which this computer does not have.' + #13#10#13#10 +
      'Windows 10 version 1903 and later and Windows 11 include it. Install it from Microsoft ' +
      '(https://dotnet.microsoft.com/download/dotnet-framework/net48), restart the computer, then run this setup again.',
      mbCriticalError, MB_OK, IDOK);
    Result := False;
    Exit;
  end;
  NeedVcRedist := not IsVcRedistInstalled;
  NeedWebView2 := not IsWebView2Installed;
  if NeedVcRedist then
    Log(VcRedistName + ': missing')
  else
    Log(VcRedistName + ': installed');
  if NeedWebView2 then
    Log(WebView2Name + ': missing')
  else
    Log(WebView2Name + ': installed');
end;

procedure InitializeWizard;
var
  Mode: String;
begin
  ConfigPage := CreateInputQueryPage(wpSelectTasks, 'Database connection', 'Which database does this computer use?',
    'Setup writes these values to {#ConfigFileName} in the program folder. It never stores a password: the program asks for it ' +
    'the first time it starts.' + #13#10#13#10 + 'Server: the database server''s name or address (host:port for a port other than 3306).');
  ConfigPage.Add('&Server:', False);
  ConfigPage.Add('&Database:', False);
  ConfigPage.Add('&User:', False);
  ConfigPage.Values[0] := ExpandConstant('{param:DBSERVER|localhost}');
  ConfigPage.Values[1] := ExpandConstant('{param:DBNAME|opendental}');
  ConfigPage.Values[2] := ExpandConstant('{param:DBUSER|root}');

  PrereqPage := CreateInputOptionPage(ConfigPage.ID, 'Microsoft components', 'Components the program needs are missing.',
    'This computer does not have:' + #13#10 + MissingPrereqText + #13#10 +
    'Setup can download them from Microsoft and install them now (an internet connection is needed), or you can install them later.',
    True, False);
  PrereqPage.Add('Download and install them now from Microsoft');
  PrereqPage.Add('Skip them for now (the program does not work fully until they are installed)');
  Mode := Lowercase(ExpandConstant('{param:PREREQS|ask}'));
  if Mode = 'skip' then
    PrereqPage.SelectedValueIndex := 1
  else
    PrereqPage.SelectedValueIndex := 0;

  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
  ProgressPage := CreateOutputProgressPage('Microsoft components', 'Installing the components the program needs.');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = ConfigPage.ID then
    Result := ConfigExistsInFolder
  else if PageID = PrereqPage.ID then
    Result := not (NeedVcRedist or NeedWebView2);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Error, Mode: String;
begin
  Result := True;
  if CurPageID = wpSelectDir then begin
    Error := CheckInstallFolder(WizardDirValue);
    if Error <> '' then begin
      Log(Error);
      SuppressibleMsgBox(Error, mbError, MB_OK, IDOK);
      Result := False;
    end;
  end
  else if CurPageID = ConfigPage.ID then begin
    Error := CheckConfigValues;
    if Error <> '' then begin
      Log('Database connection: ' + Error);
      SuppressibleMsgBox(Error, mbError, MB_OK, IDOK);
      Result := False;
    end;
  end
  else if CurPageID = PrereqPage.ID then begin
    if WizardSilent then begin
      Mode := Lowercase(ExpandConstant('{param:PREREQS|ask}'));
      DownloadPrereqs := (Mode = 'download');
    end
    else
      DownloadPrereqs := (PrereqPage.SelectedValueIndex = 0);
    if not DownloadPrereqs then begin
      Log('Prerequisites skipped: ' + MissingPrereqText);
      if not WizardSilent then
        SuppressibleMsgBox('The program does not work fully until these are installed:' + #13#10 + MissingPrereqText + #13#10 +
          'Install them later from Microsoft:' + #13#10 + VcRedistUrl + #13#10 + WebView2Url, mbInformation, MB_OK, IDOK);
    end;
  end
  else if CurPageID = wpReady then begin
    if DownloadPrereqs and (NeedVcRedist or NeedWebView2) then begin
      InstallPrereqs;
      if (PrereqWarnings <> '') and (not WizardSilent) then
        SuppressibleMsgBox('Setup continues, but:' + PrereqWarnings, mbInformation, MB_OK, IDOK);
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  { The folder check again, for an install whose folder page was skipped (silent installs, or the folder of an earlier install). }
  Result := CheckInstallFolder(ExpandConstant('{app}'));
  if Result <> '' then
    Log(Result);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then begin
    ConfigWanted := not FileExists(ExpandConstant('{app}\{#ConfigFileName}'));
    if ConfigWanted then
      WriteConfigToTemp
    else
      Log('{#ConfigFileName} exists already and is kept as it is.');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RemoveProgramFolder;
end;
