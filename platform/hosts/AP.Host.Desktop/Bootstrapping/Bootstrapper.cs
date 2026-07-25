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
using AP.Infra.Hardware.Extensions;
using AP.Infra.Logging.Configuration;
using AP.Infra.Logging.Helpers;
using AP.Infra.Report.Extensions;
using AP.Infra.Resilience.Configuration;
using AP.Infra.Recipe.Configuration;
using AP.Infra.Security.Configuration;
using AP.Shared.UI.Services;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Application = System.Windows.Application;

#endregion

namespace AP.Host.Desktop.Bootstrapping;

/// <summary>
///     插件启动器
/// </summary>
public class Bootstrapper : PrismBootstrapper
{
    private readonly AppRole _appRole;
    private readonly Views.SplashWindow? _splashWindow;
    private IConfigurationRoot _configuration = null!;
    private readonly PluginLoader _pluginLoader;
    private List<PluginDescriptor> _loadedPlugins = new();
    private readonly List<(string PluginName, string Error)> _failedPlugins = new();
    // 致命插件错误（Required 插件失败 / 插件图致命问题），非空时中止启动
    private readonly List<string> _fatalPluginErrors = new();
    private WebApplication? _grpcApp;
    private PluginLifecycleManager? _lifecycleManager;

    public Bootstrapper(AppRole appRole, Views.SplashWindow? splashWindow = null)
    {
        _appRole = appRole;
        _splashWindow = splashWindow;
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

        var securityEnabled = _configuration.GetValue<bool?>("Security:Enabled") ?? true;
        if (securityEnabled)
        {
            // 登录前必须完成安全模块初始化，确保默认账号/角色/权限已就绪
            try
            {
                var securityInitializer = Container.Resolve<AP.Contracts.Security.Abstractions.ISecurityDbInitializer>();
                securityInitializer.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
                Log.Information("登录前安全模块初始化完成");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "登录前安全模块初始化失败");
            }

            // 登录窗口需要在最前显示，先关闭启动画面避免遮挡
            CloseSplashWindow();

            var loginService = Container.Resolve<AP.Contracts.System.Services.ILoginService>();

            // 弹出登录窗口
            if (!loginService.ShowLoginDialog())
            {
                Application.Current.Shutdown();
                return;
            }

            // 首次登录强制修改密码
            var identityService = Container.Resolve<AP.Contracts.Security.Abstractions.IIdentityService>();
            var currentUser = identityService.CurrentUser;
            if (currentUser != null && currentUser.MustChangePassword)
            {
                if (!loginService.ShowChangePasswordDialog(currentUser.UserName))
                {
                    Application.Current.Shutdown();
                    return;
                }
            }
        }

        Application.Current.MainWindow.Show();
    }

    private void UpdateSplashStatus(string message, double progress)
    {
        _splashWindow?.Dispatcher.Invoke(() =>
        {
            if (_splashWindow.ViewModel == null) return;
            _splashWindow.ViewModel.StatusText = message;
            _splashWindow.ViewModel.ProgressValue = progress;
        });
    }

    private void CloseSplashWindow()
    {
        _splashWindow?.Dispatcher.Invoke(() => _splashWindow.Close());
    }

    /// <summary>
    ///     致命错误中止启动：记录日志、提示操作员并退出进程
    /// </summary>
    private void AbortStartup(string title, IReadOnlyList<string> errors)
    {
        var detail = string.Join("\n", errors.Select(e => $"  • {e}"));
        Log.Fatal("{Title}:\n{Detail}", title, detail);

        try
        {
            CloseSplashWindow();
            System.Windows.MessageBox.Show(
                $"{title}：\n{detail}",
                "启动失败",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            Environment.Exit(1);
        }
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
        services.AddPlatformSecurity(_configuration);
        services.AddPlatformRecipe(_configuration);
        services.AddPlcHardware(_configuration);
        services.AddReportFramework(_configuration);

        // --- 添加 gRPC 服务 (根据角色) ---
        // ❄ 封存：Server/Client 分布式模式代码保留但不维护、不验证，解冻需专项评审（docs/EVOLUTION_PLAN.md 0.1 节）
        if (_appRole.HasFlag(AppRole.Server))
        {
            services.AddPlatformGrpcServer(_configuration);
            ServerBootstrap.RegisterServices(services);
        }

        if (_appRole.HasFlag(AppRole.Client))
            ClientBootstrap.RegisterServices(services);

        // --- 扫描插件元数据 ---
        _loadedPlugins = _pluginLoader.DiscoverPlugins(_appRole);
        Log.Information("已发现 {Count} 个适用插件", _loadedPlugins.Count);

        // 插件图致命问题（重复 ID / Required 插件依赖缺失）直接进入致命错误列表
        foreach (var issue in _pluginLoader.Issues.Where(i => i.IsFatal))
            _fatalPluginErrors.Add(issue.Message);

        // 创建 logger 工厂用于传递给插件构造函数
        using var pluginLoggerFactory = LoggerFactory.Create(b => b.AddSerilog());

        var pluginAssemblies = new List<Assembly> { typeof(Bootstrapper).Assembly }; // 包含 Host 自身

        // ===== 第一阶段：用仅包含平台服务的临时容器实例化插件，收集服务注册和程序集 =====
        // 注意：ConfigureServices 里通常只注册服务，不解析服务，因此临时容器足够
        var tempProvider = services.BuildServiceProvider();

        foreach (var descriptor in _loadedPlugins)
            try
            {
                var pluginLogger = pluginLoggerFactory.CreateLogger(descriptor.PluginType);

                // 临时实例仅用于调用 ConfigureServices，不会被最终容器使用
                var tempInstance = ActivatorUtilities
                    .CreateInstance(tempProvider, descriptor.PluginType, pluginLogger) as IPlugin;

                if (tempInstance == null)
                {
                    descriptor.IsLoaded = false;
                    continue;
                }

                // 注册插件自身的服务（这些服务会被加入最终容器）
                if (tempInstance is IConfigurablePlugin configurable)
                    configurable.ConfigureServices(services, _configuration);

                // 第一阶段成功，标记为待加载
                descriptor.IsLoaded = true;

                // 收集程序集用于 MediatR 扫描
                pluginAssemblies.Add(descriptor.PluginType.Assembly);
            }
            catch (Exception ex)
            {
                // 捕获异常，防止一个插件崩溃导致整个程序启动失败
                Log.Error(ex, "插件 {Name} 服务配置失败", descriptor.Metadata.Name);
                _failedPlugins.Add((descriptor.Metadata.Name, ex.Message));
                if (descriptor.Metadata.Required)
                    _fatalPluginErrors.Add($"必需插件 {descriptor.Metadata.Name} 服务配置失败: {ex.Message}");
                descriptor.IsLoaded = false;
            }

        // --- 注册 MediatR ---
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(pluginAssemblies.ToArray()));

        // ===== 第二阶段：构建最终容器，并用最终容器重新实例化插件 =====
        // 这样插件构造函数能解析到所有已注册的服务（包括其他插件注册的服务）
        var finalProvider = services.BuildServiceProvider();

        var finalInstances = new List<IPlugin>();
        foreach (var descriptor in _loadedPlugins.Where(d => d.IsLoaded))
            try
            {
                var pluginLogger = pluginLoggerFactory.CreateLogger(descriptor.PluginType);

                var instance = ActivatorUtilities
                    .CreateInstance(finalProvider, descriptor.PluginType, pluginLogger) as IPlugin;

                if (instance == null) continue;

                descriptor.Instance = instance;
                descriptor.IsLoaded = true;
                finalInstances.Add(instance);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "插件 {Name} 最终实例化失败", descriptor.Metadata.Name);
                _failedPlugins.Add((descriptor.Metadata.Name, ex.Message));
                if (descriptor.Metadata.Required)
                    _fatalPluginErrors.Add($"必需插件 {descriptor.Metadata.Name} 实例化失败: {ex.Message}");
                descriptor.IsLoaded = false;
            }

        // --- 桥接容器 (Microsoft DI -> DryIoc) ---
        var dryIocContainer = containerRegistry.GetContainer();
        dryIocContainer.Populate(services);

        // 将最终插件实例注册到 DryIoc，便于通过 IEnumerable<IPlugin> 解析
        // 同时注册 INavigationContributor，供布局插件收集菜单项
        foreach (var instance in finalInstances)
        {
            containerRegistry.RegisterInstance(typeof(IPlugin), instance);
            if (instance is AP.Shared.PluginSDK.Navigation.INavigationContributor navigationContributor)
                containerRegistry.RegisterInstance(typeof(AP.Shared.PluginSDK.Navigation.INavigationContributor), navigationContributor);
        }

        // --- 注册生命周期管理器 ---
        _lifecycleManager = new PluginLifecycleManager(
            LoggerFactory.Create(b => b.AddSerilog()).CreateLogger<PluginLifecycleManager>());
        _lifecycleManager.RegisterPlugins(_loadedPlugins.Where(p => p.IsLoaded));
        containerRegistry.RegisterInstance(_lifecycleManager);

        containerRegistry.RegisterSingleton<ICustomDialogService, MaterialDialogService>();
        // 让插件也能解析 Prism 的 IContainerProvider
        containerRegistry.RegisterInstance<IContainerProvider>((IContainerProvider)containerRegistry);
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

                // --- 必需插件校验：存在致命错误时中止启动（重复 ID / Required 插件失败） ---
                if (_fatalPluginErrors.Count > 0)
                    AbortStartup("必需插件加载失败，系统无法启动", _fatalPluginErrors);

                UpdateSplashStatus("正在清理过期日志...", 5);

                // --- 0. 启动时清理过期日志（零开销，仅执行一次） ---
                var logRetainDays = _configuration.GetValue<int>("Logging:RetainedFileCount", 90);
                LogCleanupHelper.CleanupIfNeeded("logs", logRetainDays);

                var securityEnabled = _configuration.GetValue<bool?>("Security:Enabled") ?? true;
                if (securityEnabled)
                {
                    UpdateSplashStatus("正在初始化安全模块...", 15);

                    // --- 0.5 初始化安全模块数据库（默认账号/角色/权限） ---
                    try
                    {
                        var securityInitializer = container.Resolve<AP.Contracts.Security.Abstractions.ISecurityDbInitializer>();
                        await securityInitializer.InitializeAsync(CancellationToken.None);
                        Log.Information("安全模块数据库初始化完成");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "安全模块初始化失败");
                    }
                }
                else
                {
                    Log.Information("安全模块已禁用，跳过数据库初始化");
                }

                UpdateSplashStatus("正在初始化配方模块...", 30);

                // --- 0.6 初始化配方模块数据库 ---
                try
                {
                    var recipeDbInitializer = container.Resolve<AP.Contracts.Recipe.Abstractions.IRecipeDbInitializer>();
                    await recipeDbInitializer.InitializeAsync(CancellationToken.None);
                    Log.Information("配方模块数据库初始化完成");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "配方模块初始化失败");
                }

                UpdateSplashStatus("正在初始化报表模块...", 40);

                // --- 0.7 初始化报表模块数据库 ---
                try
                {
                    var reportDbInitializer = container.Resolve<AP.Infra.Report.Services.ReportDatabaseInitializer>();
                    await reportDbInitializer.StartAsync(CancellationToken.None);
                    Log.Information("报表模块数据库初始化完成");

                    // 宿主不自动启动 IHostedService，报表后台任务需显式启动
                    await container.Resolve<AP.Infra.Report.Services.ReportScheduler>().StartAsync(CancellationToken.None);
                    await container.Resolve<AP.Infra.Report.Services.ReportCleanupService>().StartAsync(CancellationToken.None);
                    Log.Information("报表后台任务（定时归档/定期清理）已启动");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "报表模块初始化失败");
                }

                UpdateSplashStatus("正在初始化插件...", 50);

                // --- 1. 初始化并启动插件（通过生命周期管理器，带状态机跟踪） ---
                await _lifecycleManager!.InitializePluginsAsync(container);
                await _lifecycleManager.StartPluginsAsync();

                // --- 1.1 Required 插件初始化/启动失败时中止启动 ---
                var requiredFailed = _lifecycleManager.GetFailedPlugins()
                    .Where(p => p.Metadata.Required)
                    .Select(p => $"必需插件 {p.Metadata.Name} 初始化/启动失败")
                    .ToList();
                if (requiredFailed.Count > 0)
                    AbortStartup("必需插件启动失败，系统无法启动", requiredFailed);

                UpdateSplashStatus("正在启动服务...", 75);

                // --- 2. 设备注册：全部 IDevice 单例登记进设备注册表（Device Runtime Model） ---
                try
                {
                    var deviceRegistry = container.GetService<AP.Contracts.Hardware.DeviceRuntime.IDeviceRegistry>();
                    if (deviceRegistry != null)
                        foreach (var device in container.GetServices<AP.Contracts.Hardware.DeviceRuntime.IDevice>())
                            deviceRegistry.Register(device);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "设备注册失败");
                }

                // --- 3. 启动 gRPC Server (如果是服务端) --- ❄ 封存，见上文注释
                if (_appRole.HasFlag(AppRole.Server)) StartKestrelServer(container);

                // --- 4. 启动 gRPC Client Worker (如果是客户端) --- ❄ 封存，见上文注释
                if (_appRole.HasFlag(AppRole.Client))
                {
                    var clientWorker = container.GetService<GrpcClientWorker>();
                    if (clientWorker != null)
                    {
                        await clientWorker.StartAsync(CancellationToken.None);
                        Log.Information("gRPC 客户端后台服务已启动");
                    }
                }

                UpdateSplashStatus("正在完成启动...", 95);

                // 汇总展示加载失败的插件（工业现场关键：操作员需知道功能缺失）
                if (_failedPlugins.Count > 0)
                {
                    var failedMsg = string.Join("\n", _failedPlugins.Select(f => $"  • {f.PluginName}: {f.Error}"));
                    Log.Warning("以下 {Count} 个插件加载失败:\n{FailedPlugins}", _failedPlugins.Count, failedMsg);
                }

                var eventAggregator = container.Resolve<IEventAggregator>();
                eventAggregator.GetEvent<AppInitializedEvent>().Publish();

                CloseSplashWindow();

                Log.Information(">>> 系统启动完成 <<<");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "系统启动过程中发生未捕获异常");

                // 兜底：关闭 Splash 并提示操作员，避免启动失败后界面卡死无任何反馈
                try
                {
                    CloseSplashWindow();
                    System.Windows.MessageBox.Show(
                        $"系统启动失败：{ex.Message}\n详细信息请查看日志。",
                        "启动失败",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
                catch
                {
                    // 兜底 UI 操作失败不掩盖原始异常
                }
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

            // 3. 停止 gRPC Client Worker ❄ 封存，见上文注释
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
