# 安装包构建说明

本目录包含 AP-Scaffold 的 Windows 安装包脚本，使用 [Inno Setup 6](https://jrsoftware.org/isinfo.php) 编译。

## 前置要求

- Windows 10/11
- Inno Setup **6.5 及以上**（`Languages/ChineseSimplified.isl` 随仓库携带，官方发行版不含中文；该文件要求 6.5+，换机编译无需再往 Inno 安装目录拷语言文件）
- .NET 10 SDK（仅构建机需要）

## 发布形态：框架依赖（Framework-Dependent）

当前发布方案为 **win-x64 框架依赖**：应用不含运行时，体积小，适合"初次装机时装一次运行时、后续只发应用"的外包交付节奏。

- 发布目录约 **46 MB / 243 文件**（含 14 个插件目录）；安装包（LZMA2）约 **13.4 MB**
- 不要开 `PublishReadyToRun`：R2R 与 WPF 内部混合程序集（DirectWriteForwarder）冲突，启动即 `TypeLoadException`（2026-07-29 实测踩坑）
- **暂勿改自包含发布**：`PluginLoadContext`（可回收插件隔离上下文）会把 WPF 的 C++/CLI 混合程序集 `DirectWriteForwarder` 装入插件上下文，启动即 `TypeLoadException: Collectible type '<Module>' ... FixedAddressValueTypeAttribute`。框架依赖发布因该文件不在应用目录、解析回退默认上下文而天然规避；如需自包含，须先修插件加载上下文（让框架程序集回退 `AssemblyLoadContext.Default`）

## 运行数据目录（2026-11-18 起）

程序文件与运行数据分离，避免标准用户无法写 `Program Files`：

| 内容 | 位置 |
|------|------|
| 程序/默认配置 | `C:\Program Files\自动化监控系统\`（只读） |
| 数据库/日志/报表/现场点表/配置覆盖层 | `%ProgramData%\AP-Scaffold\`（安装器授予 Users Modify） |

- 配置采用“安装目录默认值 + 数据根覆盖层”，系统设置保存只写覆盖层，升级不会覆盖现场修改；
- `tags.json` 首次启动从安装目录复制到 `%ProgramData%\AP-Scaffold\Configuration\tags.json`，后续升级保留现场副本；
- 旧版 `local_data.db` 在首次启动时复制到数据根（目标不存在时）；
- 可用环境变量 `AP_SCAFFOLD_DATA_ROOT` 指定其他可写数据根（POC/便携部署）；
- 卸载默认保留数据根，避免误删现场数据。

## 现场装机要求（仅首次）

框架依赖发布只需要安装 **.NET 10 桌面运行时**（10.x 任意小版本；仅装 8/9 不满足，安装包会检测并拦截）：

| 运行时 | 安装包 | 用途 |
|--------|--------|------|
| .NET 10 桌面运行时 | `windowsdesktop-runtime-10.x-win-x64.exe` | WPF 本体 |

> gRPC/ASP.NET Core 已物理移出交付链（2026-11-18，DH-6），不再要求 ASP.NET Core 运行时。

下载页：<https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0>。静默安装命令（可写进取景脚本）：

```powershell
.\windowsdesktop-runtime-10.x-win-x64.exe /install /quiet /norestart
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

- **x64 安装**：Setup 使用 `ArchitecturesInstallIn64BitMode=x64compatible`，装到 `C:\Program Files\自动化监控系统`，不要装进 `Program Files (x86)`；
- 自动复制宿主程序、插件目录、配置文件到安装目录
- 覆盖升级时清理旧版残留（`[InstallDelete]`：gRPC/Protobuf 文件与旧插件目录），避免已移除组件继续留在 Program Files
- 创建开始菜单和桌面快捷方式（可选）
- 安装前检测 .NET 10 桌面运行时（按主版本精确匹配，仅装 .NET 8/9 会被拦截；缺失项会在提示中列出）
- 简体中文安装界面（语言文件随仓库携带）
- 应用运行中禁止安装（AppMutex 检测，避免覆盖正在使用的文件）
- 覆盖安装/升级时保留现场配置文件（`Configuration/appsettings*.json` 仅首次安装写入）
- 支持卸载

## 体积优化可选项（当前未启用，如需再评估）

- 排除 `*.pdb` 调试符号（约 1 MB，收益小，保留便于现场排障）
- 单文件发布（`PublishSingleFile`）：文件数变少但体积不减、启动多一步解压，无收益
- Trimming / NativeAOT / ReadyToRun：WPF + Prism 反射场景不支持或实测冲突，勿启用
