; AP-Scaffold 工业自动化平台安装脚本 (Inno Setup)
; 使用方法：
;   1. 先构建整个解决方案：dotnet build AP-Automation.Platform.slnx -c Release
;      （publish 的插件目录来自 build 输出，跳过此步会打包旧插件）
;   2. 再执行框架依赖发布：
;      dotnet publish platform/hosts/AP.Host.Desktop/AP.Host.Desktop.csproj -c Release -r win-x64 --self-contained false -p:AppendRuntimeIdentifierToOutputPath=true
;   3. 用 Inno Setup 6 打开本文件并编译
;   4. 安装程序会生成在 installer/Output 目录
;
; 发布形态：框架依赖（现场需装 .NET 8 桌面运行时 + ASP.NET Core 8 运行时，安装时检测）。
; 注意：暂勿改自包含发布——PluginLoadContext 会把 WPF 混合程序集 DirectWriteForwarder
; 装入可回收插件上下文导致启动崩溃（2026-07-29 实测，详见 installer/README.md）。

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
SetupIconFile=..\platform\hosts\AP.Host.Desktop\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
; 应用运行中禁止安装（与 App.xaml.cs 中持有的命名互斥体对应）
AppMutex=AP.SCAFFOLD.PLATFORM.RUNNING

[Languages]
; 简体中文语言文件随仓库携带（官方发行版不含中文），换机编译无需再装 .isl
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"

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
// 判断指定注册表视图下是否存在 .NET 8.x 版本记录。
// 注意：版本是以"值"（值名=版本号，REG_DWORD）形式挂在 sharedfx\{框架名} 键下的，
// 不是子键——必须用 RegGetValueNames，用 RegGetSubkeyNames 会恒判缺失（2026-07-29 实测踩坑）
function HasNet8Version(RootKey: Integer; SubKey: String): Boolean;
var
  ValueNames: TArrayOfString;
  I: Integer;
begin
  Result := false;
  if RegGetValueNames(RootKey, SubKey, ValueNames) then
  begin
    for I := 0 to GetArrayLength(ValueNames) - 1 do
      if Copy(ValueNames[I], 1, 2) = '8.' then
      begin
        Result := true;
        Exit;
      end;
  end;
end;

// 检测指定 .NET 8 共享框架是否安装（按主版本精确匹配，仅装 9/10 不算满足）。
// 覆盖两个注册表视图：HKLM 在安装程序（32 位进程）中自动落 32 位视图，
// HKLM64 兜底 64 位视图——两种注册位置在真实机器上都存在
function IsNet8SharedFxInstalled(SharedFxName: String): Boolean;
var
  SubKey: String;
begin
  SubKey := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\' + SharedFxName;
  Result := HasNet8Version(HKLM, SubKey) or
            HasNet8Version(HKLM, 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\' + SharedFxName);
  if (not Result) and IsWin64 then
    Result := HasNet8Version(HKLM64, SubKey);
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
  Missing: String;
begin
  // 框架依赖发布需要两个运行时：
  //   桌面运行时（WPF 本体）+ ASP.NET Core 运行时（gRPC 组件经 FrameworkReference 声明，缺失启动即失败）
  Missing := '';
  if not IsNet8SharedFxInstalled('Microsoft.WindowsDesktop.App') then
    Missing := Missing + '  - .NET 8 桌面运行时（windowsdesktop-runtime-8.x-win-x64.exe）' + #13#10;
  if not IsNet8SharedFxInstalled('Microsoft.AspNetCore.App') then
    Missing := Missing + '  - ASP.NET Core 8 运行时（aspnetcore-runtime-8.x-win-x64.exe）' + #13#10;

  if Missing <> '' then
  begin
    if MsgBox('未检测到以下 .NET 8 运行时（仅安装其他主版本不满足要求）：' + #13#10#13#10 + Missing + #13#10 + '是否前往下载页？', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    Result := false;
    Exit;
  end;

  Result := true;
end;
