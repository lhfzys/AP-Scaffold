using AP.Infra.Report.Abstractions;
using AP.Infra.Report.Configuration;
using AP.Infra.Report.Entities;
using AP.Infra.Report.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Infra.Report.Extensions;

/// <summary>
/// 报表框架 DI 注册扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加报表框架服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddReportFramework(this IServiceCollection services, IConfiguration configuration)
    {
        // 绑定配置
        var configSection = configuration.GetSection(ReportOptions.SectionName);
        services.Configure<ReportOptions>(configSection);

        // 注册核心服务
        services.AddSingleton<IExcelExporter, ExcelExporter>();
        services.AddSingleton<IReportStorage, ReportStorage>();
        services.AddSingleton<IReportRepository, ReportRepository>();
        services.AddSingleton<ReportService>();

        // 注册后台服务
        services.AddHostedService<ReportScheduler>();
        services.AddHostedService<ReportCleanupService>();

        // 注册数据库初始化宿主服务（确保表结构存在）
        services.AddHostedService<ReportDatabaseInitializer>();

        return services;
    }

    /// <summary>
    /// 添加报表框架服务（使用默认配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddReportFramework(this IServiceCollection services, Action<ReportOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<ReportOptions>(_ => { });
        }

        // 注册核心服务
        services.AddSingleton<IExcelExporter, ExcelExporter>();
        services.AddSingleton<IReportStorage, ReportStorage>();
        services.AddSingleton<IReportRepository, ReportRepository>();
        services.AddSingleton<ReportService>();

        // 注册后台服务
        services.AddHostedService<ReportScheduler>();
        services.AddHostedService<ReportCleanupService>();

        return services;
    }

    /// <summary>
    /// 添加报表数据提供者
    /// </summary>
    /// <typeparam name="TProvider">提供者类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddReportDataProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IReportDataProvider
    {
        services.AddSingleton<IReportDataProvider, TProvider>();
        return services;
    }
}