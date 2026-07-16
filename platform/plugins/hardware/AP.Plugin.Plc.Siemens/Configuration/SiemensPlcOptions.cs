namespace AP.Plugin.Plc.Siemens.Configuration;

/// <summary>
/// 西门子 PLC 配置。
/// 注意：统一配置由 <see cref="AP.Contracts.Hardware.Models.PlcOptions"/> 承载，
/// 此处仅保留品牌特定的扩展字段（当前暂无）。
/// </summary>
public class SiemensPlcOptions
{
    public const string SectionName = "Plugins:Configuration:AP.Plugin.Plc.Siemens";
}
