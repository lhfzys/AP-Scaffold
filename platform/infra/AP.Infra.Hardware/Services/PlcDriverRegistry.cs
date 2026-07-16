using AP.Contracts.Hardware.Services;

namespace AP.Infra.Hardware.Services;

/// <summary>
/// PLC 驱动注册表。
/// 收集所有品牌插件注册的 <see cref="IPlcDriverFactory"/>，并按 DriverType 分发。
/// </summary>
public class PlcDriverRegistry
{
    private readonly Dictionary<string, IPlcDriverFactory> _factories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 注册一个驱动工厂。
    /// </summary>
    public void Register(IPlcDriverFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factories[factory.DriverType] = factory;
    }

    /// <summary>
    /// 获取指定类型的驱动工厂。
    /// </summary>
    /// <exception cref="InvalidOperationException">未找到对应驱动工厂时抛出</exception>
    public IPlcDriverFactory GetFactory(string driverType)
    {
        if (_factories.TryGetValue(driverType, out var factory))
            return factory;

        var registered = string.Join(", ", _factories.Keys.OrderBy(k => k));
        throw new InvalidOperationException(
            $"未找到 PLC 驱动 '{driverType}'。已注册的驱动: {(string.IsNullOrEmpty(registered) ? "无" : registered)}。");
    }

    /// <summary>
    /// 当前已注册的所有驱动类型。
    /// </summary>
    public IReadOnlyCollection<string> AvailableDrivers => _factories.Keys.OrderBy(k => k).ToList();

    /// <summary>
    /// 是否已注册指定驱动类型。
    /// </summary>
    public bool IsRegistered(string driverType) => _factories.ContainsKey(driverType);
}
