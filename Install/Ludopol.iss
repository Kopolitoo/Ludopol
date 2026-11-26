; Installer of Ludopol (Ludopol-<version>-setup.exe), made with Inno Setup 6
; (https://jrsoftware.org/isinfo.php), like the one of Prunel.
;
; Installs Ludopol for the current user, in %LocalAppData%\Programs\Ludopol: no administrator
; rights, so no Windows "Do you want to allow…" prompt (Ludopol needs no UI access, unlike
; Prunel). Adds Ludopol to the Start menu, optionally to the desktop (ticked by default), and to
; "Settings > Apps" of Windows (to uninstall it).
; The program is "self-contained": .NET 8 is inside, nothing else to install.
; The catalogue, the settings and the log (%LocalAppData%\Ludopol) are never touched, not even
; by the uninstallation.
;
; Compiled by .github/scripts/make-setup.ps1:
;   ISCC.exe /DAppVersion=0.1.0 /DProgramFolder=<published Ludopol> /DOutputFolder=<folder> Ludopol.iss
;
; Saved in UTF-8 WITH a BOM: Inno Setup then reads the accented French letters correctly.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
; The folder made by "dotnet publish" (see make-setup.ps1).
#ifndef ProgramFolder
  #define ProgramFolder "..\bin\publish"
#endif
#ifndef OutputFolder
  #define OutputFolder "..\bin\setup"
#endif

[Setup]
; Identifies Ludopol in Windows: never change it, or an update would be a second program.
AppId={{6547A2E3-8608-42D9-95EB-74F5AE976446}
AppName=Ludopol
AppVersion={#AppVersion}
AppVerName=Ludopol {#AppVersion}
AppPublisher=Ludopol
AppPublisherURL=https://github.com/Kopolitoo/Ludopol
AppSupportURL=https://github.com/Kopolitoo/Ludopol/issues
VersionInfoVersion={#AppVersion}
; Current user only: {autopf} is then %LocalAppData%\Programs.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DefaultDirName={autopf}\Ludopol
; As few pages as possible (each click is an effort by gaze): no folder and no Start menu
; folder to choose; the language is the one of Windows (French or English).
DisableDirPage=yes
DisableProgramGroupPage=yes
; The only page before the copy is the "Desktop shortcut" box: its button becomes "Install",
; so the box costs no extra click.
DisableReadyPage=yes
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage
; Ludopol is closed by the [Code] below, without asking.
CloseApplications=no
WizardStyle=modern
SetupIconFile=..\Resources\Ludopol.ico
UninstallDisplayIcon={app}\Ludopol.exe
UninstallDisplayName=Ludopol
OutputDir={#OutputFolder}
OutputBaseFilename=Ludopol-{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
; Desktop shortcut, ticked by default (the box keeps the user's last choice on an update).
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; Data folder of the program: emptied before the copy, so that no old language file stays.
Type: filesandordirs; Name: "{app}\Languages"

[Files]
; The program and its files (never the debug files).
Source: "{#ProgramFolder}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Ludopol"; Filename: "{app}\Ludopol.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Ludopol"; Filename: "{app}\Ludopol.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; Last page: "Launch Ludopol" (ticked).
Filename: "{app}\Ludopol.exe"; Description: "{cm:LaunchProgram,Ludopol}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Everything left in the program folder. The data folder (%LocalAppData%\Ludopol) is another
; folder and stays.
Type: filesandordirs; Name: "{app}"

[Code]
// Closes a running Ludopol: the files of a running program cannot be replaced.
procedure CloseLudopol;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM Ludopol.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  // taskkill returns 0 when Ludopol was running and is now closed.
  if ResultCode = 0 then
    Sleep(2000);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  CloseLudopol;
  Result := '';
end;

function InitializeUninstall: Boolean;
begin
  CloseLudopol;
  Result := True;
end;
