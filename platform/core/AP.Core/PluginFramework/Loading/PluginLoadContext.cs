using System.Reflection;
using System.Runtime.Loader;

namespace AP.Core.PluginFramework.Loading;

/// <summary>
/// 插件隔离加载上下文 (解决 DLL 版本冲突的关键)
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    private static readonly string[] SharedPrefixes = new[]
    {
        // 我们的共享项目
        "AP.Core",
        "AP.Shared",
        "AP.Contracts",

        // 核心框架
        "Prism",
        "DryIoc",
        "MediatR",
        "CommunityToolkit.Mvvm",
        "Microsoft.Extensions",

        // 基础类库
        "System.Diagnostics",
        "System.Runtime",
        "System.ComponentModel",

        // 第三方库
        "MaterialDesign",
        "MahApps.Metro",
        "Newtonsoft.Json",
        "Serilog",
        "FreeSql"
    };

    private static readonly HashSet<string> SharedAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "AP.Core",
        "AP.Shared.PluginSDK",
        "AP.Shared.UI",
        "AP.Shared.Utilities",
        "AP.Contracts.Core",
        "AP.Contracts.Hardware",
        "AP.Contracts.System",

        // Prism 核心系列
        "Prism",
        "Prism.Wpf",
        "Prism.Core",
        "Prism.DryIoc",
        "Prism.Container.Abstractions", // <--- 报错的元凶
        "DryIoc",

        // UI 库系列
        "MaterialDesignThemes.Wpf",
        "MaterialDesignColors",
        "CommunityToolkit.Mvvm",

        //  微软基础扩展库 (必须强制共享，否则 ILogger 类型不匹配)
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Logging",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Configuration.Abstractions",
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.Hosting.Abstractions",
        "System.Diagnostics.DiagnosticSource",

        // 其他基础库
        "Serilog",
        "MediatR",
        "Polly",
        "FreeSql"
    };

    public PluginLoadContext(string pluginPath) : base(true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name ?? "";
        if (SharedPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return null;

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null) return LoadFromAssemblyPath(assemblyPath);

        var rootPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName.Name}.dll");
        if (File.Exists(rootPath))
            return LoadFromAssemblyPath(rootPath);

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
    }
}