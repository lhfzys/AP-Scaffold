using AP.Infra.Resilience.Factories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace AP.Infra.Resilience.Configuration;

/// <summary>
/// 将所有内容注册到 DI 容器中
/// </summary>
public static class ResilienceServiceExtensions
{
    /// <summary>
    /// 注册平台韧性服务
    /// </summary>
    public static IServiceCollection AddPlatformResilience(this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. 绑定配置
        services.Configure<ResilienceOptions>(configuration.GetSection(ResilienceOptions.SectionName));

        // 2. 注册 Polly Registry
        services.AddResiliencePipelineRegistry<string>();

        // 3. 注册自定义工厂 (负责初始化策略)
        services.AddSingleton<ResiliencePipelineFactory>();

        // 4. 自动初始化工厂 (在应用启动时构建策略)
        // 这一步确保 Factory 的构造函数被调用，策略被注册到 Registry 中
        services.AddTransient<ResiliencePipeline>(sp =>
        {
            var factory = sp.GetRequiredService<ResiliencePipelineFactory>();
            // 仅作为触发器，实际使用时应注入 ResiliencePipelineFactory 或 ResiliencePipelineRegistry
            return ResiliencePipeline.Empty;
        });

        return services;
    }
}