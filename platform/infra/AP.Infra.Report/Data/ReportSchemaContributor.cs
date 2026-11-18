using AP.Infra.Database.Migration;
using FreeSql;

namespace AP.Infra.Report.Data;

/// <summary>报表模块 schema。</summary>
public sealed class ReportSchemaContributor : IDatabaseSchemaContributor
{
    public string Name => "report";
    public int Version => 1;

    public void Apply(IFreeSql freeSql)
    {
        freeSql.CodeFirst.SyncStructure(typeof(Entities.ReportArchive));
    }
}
