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
; 应用运行中禁止安装（与 App.xaml.cs 中持有的命名互斥体对应）
AppMutex=AP.SCAFFOLD.PLATFORM.RUNNING

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 发布目录下的所有文件（现场配置文件除外，单独处理）
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "Configuration\appsettings*.json"; Flags: ignoreversion recursesubdirs createallsubdirs
; 现场配置文件：仅首次安装时写入，覆盖安装/升级时保留用户已修改的配置
Source: "{#SourceDir}\Configuration\appsettings*.json"; DestDir: "{app}\Configuration"; Flags: onlyifdoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// 检测 .NET 8 Windows 桌面运行时（按主版本精确匹配：
// 注册表 sharedfx\Microsoft.WindowsDesktop.App 下的子键为已装版本号，仅装 9/10 不算满足）
function IsNet8DesktopRuntimeInstalled(): Boolean;
var
  SubkeyNames: TArrayOfString;
  I: Integer;
begin
  Result := false;
  if RegGetSubkeyNames(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', SubkeyNames) or
     RegGetSubkeyNames(HKLM, 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', SubkeyNames) then
  begin
    for I := 0 to GetArrayLength(SubkeyNames) - 1 do
    begin
      if Copy(SubkeyNames[I], 1, 2) = '8.' then
      begin
        Result := true;
        Exit;
      end;
    end;
  end;
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  if not IsNet8DesktopRuntimeInstalled() then
  begin
    if MsgBox('未检测到 .NET 8 Windows 桌面运行时（仅安装其他主版本不满足要求），是否前往下载？', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    Result := false;
    Exit;
  end;

  Result := true;
end;
