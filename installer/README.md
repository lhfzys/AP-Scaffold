# 安装包构建说明

本目录包含 AP-Scaffold 的 Windows 安装包脚本，使用 [Inno Setup 6](https://jrsoftware.org/isinfo.php) 编译。

## 前置要求

- Windows 10/11
- Inno Setup 6 及以上版本
- .NET 8 SDK

## 构建步骤

1. **发布应用**

   ```powershell
   dotnet publish platform/hosts/AP.Host.Desktop/AP.Host.Desktop.csproj -c Release
   ```

2. **编译安装包**

   用 Inno Setup 打开 `installer/setup.iss`，点击 Build。

   或使用命令行：

   ```powershell
   & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer/setup.iss
   ```

3. **获取安装包**

   生成的安装程序位于 `installer/Output/` 目录。

## 安装包特性

- 自动复制宿主程序、插件目录、配置文件到安装目录
- 创建开始菜单和桌面快捷方式（可选）
- 安装前检测 .NET 8 Windows 桌面运行时（按主版本精确匹配，仅装 .NET 9/10 会被拦截）
- 应用运行中禁止安装（AppMutex 检测，避免覆盖正在使用的文件）
- 覆盖安装/升级时保留现场配置文件（`Configuration/appsettings*.json` 仅首次安装写入）
- 支持卸载
