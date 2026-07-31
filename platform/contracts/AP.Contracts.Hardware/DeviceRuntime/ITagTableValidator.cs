namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 点表校验器：按启动加载（<see cref="ITagTable"/>）完全相同的规则对候选点定义做全量校验。
/// 供点表编辑等界面在保存前预检，避免非法点表写盘后下次启动快速失败。
/// </summary>
public interface ITagTableValidator
{
    /// <summary>
    /// 全量校验（点名唯一 / 设备已注册 / 地址经驱动验证器解析）。
    /// 返回全部错误（空列表 = 通过），不抛异常。
    /// </summary>
    IReadOnlyList<string> Validate(IReadOnlyList<TagDefinition> tags);
}
