using AP.Infra.Database.Migration;
using FreeSql;

namespace AP.Infra.Recipe.Data;

/// <summary>配方模块 schema。</summary>
public sealed class RecipeSchemaContributor : IDatabaseSchemaContributor
{
    public string Name => "recipe";
    public int Version => 2;

    public void Apply(IFreeSql freeSql)
    {
        freeSql.CodeFirst.SyncStructure(typeof(Entities.Recipe));
        freeSql.CodeFirst.SyncStructure(typeof(Entities.RecipeState));
        freeSql.CodeFirst.SyncStructure(typeof(Entities.RecipeVersion));
    }
}
