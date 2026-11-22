#ifdef LiteEdition
  #define MyAppName "ClickForge Lite"
  #define MyAppExeName "ClickForge.Lite.exe"
  #define MyOutputName "ClickForge-Lite-Setup-x64"
#else
  #define MyAppName "ClickForge"
  #define MyAppExeName "ClickForge.exe"
  #define MyOutputName "ClickForge-Setup-x64"
#endif
#ifndef MyAppVersion
  #define MyAppVersion "0.2.19"
#endif
#define MyAppPublisher "ClickForge"
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif

[Setup]
#ifdef LiteEdition
AppId={{34E91658-977B-4627-905F-DF2296A9572B}
#else
AppId={{A9768362-6254-4A32-B71B-23B290A9957A}
#endif
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\artifacts\installer
OutputBaseFilename={#MyOutputName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\ClickForge.App\ClickForge.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные значки:"

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Запустить {#MyAppName}"; Flags: nowait postinstall skipifsilent
