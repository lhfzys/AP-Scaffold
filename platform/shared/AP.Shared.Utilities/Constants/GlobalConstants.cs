namespace AP.Shared.Utilities.Constants;

/// <summary>
/// 全局应用常量
/// </summary>
public static class GlobalConstants
{
    //  项目命名规范
    public const string ProjectPrefix = "AP";

    public static class Paths
    {
        // 插件目录
        public const string Plugins = "plugins";

        // 配置文件名称
        public const string AppSettings = "appsettings.json";
    }

    // UI 区域名称常量
    public static class RegionNames
    {
        public const string MainRegion = "MainRegion";
        public const string HeaderRegion = "HeaderRegion";
        public const string StatusBarRegion = "StatusBarRegion";
    }

    // 配置文件的 Key
    public static class ConfigKeys
    {
        public const string AppRole = "AppRole";
        public const string DatabaseProvider = "Database:Provider";
        public const string SqliteConnection = "Database:SQLite:ConnectionString";

        public const string PostgreSqlConnection = "Database:PostgreSQL:ConnectionString";

        // gRPC 相关配置
        public const string GrpcServerUrl = "Grpc:ServerUrl";
        public const string ClientId = "Grpc:ClientId";
        public const string ClientName = "Grpc:ClientName";
    }
}