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
            if (Enum.TryParse<AppRole>(value, true, out var role)) return EnsureStandalone(role);
        }

        // 2. 降级读取 appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("Configuration/appsettings.json", true)
            .Build();

        var configRole = config["AppRole"];
        if (Enum.TryParse<AppRole>(configRole, true, out var roleFromConfig)) return EnsureStandalone(roleFromConfig);

        // 3. 默认单机模式
        return AppRole.Standalone;
    }

    /// <summary>
    /// Server/Client 的 gRPC 宿主代码已物理移出交付链；显式拒绝，避免“以为在跑服务端，实际跑单机 UI”。
    /// </summary>
    private static AppRole EnsureStandalone(AppRole role)
    {
        if (role != AppRole.Standalone)
            throw new InvalidOperationException(
                $"当前交付物只支持 Standalone 单机模式；AppRole={role} 的 Server/Client（gRPC）能力已冻结并从解决方案移除。");

        return role;
    }
}