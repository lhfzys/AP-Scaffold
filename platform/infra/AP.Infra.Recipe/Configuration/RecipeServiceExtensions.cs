using AP.Contracts.Recipe.Abstractions;
using AP.Infra.Recipe.Data;
using AP.Infra.Recipe.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Infra.Recipe.Configuration;

/// <summary>
/// 配方模块服务注册扩展
/// </summary>
public static class RecipeServiceExtensions
{
    /// <summary>
    /// 注册配方管理服务
    /// </summary>
    public static IServiceCollection AddPlatformRecipe(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IRecipeManager, RecipeManager>();
        services.AddSingleton<IRecipeDbInitializer, RecipeDbInitializer>();
        return services;
    }
}
