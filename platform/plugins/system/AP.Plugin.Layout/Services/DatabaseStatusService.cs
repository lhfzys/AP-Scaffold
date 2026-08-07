using FreeSql;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Plugin.Layout.Services;

/// <summary>
/// 数据库连通状态探测（底部状态栏与首页"系统服务状态"卡共用）。
/// 每次调用执行一次轻量探测（select 1），周期控制由调用方负责。
/// </summary>
public sealed class DatabaseStatusService
{
    private readonly IServiceProvider _serviceProvider;

    public DatabaseStatusService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>探测一次，返回（状态文本, 级别 ok/err/none）。</summary>
    public async Task<(string Text, string Level)> ProbeAsync()
    {
        try
        {
            var freeSql = _serviceProvider.GetService<IFreeSql>();
            if (freeSql == null)
                return ("未启用", "none");

            await freeSql.Ado.ExecuteScalarAsync("select 1");
            return ("已连接", "ok");
        }
        catch
        {
            return ("异常", "err");
        }
    }
}
