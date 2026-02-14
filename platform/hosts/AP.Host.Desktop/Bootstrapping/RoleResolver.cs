using AP.Core.Enums;
using Microsoft.Extensions.Configuration;

namespace AP.Host.Desktop.Bootstrapping;

/// <summary>
/// 角色解析器 (决定应用以何种模式启动)
/// </summary>
public static class RoleResolver
{
    public static AppRole Resolve(string[] args)
    {
        // 1. 优先读取命令行参数: --role=Server
        var roleArg = args.FirstOrDefault(a => a.StartsWith("--role=", StringComparison.OrdinalIgnoreCase));
        if (roleArg != null)
        {
            var value = roleArg.Split('=')[1];
            if (Enum.TryParse<AppRole>(value, true, out var role)) return role;
        }

        // 2. 降级读取 appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("Configuration/appsettings.json", true)
            .Build();

        var configRole = config["AppRole"];
        if (Enum.TryParse<AppRole>(configRole, true, out var roleFromConfig)) return roleFromConfig;

        // 3. 默认单机模式
        return AppRole.Standalone;
    }
}