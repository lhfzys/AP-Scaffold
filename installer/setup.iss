; AP-Scaffold 工业自动化平台安装脚本 (Inno Setup)
; 使用方法：
;   1. 先执行 dotnet publish -c Release
;   2. 用 Inno Setup 6 打开本文件并编译
;   3. 安装程序会生成在 installer/Output 目录

#define MyAppName "自动化监控系统"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Industrial Automation"
#define MyAppExeName "AP.Host.Desktop.exe"
#define SourceDir "..\bin\Release\publish"

[Setup]
AppId={{AP-SCAFFOLD-PLATFORM-2026}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=Output
OutputBaseFilename={#MyAppName}-{#MyAppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
SetupIconFile=
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 发布目录下的所有文件
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
  NetRuntimeInstalled: Boolean;
begin
  // 简单检测 .NET 8 桌面运行时是否已安装（通过注册表）
  NetRuntimeInstalled := RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App')
                      or RegKeyExists(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App');

  if not NetRuntimeInstalled then
  begin
    if MsgBox('未检测到 .NET 8 Windows 桌面运行时，是否前往下载？', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    Result := false;
    Exit;
  end;

  Result := true;
end;
