#region

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using AP.Contracts.Core.Events;
using AP.Core.Enums;
using AP.Core.Lifecycle;
using AP.Core.PluginFramework.Abstractions;
using AP.Core.PluginFramework.Loading;
using AP.Host.Desktop.Views;
using AP.Infra.Database.Configuration;
using AP.Infra.Grpc.Client;
using AP.Infra.Grpc.Server;
using AP.Infra.Logging.Configuration;
using AP.Infra.Logging.Helpers;
using AP.Infra.Resilience.Configuration;
using AP.Shared.UI.Services;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

#endregion

namespace AP.Host.Desktop.Bootstrapping;

/// <summary>
///     插件启动器
/// </summary>
public class Bootstrapper : PrismBootstrapper
{
    private readonly AppRole _appRole;
    private IConfigurationRoot _configuration = null!;
    private readonly PluginLoader _pluginLoader;
    private List<PluginDescriptor> _loadedPlugins = new();
    private readonly List<(string PluginName, string Error)> _failedPlugins = new();
    private WebApplication? _grpcApp;
    private PluginLifecycleManager? _lifecycleManager;

    public Bootstrapper(AppRole appRole)
    {
        _appRole = appRole;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
        _pluginLoader = new PluginLoader(loggerFactory.CreateLogger<PluginLoader>());
    }

    protected override DependencyObject CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void InitializeShell(DependencyObject shell)
    {
        Application.Current.MainWindow = (Window)shell;
        Application.Current.MainWindow.Show();
    }

    /// <summary>
    ///     注册所有服务 (DI 容器配置)
    /// </summary>
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "Configuration");

        if (!Directory.Exists(configPath))
            Directory.CreateDirectory(configPath);

        // 1. 加载配置
        var builder = new ConfigurationBuilder()
            .SetBasePath(configPath)
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile($"appsettings.{_appRole}.json", true, true)
            .AddEnvironmentVariables();

        _configuration = builder.Build();

        // 注册配置到 Prism
        containerRegistry.RegisterInstance<IConfiguration>(_configuration);
        containerRegistry.RegisterInstance(_appRole);

        // --- 准备 Microsoft.Extensions.DependencyInjection 服务集合 ---
        var services = new ServiceCollection();

        // --- 添加平台基础设施 ---
        services.AddPlatformLogging(_configuration);
        services.AddPlatformDatabase(_configuration, _appRole);
        services.AddPlatformResilience(_configuration);

        // --- 添加 gRPC 服务 (根据角色) ---
        if (_appRole.HasFlag(AppRole.Server))
        {
            services.AddPlatformGrpcServer(_configuration);
            ServerBootstrap.RegisterServices(services);
        }

        if (_appRole.HasFlag(AppRole.Client))
            ClientBootstrap.RegisterServices(services);


        // --- 扫描并实例化插件 ---
        _loadedPlugins = _pluginLoader.DiscoverPlugins(_appRole);
        Log.Information("已发现 {Count} 个适用插件", _loadedPlugins.Count);

        // 创建一个临时的 logger 工厂用于传递给插件构造函数
        using var pluginLoggerFactory = LoggerFactory.Create(b => b.AddSerilog());

        var pluginAssemblies = new List<Assembly> { typeof(Bootstrapper).Assembly }; // 包含 Host 自身

        // 构建临时 ServiceProvider 用于插件实例化（支持 DI 解析构造函数参数）
        var tempProvider = services.BuildServiceProvider();

        foreach (var descriptor in _loadedPlugins)
            try
            {
                var pluginLogger = pluginLoggerFactory.CreateLogger(descriptor.PluginType);

                // 使用 ActivatorUtilities 从 DI 容器解析构造函数参数
                // 支持 ILogger 和其他已注册的服务（如 IOptions<T>）
                var instance = ActivatorUtilities
                    .CreateInstance(tempProvider, descriptor.PluginType, pluginLogger) as IPlugin;

                if (instance == null) continue;

                descriptor.Instance = instance;
                descriptor.IsLoaded = true;

                // 注册插件自身的服务
                if (instance is IConfigurablePlugin configurable)
                    configurable.ConfigureServices(services, _configuration);

                // 将插件单例注册到容器 (方便通过 IEnumerable<IPlugin> 获取)
                services.AddSingleton(typeof(IPlugin), instance);

                // 收集程序集用于 MediatR 扫描
                pluginAssemblies.Add(descriptor.PluginType.Assembly);
            }
            catch (Exception ex)
            {
                // 捕获异常，防止一个插件崩溃导致整个程序启动失败
                // 使用 Log.Error 确保 Release 构建下也能记录
                Log.Error(ex, "插件 {Name} 加载失败", descriptor.Metadata.Name);
                _failedPlugins.Add((descriptor.Metadata.Name, ex.Message));
                descriptor.IsLoaded = false;
            }

        // --- 注册 MediatR ---
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(pluginAssemblies.ToArray()));
        // --- 注册生命周期管理器 ---
        _lifecycleManager = new PluginLifecycleManager(
            LoggerFactory.Create(b => b.AddSerilog()).CreateLogger<PluginLifecycleManager>());
        _lifecycleManager.RegisterPlugins(_loadedPlugins.Where(p => p.IsLoaded));
        services.AddSingleton(_lifecycleManager);
        // --- 桥接容器 (Microsoft DI -> DryIoc) ---
        var dryIocContainer = containerRegistry.GetContainer();
        dryIocContainer.Populate(services);

        containerRegistry.RegisterSingleton<ICustomDialogService, MaterialDialogService>();
        // 让插件也能解析 Prism 的 IContainerProvider
        containerRegistry.RegisterInstance<IContainerProvider>(containerRegistry as IContainerProvider);
    }

    /// <summary>
    ///     初始化完成后 (启动插件)
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // 异步启动，避免阻塞 UI 线程
        Task.Run(async () =>
        {
            try
            {
                // 获取容器的新 Scope (也就是当前的根容器)
                var container = Container.GetContainer();

                // --- 0. 启动时清理过期日志（零开销，仅执行一次） ---
                var logRetainDays = _configuration.GetValue<int>("Logging:RetainedFileCount", 90);
                LogCleanupHelper.CleanupIfNeeded("logs", logRetainDays);

                // --- 1. 初始化并启动插件（通过生命周期管理器，带状态机跟踪） ---
                await _lifecycleManager!.InitializePluginsAsync(container);
                await _lifecycleManager.StartPluginsAsync();

                // --- 3. 启动 gRPC Server (如果是服务端) ---
                if (_appRole.HasFlag(AppRole.Server)) StartKestrelServer(container);

                // --- 4. 启动 gRPC Client Worker (如果是客户端) ---
                if (_appRole.HasFlag(AppRole.Client))
                {
                    var clientWorker = container.GetService<GrpcClientWorker>();
                    if (clientWorker != null)
                    {
                        await clientWorker.StartAsync(CancellationToken.None);
                        Log.Information("gRPC 客户端后台服务已启动");
                    }
                }

                // 汇总展示加载失败的插件（工业现场关键：操作员需知道功能缺失）
                if (_failedPlugins.Count > 0)
                {
                    var failedMsg = string.Join("\n", _failedPlugins.Select(f => $"  • {f.PluginName}: {f.Error}"));
                    Log.Warning("以下 {Count} 个插件加载失败:\n{FailedPlugins}", _failedPlugins.Count, failedMsg);

                    // 在 UI 线程弹出警告
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"以下 {_failedPlugins.Count} 个插件加载失败，相关功能可能不可用：\n\n{failedMsg}\n\n请检查日志获取详细信息。",
                            "插件加载警告",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    });
                }

                var eventAggregator = container.Resolve<IEventAggregator>();
                eventAggregator.GetEvent<AppInitializedEvent>().Publish();

                Log.Information(">>> 系统启动完成 <<<");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "系统启动过程中发生未捕获异常");
            }
        });
    }

    private void StartKestrelServer(IServiceProvider provider)
    {
        try
        {
            var port = _configuration.GetValue<int>("Grpc:ServerPort", 5000);
            var builder = WebApplication.CreateBuilder();

            // 配置 Kestrel
            builder.ConfigureKestrelForGrpc(port);

            // 使用 Serilog
            builder.Host.UseSerilog();

            // 注册 gRPC
            builder.Services.AddGrpc();

            // 桥接单例服务：从 WPF 容器获取 Broadcaster，共享给 ASP.NET Core
            var broadcaster = provider.GetRequiredService<StreamBroadcaster>();
            builder.Services.AddSingleton(broadcaster);

            var app = builder.Build();
            app.MapGrpcService<GrpcGateService>();

            _grpcApp = app; // 保存引用以便优雅关闭
            app.RunAsync();
            Log.Information("gRPC Server 正在监听端口: {Port}", port);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Kestrel 服务器启动失败");
        }
    }

    /// <summary>
    /// 优雅停止所有插件和服务
    /// 应在应用退出时调用
    /// </summary>
    public async Task ShutdownAsync()
    {
        Log.Information("=== 开始优雅关闭 ===");

        var container = Container.GetContainer();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // 10秒超时

        try
        {
            // 1. 通过生命周期管理器停止所有插件（按优先级反序，带状态机跟踪）
            if (_lifecycleManager != null)
            {
                await _lifecycleManager.StopPluginsAsync(cts.Token);
            }

            // 2. 停止 gRPC Server
            if (_grpcApp != null)
            {
                Log.Information("正在停止 gRPC Server...");
                await _grpcApp.StopAsync(cts.Token);
                Log.Information("gRPC Server 已停止");
            }

            // 3. 停止 gRPC Client Worker
            if (_appRole.HasFlag(AppRole.Client))
            {
                var clientWorker = container.GetService<GrpcClientWorker>();
                if (clientWorker != null)
                {
                    Log.Information("正在停止 gRPC Client Worker...");
                    await clientWorker.StopAsync(cts.Token);
                    Log.Information("gRPC Client Worker 已停止");
                }
            }

            Log.Information("=== 优雅关闭完成 ===");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "优雅关闭过程中发生异常");
        }
        finally
        {
            cts.Dispose();
        }
    }
}
