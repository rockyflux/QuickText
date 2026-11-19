; Inno Setup 6 script for the optional wizard installer.
; Keeps the portable zip/exe release assets unchanged: this only wraps the same
; self-contained QuickText.exe already published by the win-x64 profile.
;
; Defaults to a per-user install under %LOCALAPPDATA%\Programs\QuickText (no admin).
; The directory page stays enabled so the user can change the path; choosing
; "Install for all users" elevates and switches the default root to Program Files.
;
; Compile (from repo root, after publishing win-x64):
;   ISCC /DMyAppVersion=1.2.3 /DMyAppSource=src\QuickText.App\bin\Release\publish packaging\innosetup\QuickText.iss
;
; Do NOT drop a QuickText.portable marker — installed mode uses %APPDATA%\QuickText.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef MyAppSource
  #define MyAppSource "..\..\src\QuickText.App\bin\Release\publish"
#endif
#ifndef MyAppOutputDir
  #define MyAppOutputDir "..\..\dist"
#endif
#ifndef MyAppOutputBase
  #define MyAppOutputBase "QuickText-v" + MyAppVersion + "-win-x64-setup"
#endif

#define MyAppName "QuickText"
#define MyAppPublisher "Rocky"
#define MyAppURL "https://github.com/rockyflux/QuickText"
#define MyAppExeName "QuickText.exe"

[Setup]
; Fixed AppId so upgrades replace the previous install instead of side-by-side copies.
AppId={{B6E7A1C2-4D8F-4E3A-9B1C-7F2E5A0D8C91}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
; {autopf} → %LOCALAPPDATA%\Programs (per-user) or Program Files (all-users).
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Keep the directory page so users can change the install path.
DisableDirPage=no
AllowNoIcons=yes
LicenseFile=..\..\LICENSE
OutputDir={#MyAppOutputDir}
OutputBaseFilename={#MyAppOutputBase}
SetupIconFile=..\..\assets\branding\quicktext.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Per-user by default; dialog offers an all-users (admin) alternative.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
RestartApplications=no
; Uninstall removes the program folder only — leave %APPDATA%\QuickText user data alone.
[Languages]
; Vendored: official Inno tree ships Chinese under Files/Languages, but the Windows
; installer omits it from Languages\. Keep a copy next to this script so CI/local
; builds don't depend on which ISL pack was installed.
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
; Checked by default — tray app is meant to be ready after login; user can still uncheck.
Name: "autostart"; Description: "Login start / 开机启动"; GroupDescription: "Startup / 启动"

[Files]
Source: "{#MyAppSource}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Match Autostart.cs installed-mode path (HKCU Run + --autostart), so Settings / footer
; toggles stay in sync with what the installer wrote. Uninstall removes the value.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "QuickText"; \
    ValueData: """{app}\{#MyAppExeName}"" --autostart"; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
