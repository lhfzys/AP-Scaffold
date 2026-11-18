using AP.Infra.Database.Migration;
using FreeSql;

namespace AP.Infra.Recipe.Data;

/// <summary>配方模块 schema。</summary>
public sealed class RecipeSchemaContributor : IDatabaseSchemaContributor
{
    public string Name => "recipe";
    public int Version => 1;

    public void Apply(IFreeSql freeSql)
    {
        freeSql.CodeFirst.SyncStructure(typeof(Entities.Recipe));
    }
}
