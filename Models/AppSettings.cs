namespace ImagePdfToolkit.Models;

public sealed class AppSettings
{
    public string LanguageCode { get; set; } = Services.LocalizationService.SystemLanguageCode;

    public string? SourceImagePath { get; set; }

    public string?[] WatermarkPaths { get; set; } = new string?[AppConstants.SlotCount];

    public string? OutputDirectory { get; set; }

    public string? LastSaveDirectory { get; set; }

    public int WatermarkSizePercent { get; set; } = AppConstants.DefaultWatermarkSizePercent;

    public int WatermarkColorDepthPercent { get; set; } = AppConstants.DefaultWatermarkColorDepthPercent;

    public int WatermarkOffsetXPercent { get; set; }

    public int WatermarkOffsetYPercent { get; set; }

    public int? Watermark2SizePercent { get; set; }

    public int? Watermark2ColorDepthPercent { get; set; }

    public int? Watermark2OffsetXPercent { get; set; }

    public int? Watermark2OffsetYPercent { get; set; }

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public double? WindowWidth { get; set; }

    public double? WindowHeight { get; set; }

    public bool IsWindowMaximized { get; set; }
}
