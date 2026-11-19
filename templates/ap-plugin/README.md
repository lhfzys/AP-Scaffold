# AP-Scaffold 业务插件模板

从示例业务插件生成一个新插件（导航页 + 设置页 + 报表提供者 + ViewModel 示例）。

## 安装模板

在仓库根目录执行：

```powershell
dotnet new install .\templates\ap-plugin
```

## 生成插件

建议生成到 `platform/plugins/business/`，这样相对项目引用（`..\..\..\shared`）才正确：

```powershell
dotnet new ap-plugin -n AP.Plugin.MyBusiness -o platform/plugins/business/AP.Plugin.MyBusiness
```

## 加入解决方案

把新项目加入 `AP-Automation.Platform.slnx` 的 `/platform/plugins/business/` 节点，然后构建：

```powershell
dotnet build AP-Automation.Platform.slnx -c Release
```

插件会被 `Directory.Build.props` 自动输出到 `bin/Release/plugins/AP.Plugin.MyBusiness/`，宿主启动时自动发现。

## 生成后要改的地方

1. `Plugin.cs`：`[PluginMetadata]` 的 `Name`、菜单 `Label`/`IconKind`/`Order`；
2. `Views/PluginView.xaml`：替换为真实业务页面；
3. `ViewModels/PluginViewModel.cs`：替换示例计数逻辑；
4. `Configuration/PluginOptions.cs`：改成项目自己的配置项；
5. 不需要报表时删除 `Reporting/PluginReportProvider.cs` 及 `Plugin.cs` 中的注册。

## 测试

模板同时生成 `Tests/AP.Plugin.Sample.Tests.csproj`（运行时文件名会替换为你的项目名），包含 ViewModel、设置回环、报表提供者测试。把测试项目一起加入解决方案，或单独运行：

```powershell
dotnet test platform/plugins/business/AP.Plugin.MyBusiness/Tests/AP.Plugin.MyBusiness.Tests.csproj
```

测试项目引用同目录插件项目；如移动目录，请同步修改 `..\AP.Plugin.Sample.csproj`。
