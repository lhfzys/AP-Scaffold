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

模板不自动生成测试项目；建议在 `platform/tests/AP.Infra.Tests` 或新建测试项目中引用插件，至少覆盖：

- ViewModel 配置加载与核心命令；
- 设置编辑器 `LoadFromConfiguration` / `Validate` / `GetConfigurationValue` 回环；
- 报表提供者（如保留）返回数据形状。

参考仓库内 `platform/tests/AP.Infra.Tests/Business/SamplePluginTests.cs`。
