# 安装包构建说明

本目录包含 AP-Scaffold 的 Windows 安装包脚本，使用 [Inno Setup 6](https://jrsoftware.org/isinfo.php) 编译。

## 前置要求

- Windows 10/11
- Inno Setup **6.5 及以上**（`Languages/ChineseSimplified.isl` 随仓库携带，官方发行版不含中文；该文件要求 6.5+，换机编译无需再往 Inno 安装目录拷语言文件）
- .NET 8 SDK（仅构建机需要）

## 发布形态：框架依赖（Framework-Dependent）

当前发布方案为 **win-x64 框架依赖**：应用不含运行时，体积小，适合"初次装机时装一次运行时、后续只发应用"的外包交付节奏。

- 发布目录约 **26 MB / 227 文件**（含 13 个插件目录）；安装包（LZMA2）约 **7.7 MB**
- 不要开 `PublishReadyToRun`：R2R 与 WPF 内部混合程序集（DirectWriteForwarder）冲突，启动即 `TypeLoadException`（2026-07-29 实测踩坑）
- **暂勿改自包含发布**：`PluginLoadContext`（可回收插件隔离上下文）会把 WPF 的 C++/CLI 混合程序集 `DirectWriteForwarder` 装入插件上下文，启动即 `TypeLoadException: Collectible type '<Module>' ... FixedAddressValueTypeAttribute`。框架依赖发布因该文件不在应用目录、解析回退默认上下文而天然规避；如需自包含，须先修插件加载上下文（让框架程序集回退 `AssemblyLoadContext.Default`）

## 现场装机要求（仅首次）

框架依赖发布需要现场安装**两个** .NET 8 运行时（8.x 任意小版本；仅装 9/10 不满足，安装包会检测并拦截）：

| 运行时 | 安装包 | 用途 |
|--------|--------|------|
| .NET 8 桌面运行时 | `windowsdesktop-runtime-8.x-win-x64.exe` | WPF 本体 |
| ASP.NET Core 8 运行时 | `aspnetcore-runtime-8.x-win-x64.exe` | gRPC 组件（宿主经 `FrameworkReference` 声明，缺失启动即失败） |

下载页：<https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0>。静默安装命令（可写进取景脚本）：

```powershell
.\windowsdesktop-runtime-8.x-win-x64.exe /install /quiet /norestart
.\aspnetcore-runtime-8.x-win-x64.exe /install /quiet /norestart
```

## 构建步骤

**一键方式（推荐）**：双击运行 `installer/build-installer.bat`（自动完成"清理 bin → 构建 → 发布 → 编译安装包"四步），产物在 `installer/Output/`。

**手动分步方式**：

1. **清理旧产物**（可选但推荐，避免残留插件目录）

   ```powershell
   rm -rf bin   # 或手动删除 bin 文件夹
   ```

2. **构建整个解决方案**（插件输出到 `bin/Release/plugins/`，publish 只负责复制，跳过此步会打包旧插件）

   ```powershell
   dotnet build AP-Automation.Platform.slnx -c Release
   ```

3. **发布应用**（输出到 `bin/Release/publish/`；`-p:AppendRuntimeIdentifierToOutputPath=true` 让 RID 构建落入 `bin/Release/win-x64/`，不污染开发目录）

   ```powershell
   dotnet publish platform/hosts/AP.Host.Desktop/AP.Host.Desktop.csproj -c Release -r win-x64 --self-contained false -p:AppendRuntimeIdentifierToOutputPath=true
   ```

4. **编译安装包**

   用 Inno Setup 打开 `installer/setup.iss`，点击 Build。

   或使用命令行（ISCC 路径按实际安装位置调整）：

   ```powershell
   & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer/setup.iss
   ```

5. **获取安装包**

   生成的安装程序位于 `installer/Output/` 目录（已 gitignore）。

## 安装包特性

- 自动复制宿主程序、插件目录、配置文件到安装目录
- 创建开始菜单和桌面快捷方式（可选）
- 安装前检测 .NET 8 桌面运行时 + ASP.NET Core 运行时（按主版本精确匹配，仅装 .NET 9/10 会被拦截；缺失项会在提示中列出）
- 简体中文安装界面（语言文件随仓库携带）
- 应用运行中禁止安装（AppMutex 检测，避免覆盖正在使用的文件）
- 覆盖安装/升级时保留现场配置文件（`Configuration/appsettings*.json` 仅首次安装写入）
- 支持卸载

## 体积优化可选项（当前未启用，如需再评估）

- 排除 `*.pdb` 调试符号（约 1 MB，收益小，保留便于现场排障）
- 单文件发布（`PublishSingleFile`）：文件数变少但体积不减、启动多一步解压，无收益
- Trimming / NativeAOT / ReadyToRun：WPF + Prism 反射场景不支持或实测冲突，勿启用
