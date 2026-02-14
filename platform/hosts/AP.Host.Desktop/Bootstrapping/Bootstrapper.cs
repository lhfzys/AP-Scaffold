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
using AP.Infra.Resilience.Configuration;
using AP.Shared.UI.Services;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO;
using System.Reflection;
using System.Windows;

namespace AP.Host.Desktop.Bootstrapping;

/// <summary>
/// 插件启动器
/// </summary>
public class Bootstrapper : PrismBootstrapper
{
    private readonly AppRole _appRole;
    private IConfigurationRoot _configuration = null!;
    private readonly PluginLoader _pluginLoader;
    private List<PluginDescriptor> _loadedPlugins = new();

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
    /// 注册所有服务 (DI 容器配置)
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

        foreach (var descriptor in _loadedPlugins)
            try
            {
                var pluginLogger = pluginLoggerFactory.CreateLogger(descriptor.PluginType);

                var instance = Activator.CreateInstance(descriptor.PluginType, pluginLogger) as IPlugin;

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
                System.Diagnostics.Debug.WriteLine($"插件 {descriptor.PluginType.Name} 加载失败: {ex.Message}");
            }

        // --- 注册 MediatR ---
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(pluginAssemblies.ToArray()));
        // --- 注册生命周期管理器 ---
        services.AddSingleton<PluginLifecycleManager>();
        // --- 桥接容器 (Microsoft DI -> DryIoc) ---
        var dryIocContainer = containerRegistry.GetContainer();
        dryIocContainer.Populate(services);

        containerRegistry.RegisterSingleton<ICustomDialogService, MaterialDialogService>();
        // 让插件也能解析 Prism 的 IContainerProvider
        containerRegistry.RegisterInstance<IContainerProvider>(containerRegistry as IContainerProvider);
    }

    /// <summary>
    /// 初始化完成后 (启动插件)
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
                // --- 1. 初始化插件 ---
                Log.Information("=== 开始初始化插件 ===");
                foreach (var desc in _loadedPlugins.Where(p => p.IsLoaded && p.Instance != null))
                    try
                    {
                        await desc.Instance!.InitializeAsync(container);
                        Log.Information("插件已初始化: {Name}", desc.Metadata.Name);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "插件 {Name} 初始化失败", desc.Metadata.Name);
                    }

                // --- 2. 启动插件 ---
                Log.Information("=== 开始启动插件 ===");
                foreach (var desc in _loadedPlugins.Where(p => p.IsLoaded && p.Instance != null))
                    try
                    {
                        await desc.Instance!.StartAsync();
                        Log.Information("插件已启动: {Name}", desc.Metadata.Name);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "插件 {Name} 启动失败", desc.Metadata.Name);
                    }

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

            app.RunAsync();
            Log.Information("gRPC Server 正在监听端口: {Port}", port);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Kestrel 服务器启动失败");
        }
    }
}