using Microsoft.Extensions.Configuration;

namespace AP.Shared.Utilities.Paths;

/// <summary>
/// 应用数据目录解析。
///
/// 程序文件与运行数据分离：
/// - 安装目录（Program Files）只读，放程序与默认配置；
/// - 数据根默认 %ProgramData%\AP-Scaffold，标准用户可写；
/// - 配置采用“安装目录默认值 + 数据根覆盖层”，保存只写覆盖层。
///
/// 显式指定（环境变量/配置）不可写时直接失败，不静默回退；
/// 默认位置不可写时回退到 %LocalAppData%\AP-Scaffold。
/// </summary>
public static class AppPaths
{
    public const string DataFolderName = "AP-Scaffold";
    public const string DataRootEnvironmentVariable = "AP_SCAFFOLD_DATA_ROOT";

    private static readonly object Gate = new();
    private static string? _dataRoot;

    /// <summary>当前数据根；未初始化时回退到程序目录（兼容测试与工具场景）。</summary>
    public static string DataRoot
    {
        get
        {
            lock (Gate)
            {
                return _dataRoot ?? AppContext.BaseDirectory;
            }
        }
    }

    /// <summary>是否已经显式初始化数据根。</summary>
    public static bool IsInitialized
    {
        get
        {
            lock (Gate)
            {
                return _dataRoot != null;
            }
        }
    }

    /// <summary>安装目录下的默认配置目录（只读）。</summary>
    public static string DefaultConfigDirectory => Path.Combine(AppContext.BaseDirectory, "Configuration");

    /// <summary>可写配置覆盖层目录。</summary>
    public static string ConfigDirectory => Path.Combine(DataRoot, "Configuration");

    /// <summary>日志目录。</summary>
    public static string LogDirectory => Path.Combine(DataRoot, "logs");

    /// <summary>报表目录。</summary>
    public static string ReportDirectory => Path.Combine(DataRoot, "reports");

    /// <summary>SQLite 数据库文件。</summary>
    public static string DatabasePath => Path.Combine(DataRoot, "local_data.db");

    /// <summary>现场点表文件。</summary>
    public static string TagTablePath => Path.Combine(ConfigDirectory, "tags.json");

    /// <summary>
    /// 解析数据根（按优先级：环境变量 → 配置 → ProgramData → LocalAppData）。
    /// 显式指定的路径不可写时抛出 <see cref="IOException"/>。
    /// </summary>
    public static string ResolveDataRoot(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var explicitRoot = Environment.GetEnvironmentVariable(DataRootEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(explicitRoot))
            explicitRoot = configuration["AppRuntime:DataRoot"];

        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var fullPath = ResolveExplicitPath(explicitRoot);
            if (!TryPrepare(fullPath, out var error))
            {
                throw new IOException(
                    $"数据目录不可写：{fullPath}。请检查目录权限，或设置环境变量 " +
                    $"{DataRootEnvironmentVariable} 指向可写目录。原因：{error}");
            }

            return fullPath;
        }

        var candidates = new[]
        {
            GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        var failures = new List<string>();
        foreach (var basePath in candidates)
        {
            if (string.IsNullOrWhiteSpace(basePath)) continue;

            var candidate = Path.Combine(basePath, DataFolderName);
            if (TryPrepare(candidate, out var error))
                return candidate;

            failures.Add($"{candidate}: {error}");
        }

        throw new IOException(
            "找不到可写的数据目录。已尝试：\n  " + string.Join("\n  ", failures) +
            $"\n请设置环境变量 {DataRootEnvironmentVariable} 指向可写目录。");
    }

    /// <summary>初始化数据根。生产环境只调用一次；测试用 <see cref="ResetForTests"/> 隔离。</summary>
    public static void Initialize(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);

        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(dataRoot));
        lock (Gate)
        {
            if (_dataRoot != null && !string.Equals(_dataRoot, fullPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"数据根已初始化为 {_dataRoot}，不能再改为 {fullPath}。");

            _dataRoot = fullPath;
        }
    }

    /// <summary>首次运行时准备数据目录：默认点表与旧版数据库迁移。</summary>
    public static void PrepareDataFiles()
    {
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(ReportDirectory);

        CopyDefaultTagTableIfMissing();
        MigrateLegacyDatabaseIfNeeded();
    }

    /// <summary>相对路径解析到数据根下；绝对路径原样返回。</summary>
    public static string ResolvePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return DataRoot;
        return Path.IsPathRooted(path) ? path : Path.Combine(DataRoot, path);
    }

    /// <summary>仅供单元测试：清除初始化状态。</summary>
    public static void ResetForTests()
    {
        lock (Gate)
        {
            _dataRoot = null;
        }
    }

    private static string ResolveExplicitPath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        return Path.IsPathRooted(expanded)
            ? Path.GetFullPath(expanded)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));
    }

    private static string GetFolderPath(Environment.SpecialFolder folder)
    {
        try
        {
            return Environment.GetFolderPath(folder);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool TryPrepare(string root, out string error)
    {
        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "Configuration"));
            Directory.CreateDirectory(Path.Combine(root, "logs"));
            Directory.CreateDirectory(Path.Combine(root, "reports"));

            var probe = Path.Combine(root, $".write-probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void CopyDefaultTagTableIfMissing()
    {
        var source = Path.Combine(DefaultConfigDirectory, "tags.json");
        if (!File.Exists(source) || File.Exists(TagTablePath)) return;

        File.Copy(source, TagTablePath);
        Serilog.Log.Information("已复制默认点表到数据目录: {Path}", TagTablePath);
    }

    private static void MigrateLegacyDatabaseIfNeeded()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "local_data.db");
        var target = DatabasePath;

        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) return;
        if (!File.Exists(source) || File.Exists(target)) return;

        var copied = new List<string>();
        try
        {
            var files = new[]
            {
                (source, target),
                (source + "-wal", target + "-wal"),
                (source + "-shm", target + "-shm")
            };

            foreach (var (from, to) in files)
            {
                if (!File.Exists(from)) continue;

                File.Copy(from, to, overwrite: false);
                copied.Add(to);
            }

            Serilog.Log.Information("已将旧版数据库迁移到数据目录: {Source} → {Target}", source, target);
        }
        catch (Exception ex)
        {
            foreach (var path in copied)
            {
                try { File.Delete(path); } catch { /* 清理失败不掩盖原始异常 */ }
            }

            throw new IOException(
                $"迁移旧版数据库失败：{source} → {target}。请检查数据目录权限，或手工复制后重试。", ex);
        }
    }
}
