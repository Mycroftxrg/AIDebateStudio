#define MyAppName "AI 辩论工作室"
#define MyAppVersion "1.1"
#define MyAppPublisher "Mycroftxrg"
#define MyAppExeName "AIDebateStudio.exe"
#define SourceDir "bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"

[Setup]
AppId={{F65C1E84-6D7F-4F3B-A0E6-AB9037AF5EE2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\AIDebateStudio
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=artifacts\windows
OutputBaseFilename=AIDebateStudio-1.1-windows-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent
