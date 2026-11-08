; Open PMS - Dental: the workstation installer (Inno Setup 6).
; Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
;
; Compile it with tools\Build-Release.ps1. The script builds Release x86, checks the files, stages them in one folder and passes that
; folder as StagingDir, the information page text (installer\setup-info.txt with NOTICE) as InfoFile, and the License Agreement page text
; (installer\setup-license.txt: how the program is licensed as installed, then LICENSE-GPL-3.0.txt) as LicenseTextFile. Everything in the
; staging folder is installed as it is; this script adds only FreeDentalConfig.xml and the uninstaller, and on an upgrade deletes the
; files of an earlier release that this one no longer ships ([InstallDelete]).
;
; Command line (besides Inno Setup's own switches, such as /SILENT, /VERYSILENT, /SUPPRESSMSGBOXES, /DIR=, /NOICONS, /TASKS=, /LOG=):
;   /DBSERVER=name      database server for FreeDentalConfig.xml (host, or host:port for a port other than 3306). Default localhost.
;   /DBNAME=name        database. Default opendental.
;   /DBUSER=name        database user. Default opmsd, the program's own account in the server guide (docs/db-connection-security.md,
;                       section 5); the program no longer uses root. The connection settings tool warns about root for a server
;                       over the network.
;   /DBCA=file          the office CA certificate file. Needed for a server over the network (docs/specs/db-connection-security.md).
;   /DBPFX=file         the client certificate (.pfx) to import into the computer's store, or
;   /DBTHUMBPRINT=hex   a client certificate already in LocalMachine\My. Neither: the database password is used instead.
;   /DBSECRETSFILE=file lines PfxPassword=... and DbPassword=... for a silent install. Setup only passes the path on to the connection
;                       settings tool, which reads the file and never changes it: the caller deletes it. Secrets never go on a command line.
;   Setup writes FreeDentalConfig.xml without a password. An administrative install then runs OpenPmsDentalDbSettings.exe (the connection
;   settings tool) for a server over the network or when any of the DB* security switches is given: its window, filled from these values,
;   in an interactive install, or /silent in a silent one. For a server on this computer an interactive install opens the window in
;   password mode (the program never asks for the database password; Cancel keeps it blank). On an upgrade (FreeDentalConfig.xml exists)
;   setup never passes the connection page's defaults: with any of /DBCA, /DBPFX, /DBTHUMBPRINT, /DBSECRETSFILE it runs the tool with only
;   the switches given (/DBSERVER, /DBNAME, /DBUSER too when given), /silent in a silent install; without them it runs the tool's /check
;   and, interactive, offers its window when the settings do not meet the connection rule.
;   /PREREQS=ask|download|skip
;                       a missing Visual C++ 2015-2022 x86 runtime: ask (the default; a silent install skips), download and install it
;                       from Microsoft, or skip it with a warning.
; The Microsoft Edge WebView2 Runtime (part of Windows 11) is only checked: when it is missing, setup tells the administrator how to install
; it (and logs it). Setup never downloads it.
;   /CURRENTUSER        install without administrator rights, for tests only (no Event Log source; pass /DIR= too).
; Setup never starts the program, silent or not. The office switch-over (adopting an Open Dental database) is not part of this setup.
; Exit code: Inno Setup's own (0 done, 1 to 8 setup did not finish), and 10 for a silent install that finished but whose connection settings
; step did not: the connection settings tool failed or could not start, or, on an upgrade without security switches, the kept settings fail
; the connection rule (/check). The files are installed; setup's /LOG has the tool's result line and exit code. Run the tool as an
; administrator to finish (docs/db-connection-security.md, section 6). Interactive installs show the failure and end with 0.

#define AppVersion "0.0"
#define AppName "Open PMS - Dental"
#define ExeName "OpenPmsDental.exe"
#define ConfigFileName "FreeDentalConfig.xml"
#define DbSettingsExeName "OpenPmsDentalDbSettings.exe"
#define EventLogSource "OpenPMS-Dental"
#define SourceUrl "https://github.com/Marysville-Dental-Center/opendental"
#ifndef StagingDir
  #define StagingDir "bin\staging"
#endif
#ifndef InfoFile
  #define InfoFile "bin\setup-info.txt"
#endif
#ifndef LicenseTextFile
  #define LicenseTextFile "bin\setup-license.txt"
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
AppCopyright=Copyright (C) 2003 and later years, Jordan Sparks, D.M.D., Open Dental Software, Inc. and contributors. Changes in Open PMS - Dental: Copyright (C) 2026 and later years, Open PMS - Dental contributors. Source files: GNU GPL v2 or later. As installed, with PDFsharp and MigraDoc (GNU AGPL v3): GNU GPL v3.
VersionInfoVersion=0.0.0.0
VersionInfoTextVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoProductVersion=0.0.0.0
VersionInfoProductTextVersion={#AppVersion}
VersionInfoCompany=Open PMS - Dental contributors
VersionInfoDescription={#AppName} Setup
VersionInfoCopyright=Open PMS - Dental contributors; a fork of Open Dental (c) Open Dental Software, Inc. and contributors. Source files: GNU GPL v2 or later. As installed, with PDFsharp and MigraDoc (GNU AGPL v3): GNU GPL v3.
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
; The Open PMS - Dental logo (branding/README.md): setup's own icon and the wizard images, at 100% and 200% (Setup picks the one for the
; screen's dpi). They are part of setup only, never installed files.
SetupIconFile=..\branding\app.ico
WizardImageFile=..\branding\installer-wizard.bmp,..\branding\installer-wizard-2x.bmp
WizardSmallImageFile=..\branding\installer-small.bmp,..\branding\installer-small-2x.bmp
; The License Agreement page: as installed, with PDFsharp and MigraDoc (GNU AGPL v3), the program is conveyed under the GNU GPL v3, so the page
; shows that text, after a header that names the AGPL part and the GPL v2 or later of the program's own source files (LICENSE).
LicenseFile={#LicenseTextFile}
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

[InstallDelete]
; Files of an earlier release that this one no longer ships (ForkReleases.ListRetiredFiles). An upgrade deletes them, so the program folder
; holds only this release's files: the release channel publishes every top-level file of it. Nothing loads them any more.
; The storage library of v0.0, renamed OpenPmsDentalStorage.dll on 2026-11-02 (docs\port-notes\RENAME.md).
Type: files; Name: "{app}\OpenPmsDentalCloud.dll"
; Libraries v0.0 installed that the program no longer uses (the tier 1 library updates). tests\LibraryBindingTests checks that the release
; ships none of them.
Type: files; Name: "{app}\Tamir.SharpSSH.dll"
Type: files; Name: "{app}\Org.Mentalis.Security.dll"
Type: files; Name: "{app}\DiffieHellman.dll"
Type: files; Name: "{app}\OpenPop.dll"
; fo-dicom 4 (MS-PL), replaced by the in-house DICOM reader (docs/imaging/dicom-reader.md).
Type: files; Name: "{app}\Dicom.Core.dll"
Type: files; Name: "{app}\Dicom.Native.dll"
; NDde (its own shared source licence, which does not fit the GPL), removed with the DentX, Dexis Integrator and Vipersoft program links
; (owner decision of 2026-11-07).
Type: files; Name: "{app}\NDde.dll"
; PDFsharp and MigraDoc 1.2 (0.0), which PDFsharp and MigraDoc 6.5 replace under other file names (THIRD-PARTY-NOTICES.txt).
Type: files; Name: "{app}\PdfSharp.dll"
Type: files; Name: "{app}\PdfSharp.Charting.dll"
Type: files; Name: "{app}\MigraDoc.Rendering.dll"
; The spell checker: NHunspell and its native Hunspell (v0.0 shipped the x86 one only), replaced by the managed WeCantSpell.Hunspell.dll.
Type: files; Name: "{app}\NHunspell.dll"
Type: files; Name: "{app}\Hunspellx86.dll"

[Files]
Source: "{#StagingDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Written by the [Code] below into {tmp} only when the program folder has no FreeDentalConfig.xml. It keeps the folder's rights: only an
; administrator changes the connection settings, with the connection settings tool (owner decision Q7, docs/specs/db-connection-security.md).
Source: "{tmp}\{#ConfigFileName}"; DestDir: "{app}"; Flags: external onlyifdoesntexist; Check: ShouldInstallConfig

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
  { Microsoft's Evergreen bootstrapper, for the administrator. Setup only names it and never downloads it. }
  WebView2Url = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703';
  WebView2Key = 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  VcRedistName = 'Microsoft Visual C++ 2015-2022 Redistributable (x86)';
  WebView2Name = 'Microsoft Edge WebView2 Runtime';
  { Setup's exit code when a silent install finished but its connection settings step did not: the tool failed or could not start, or the
    kept settings fail the connection rule (GetCustomSetupExitCode). Inno Setup's own codes are 0 to 8. }
  DbSettingsFailedExitCode = 10;

var
  ConfigPage: TInputQueryWizardPage;
  SecurityPage: TInputFileWizardPage;
  PrereqPage: TInputOptionWizardPage;
  WebView2Page: TOutputMsgWizardPage;
  DownloadPage: TDownloadWizardPage;
  ProgressPage: TOutputProgressWizardPage;
  NeedVcRedist: Boolean;
  NeedWebView2: Boolean;
  DownloadPrereqs: Boolean;
  ConfigWanted: Boolean;
  PrereqWarnings: String;
  DbSettingsFailed: Boolean;

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
    Result := Result + '- ' + VcRedistName + ': Direct2dWrapper.dll, the program''s drawing, needs it.' + #13#10;
end;

{ What the administrator does when the WebView2 Runtime is missing. Setup goes on without it and downloads nothing. }
function WebView2MissingText: String;
begin
  Result := 'This computer does not have the ' + WebView2Name + '. Windows 11 includes it.' + #13#10#13#10 +
    'Every window of the program that shows a page needs it: the wiki, the email previews, PDFs in the Imaging module, and the ' +
    'pages of vendors (card entry, eRx, reports). Until it is installed, those windows say that it is missing.' + #13#10#13#10 +
    'Setup does not download it. An administrator installs it from Microsoft: run the Evergreen Bootstrapper (' + WebView2Url + '), ' +
    'or deploy Microsoft''s Evergreen Standalone Installer for the x86 or x64 runtime, or install it through Windows Update or the ' +
    'office''s device management.';
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
  if NeedVcRedist then
    AddPrereqWarning(VcRedistName + ' is still missing. Install it from ' + VcRedistUrl);
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

{ The layout the program reads (DbConnectionConfig), with no password. The connection settings tool, which setup runs after the files are
  installed (SetUpConnection), adds the office CA and the client certificate or the password stored with DPAPI; the program never asks for
  the password. NoShowOnStartup stays False as v0.0 wrote it; Release builds ignore it (they have no Choose Database). }
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

{ ---------- The connection settings tool (docs/specs/db-connection-security.md) ---------- }

{ A server on this computer, as setup can tell without asking DNS: localhost, 127.x.x.x or ::1, with or without a port. The tool and the
  program decide with the real rule; this only decides whether setup needs the tool at all. }
function IsLoopbackServer(const Server: String): Boolean;
var
  S: String;
begin
  S := Lowercase(Trim(Server));
  Result := (S = 'localhost') or (Pos('localhost:', S) = 1) or (Pos('127.', S) = 1) or (S = '::1') or (Pos('[::1]', S) = 1);
end;

function ConfigCaFile: String;
begin
  Result := Trim(SecurityPage.Values[0]);
end;

function ConfigPfxFile: String;
begin
  Result := Trim(SecurityPage.Values[1]);
end;

function HasSecuritySwitches: Boolean;
begin
  Result := (ConfigCaFile <> '') or (ConfigPfxFile <> '') or (ExpandConstant('{param:DBTHUMBPRINT|}') <> '') or
    (ExpandConstant('{param:DBSECRETSFILE|}') <> '');
end;

{ The tool's switches with the values of the connection pages. Never a secret: those come from the secrets file or the tool's own window. }
function DbSettingsParams: String;
var
  Thumbprint, SecretsFile: String;
begin
  Result := '/server=' + AddQuotes(ConfigServer) + ' /database=' + AddQuotes(ConfigDatabase) + ' /user=' + AddQuotes(ConfigUser);
  if ConfigCaFile <> '' then
    Result := Result + ' /ca=' + AddQuotes(ConfigCaFile);
  Thumbprint := ExpandConstant('{param:DBTHUMBPRINT|}');
  if ConfigPfxFile <> '' then
    Result := Result + ' /pfx=' + AddQuotes(ConfigPfxFile)
  else if Thumbprint <> '' then
    Result := Result + ' /thumbprint=' + AddQuotes(Thumbprint);
  SecretsFile := ExpandConstant('{param:DBSECRETSFILE|}');
  if SecretsFile <> '' then
    Result := Result + ' /secretsfile=' + AddQuotes(SecretsFile);
end;

{ Runs the tool from the program folder and waits; its result line goes to setup's log. Returns its exit code, or -1 when it could not be
  started. Setup is elevated here (administrative installs only), which the tool's manifest needs. }
function RunDbSettings(const Params: String; const ShowWindow: Boolean): Integer;
var
  ToolPath, LogPath, LogText: String;
  ResultCode, ShowCmd: Integer;
  ToolLog: AnsiString;
begin
  Result := -1;
  ToolPath := ExpandConstant('{app}\{#DbSettingsExeName}');
  LogPath := ExpandConstant('{tmp}\dbsettings.log');
  DeleteFile(LogPath);
  if ShowWindow then
    ShowCmd := SW_SHOW
  else
    ShowCmd := SW_HIDE;
  Log('Running {#DbSettingsExeName} ' + Params);
  if not Exec(ToolPath, Params + ' /log=' + AddQuotes(LogPath), ExpandConstant('{app}'), ShowCmd, ewWaitUntilTerminated, ResultCode) then begin
    Log('{#DbSettingsExeName} could not be started: ' + SysErrorMessage(ResultCode));
    Exit;
  end;
  Result := ResultCode;
  if LoadStringFromFile(LogPath, ToolLog) then begin
    LogText := String(ToolLog);
    Log(Trim(LogText));
  end;
  Log('{#DbSettingsExeName} exit code ' + IntToStr(ResultCode));
end;

function DbSettingsLogText: String;
var
  ToolLog: AnsiString;
begin
  Result := '';
  if LoadStringFromFile(ExpandConstant('{tmp}\dbsettings.log'), ToolLog) then
    Result := Trim(String(ToolLog));
end;

{ True when a security switch is on setup's command line. On an upgrade the security page is skipped, so only these count. }
function HasSecurityParams: Boolean;
begin
  Result := (ExpandConstant('{param:DBCA|}') <> '') or (ExpandConstant('{param:DBPFX|}') <> '') or
    (ExpandConstant('{param:DBTHUMBPRINT|}') <> '') or (ExpandConstant('{param:DBSECRETSFILE|}') <> '');
end;

{ ' /switch="value"' when setup's command line has /Param=value, else ''. }
function GivenSwitch(const Param, Switch: String): String;
var
  Value: String;
begin
  Value := Trim(ExpandConstant('{param:' + Param + '|}'));
  if Value <> '' then
    Result := ' ' + Switch + '=' + AddQuotes(Value)
  else
    Result := '';
end;

{ The tool's switches for an upgrade: only those given on setup's command line, never the connection page's defaults (localhost,
  opendental, opmsd), which would replace the computer's existing connection. Never a secret. }
function UpgradeParams: String;
begin
  Result := GivenSwitch('DBSERVER', '/server') + GivenSwitch('DBNAME', '/database') + GivenSwitch('DBUSER', '/user') + GivenSwitch('DBCA', '/ca');
  if ExpandConstant('{param:DBPFX|}') <> '' then
    Result := Result + GivenSwitch('DBPFX', '/pfx')
  else
    Result := Result + GivenSwitch('DBTHUMBPRINT', '/thumbprint');
  Result := Trim(Result + GivenSwitch('DBSECRETSFILE', '/secretsfile'));
end;

{ Shows (suppressibly) that the tool did not finish; its result line is in setup's log already. Cancel (6) is the user's choice. A silent
  install then ends with DbSettingsFailedExitCode. }
procedure ReportDbSettingsFailure(const ToolExit: Integer);
begin
  if (ToolExit <> 0) and (ToolExit <> 6) then begin
    if WizardSilent then
      DbSettingsFailed := True;
    SuppressibleMsgBox('The connection settings were not completed (' + IntToStr(ToolExit) + '): ' + DbSettingsLogText + #13#10#13#10 +
      'The program will not connect until an administrator runs {#DbSettingsExeName} in the program folder.', mbError, MB_OK, IDOK);
  end;
end;

{ After the files are installed. A new connection: the tool sets up the office CA and the client certificate or password (for a database on
  this computer, interactive, the password). An existing one: the tool applies the security switches given, or checks the settings
  against the connection rule of this version. }
procedure SetUpConnection;
var
  ToolExit: Integer;
  NeedsTool: Boolean;
begin
  if ConfigWanted then begin
    NeedsTool := (not IsLoopbackServer(ConfigServer)) or HasSecuritySwitches;
    if not IsAdminInstallMode then begin
      if NeedsTool then begin
        Log('The connection to ' + ConfigServer + ' needs the connection settings tool, which needs administrator rights.');
        SuppressibleMsgBox('The database server ' + ConfigServer + ' is reached over the network, encrypted. An administrator must run ' +
          '{#DbSettingsExeName} in the program folder to set up the office CA certificate and the client certificate or password.',
          mbInformation, MB_OK, IDOK);
      end;
      Exit;
    end;
    if NeedsTool then begin
      if WizardSilent then
        ToolExit := RunDbSettings(DbSettingsParams + ' /silent', False)
      else
        ToolExit := RunDbSettings(DbSettingsParams, True);
      ReportDbSettingsFailure(ToolExit);
      Exit;
    end;
    { A database on this computer. Release builds have no Choose Database to ask for its password, so an interactive install opens the
      tool in password mode, to store it with DPAPI; Cancel keeps it blank. A silent one gives it with /DBSECRETSFILE= (NeedsTool above). }
    if not WizardSilent then
      ReportDbSettingsFailure(RunDbSettings(DbSettingsParams + ' /auth=password', True));
    Exit;
  end;
  if not IsAdminInstallMode then
    Exit;
  if HasSecurityParams then begin
    if WizardSilent then
      ToolExit := RunDbSettings(UpgradeParams + ' /silent', False)
    else
      ToolExit := RunDbSettings(UpgradeParams, True);
    ReportDbSettingsFailure(ToolExit);
    Exit;
  end;
  ToolExit := RunDbSettings('/check', False);
  if ToolExit = 0 then
    Exit;
  if WizardSilent then begin
    DbSettingsFailed := True;
    if ToolExit = -1 then
      Log('The connection settings could not be checked; run {#DbSettingsExeName} as an administrator.')
    else
      Log('The connection settings do not meet the connection rule of this version; run {#DbSettingsExeName} as an administrator.');
    Exit;
  end;
  if ToolExit = -1 then
    Exit;
  if SuppressibleMsgBox('The connection settings of this computer do not meet the connection rule of this version:' + #13#10#13#10 +
    DbSettingsLogText + #13#10#13#10 + 'Set them up now?', mbConfirmation, MB_YESNO, IDYES) = IDYES then
    RunDbSettings('', True);
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
    Log(WebView2Name + ': missing. Setup does not download it; the administrator installs it from ' + WebView2Url)
  else
    Log(WebView2Name + ': installed');
end;

procedure InitializeWizard;
var
  Mode: String;
begin
  ConfigPage := CreateInputQueryPage(wpSelectTasks, 'Database connection', 'Which database does this computer use?',
    'Setup writes these values to {#ConfigFileName} in the program folder. A server over the network is reached only encrypted: ' +
    'the next page asks for the office CA certificate and the client certificate. For a server on this computer the connection ' +
    'settings tool opens after the install and asks for the database password (Cancel when the user has none).' + #13#10#13#10 +
    'Server: the name the server''s certificate carries, as the computers reach it (host:port for a port other than 3306). ' +
    'User: the program''s own database account from the server set-up (opmsd in the guide), not root.');
  ConfigPage.Add('&Server:', False);
  ConfigPage.Add('&Database:', False);
  ConfigPage.Add('&User:', False);
  ConfigPage.Values[0] := ExpandConstant('{param:DBSERVER|localhost}');
  ConfigPage.Values[1] := ExpandConstant('{param:DBNAME|opendental}');
  ConfigPage.Values[2] := ExpandConstant('{param:DBUSER|opmsd}');

  SecurityPage := CreateInputFilePage(ConfigPage.ID, 'Encrypted connection', 'The office CA and the client certificate',
    'After the files are installed, setup opens the connection settings tool ({#DbSettingsExeName}) with these files. It asks there for ' +
    'the passwords, which never go on a command line. No client certificate: the tool asks for the database password instead.');
  SecurityPage.Add('Office &CA certificate file (from the server set-up):', 'Certificates (*.pem;*.crt;*.cer)|*.pem;*.crt;*.cer|All files|*.*', '.pem');
  SecurityPage.Add('Client &certificate (.pfx), blank to use a database password:', 'Client certificates (*.pfx;*.p12)|*.pfx;*.p12|All files|*.*', '.pfx');
  SecurityPage.Values[0] := ExpandConstant('{param:DBCA|}');
  SecurityPage.Values[1] := ExpandConstant('{param:DBPFX|}');

  PrereqPage := CreateInputOptionPage(SecurityPage.ID, 'Microsoft components', 'A component the program needs is missing.',
    'This computer does not have:' + #13#10 + MissingPrereqText + #13#10 +
    'Setup can download it from Microsoft and install it now (an internet connection is needed), or you can install it later.',
    True, False);
  PrereqPage.Add('Download and install it now from Microsoft');
  PrereqPage.Add('Skip it for now (the program does not work fully until it is installed)');
  WebView2Page := CreateOutputMsgPage(PrereqPage.ID, WebView2Name, 'The program''s browser windows need it.', WebView2MissingText);
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
  else if PageID = SecurityPage.ID then
    { Only for a new connection to a server over the network, and only an administrative install can run the tool. }
    Result := ConfigExistsInFolder or IsLoopbackServer(ConfigServer) or (not IsAdminInstallMode)
  else if PageID = PrereqPage.ID then
    Result := not NeedVcRedist
  else if PageID = WebView2Page.ID then
    Result := not NeedWebView2;
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
        SuppressibleMsgBox('The program does not work fully until this is installed:' + #13#10 + MissingPrereqText + #13#10 +
          'Install it later from Microsoft:' + #13#10 + VcRedistUrl, mbInformation, MB_OK, IDOK);
    end;
  end
  else if CurPageID = wpReady then begin
    if DownloadPrereqs and NeedVcRedist then begin
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
  end
  else if CurStep = ssPostInstall then
    SetUpConnection;
end;

{ Called when setup finished. A silent install whose connection settings step failed (ReportDbSettingsFailure, the /check of an upgrade)
  ends with DbSettingsFailedExitCode, so that a deployment script sees it; the files are installed, and setup's log has the tool's result
  line. Interactive installs showed the failure and end with 0. }
function GetCustomSetupExitCode: Integer;
begin
  Result := 0;
  if DbSettingsFailed then begin
    Log('Setup exit code ' + IntToStr(DbSettingsFailedExitCode) + ': the connection settings were not completed.');
    Result := DbSettingsFailedExitCode;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RemoveProgramFolder;
end;
