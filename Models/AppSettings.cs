namespace RandomWatermarkTool.Models;

public sealed class AppSettings
{
    public string? SourceImagePath { get; set; }

    public string?[] WatermarkPaths { get; set; } = new string?[AppConstants.SlotCount];

    public string? OutputDirectory { get; set; }

    public string? LastSaveDirectory { get; set; }

    public int WatermarkSizePercent { get; set; } = AppConstants.DefaultWatermarkSizePercent;

    public int WatermarkColorDepthPercent { get; set; } = AppConstants.DefaultWatermarkColorDepthPercent;

    public int WatermarkOffsetXPercent { get; set; }

    public int WatermarkOffsetYPercent { get; set; }
}
