#ifndef SourceDir
#define SourceDir "..\publish-installer"
#endif

#ifndef OutputDir
#define OutputDir "output"
#endif

#ifndef SourceZip
#define SourceZip "output\Cinnamon-Dynamic-Wallpaper-Source-1.0.0.zip"
#endif

#define MyAppName "Cinnamon Dynamic Wallpaper"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Abhishek Hembrom"
#define MyAppExeName "CinnamonDynamicWallpaper.Windows.exe"

[Setup]
AppId={{5B7782B1-36C7-45EF-8C09-2E753C3A8C19}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/TobiZog/cinnamon-dynamic-wallpaper
AppSupportURL=https://github.com/TobiZog/cinnamon-dynamic-wallpaper/issues
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\..\LICENSE
InfoAfterFile=..\..\THIRD_PARTY_NOTICES.txt
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir={#OutputDir}
OutputBaseFilename=Cinnamon-Dynamic-Wallpaper-Setup-{#MyAppVersion}-x64
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceZip}"; DestDir: "{app}\Source Code"; Flags: ignoreversion
Source: "{#SourceDir}\Stop-App.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "CinnamonDynamicWallpaper"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --background"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; WorkingDir: "{app}"; Flags: postinstall nowait skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
	ResultCode: Integer;
begin
	if CurUninstallStep = usUninstall then
	begin
		Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
			'-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\Stop-App.ps1') + '" -InstallPath "' + ExpandConstant('{app}') + '"',
			'', SW_HIDE, ewWaitUntilTerminated, ResultCode);
		RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'CinnamonDynamicWallpaper');
	end;
end;