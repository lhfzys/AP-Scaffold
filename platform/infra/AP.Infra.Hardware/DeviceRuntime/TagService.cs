using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.Services;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// Tag 服务实现：点名 → 设备 + 规范化地址（T4.2 已完成解析，读写零解析开销）。
/// PLC 设备经统一 <see cref="IPlcService"/> 代理访问（写操作自动审计不受影响）；
/// 通信失败一律返回 Quality=Bad，不抛异常。
/// </summary>
public sealed class TagService : ITagService
{
    private readonly ITagTable _tagTable;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly IPlcService _plcService;
    private readonly ILogger<TagService> _logger;

    public TagService(
        ITagTable tagTable,
        IDeviceRegistry deviceRegistry,
        IPlcService plcService,
        ILogger<TagService> logger)
    {
        _tagTable = tagTable;
        _deviceRegistry = deviceRegistry;
        _plcService = plcService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TagValue> ReadAsync(string name, CancellationToken ct = default)
    {
        var tag = FindTag(name);
        if (tag.Definition.Access == TagAccess.WriteOnly)
            throw new InvalidOperationException($"点 '{name}' 为只写，不可读取");

        if (CheckAccessible(tag, name) is { } inaccessible)
            return inaccessible;

        try
        {
            var value = await ReadByTypeAsync(tag, ct);
            return TagValue.Good(value);
        }
        catch (NotSupportedException ex)
        {
            return TagValue.Bad(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tag 读取失败: {Tag}", name);
            return TagValue.Bad(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<TagValue> WriteAsync(string name, object? value, CancellationToken ct = default)
    {
        var tag = FindTag(name);
        if (tag.Definition.Access == TagAccess.ReadOnly)
            throw new InvalidOperationException($"点 '{name}' 为只读，不可写入");

        if (CheckAccessible(tag, name) is { } inaccessible)
            return inaccessible;

        try
        {
            await WriteByTypeAsync(tag, name, value, ct);
            return TagValue.Good(value);
        }
        catch (NotSupportedException ex)
        {
            return TagValue.Bad(ex.Message);
        }
        catch (ArgumentException)
        {
            throw; // 值类型不匹配属调用方编程错误，如实上抛
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tag 写入失败: {Tag}", name);
            return TagValue.Bad(ex.Message);
        }
    }

    private ResolvedTag FindTag(string name)
    {
        return _tagTable.Find(name)
            ?? throw new ArgumentException($"点表中不存在点名: '{name}'", nameof(name));
    }

    /// <summary>设备可访问性检查：不可访问时返回 Bad 值，可访问返回 null。</summary>
    private TagValue? CheckAccessible(ResolvedTag tag, string name)
    {
        var device = _deviceRegistry.Find(tag.Definition.DeviceId);
        if (device == null)
            return TagValue.Bad($"设备未注册: '{tag.Definition.DeviceId}'");
        if (device.Info.Type != DeviceType.Plc)
            return TagValue.Bad($"设备类型 '{device.Info.Type}' 暂不支持 Tag 数据访问");
        if (device.State != DeviceConnectionState.Connected)
            return TagValue.Bad($"设备未连接（当前状态: {device.State}）");
        return null;
    }

    private async Task<object?> ReadByTypeAsync(ResolvedTag tag, CancellationToken ct)
    {
        var address = tag.NormalizedAddress;
        return tag.Definition.DataType switch
        {
            TagDataType.Bool => await _plcService.ReadAsync<bool>(address, ct),
            TagDataType.Int16 => await _plcService.ReadAsync<short>(address, ct),
            TagDataType.UInt16 => await _plcService.ReadAsync<ushort>(address, ct),
            TagDataType.Int32 => await _plcService.ReadAsync<int>(address, ct),
            TagDataType.UInt32 => await _plcService.ReadAsync<uint>(address, ct),
            TagDataType.Float => await _plcService.ReadAsync<float>(address, ct),
            TagDataType.String => await _plcService.ReadAsync<string>(address, ct),
            _ => throw new NotSupportedException($"驱动暂不支持类型: {tag.Definition.DataType}"),
        };
    }

    private async Task WriteByTypeAsync(ResolvedTag tag, string name, object? value, CancellationToken ct)
    {
        var address = tag.NormalizedAddress;
        switch (tag.Definition.DataType)
        {
            case TagDataType.Bool when value is bool b:
                await _plcService.WriteAsync(address, b, ct); break;
            case TagDataType.Int16 when value is short s:
                await _plcService.WriteAsync(address, s, ct); break;
            case TagDataType.UInt16 when value is ushort us:
                await _plcService.WriteAsync(address, us, ct); break;
            case TagDataType.Int32 when value is int i:
                await _plcService.WriteAsync(address, i, ct); break;
            case TagDataType.UInt32 when value is uint ui:
                await _plcService.WriteAsync(address, ui, ct); break;
            case TagDataType.Float when value is float f:
                await _plcService.WriteAsync(address, f, ct); break;
            case TagDataType.String when value is string str:
                await _plcService.WriteAsync(address, str, ct); break;
            case TagDataType.Int64 or TagDataType.UInt64 or TagDataType.Double or TagDataType.ByteArray:
                throw new NotSupportedException($"驱动暂不支持类型: {tag.Definition.DataType}");
            default:
                throw new ArgumentException(
                    $"点 '{name}' 的值类型与定义 {tag.Definition.DataType} 不匹配", nameof(value));
        }
    }
}
