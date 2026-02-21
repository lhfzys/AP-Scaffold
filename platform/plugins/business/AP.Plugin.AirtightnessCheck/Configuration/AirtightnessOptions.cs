namespace AP.Plugin.AirtightnessCheck.Configuration;

public class AirtightnessOptions
{
    public const string SectionName = "AirtightnessConfig";

    public double StandardPressure { get; set; } = 150.0; // 标准压力 kPa
    public double MaxLeakRate { get; set; } = 50.0; // 最大允许泄漏率 Pa/s
    public string DefaultRecipe { get; set; } = "Recipe-A01";
}