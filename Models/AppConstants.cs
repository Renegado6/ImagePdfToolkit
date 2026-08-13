namespace ImagePdfToolkit.Models;

public static class AppConstants
{
    public const int WatermarkGroupCount = 2;
    public const int SlotsPerWatermark = 3;
    public const int SlotCount = WatermarkGroupCount * SlotsPerWatermark;
    public const int DefaultWatermarkSizePercent = 100;
    public const int MinWatermarkSizePercent = 10;
    public const int MaxWatermarkSizePercent = 300;
    public const int DefaultWatermarkColorDepthPercent = 100;
    public const int MinWatermarkColorDepthPercent = 10;
    public const int MaxWatermarkColorDepthPercent = 100;
    public const int MinWatermarkOffsetPercent = -100;
    public const int MaxWatermarkOffsetPercent = 100;
    public const int WatermarkOffsetStepPercent = 5;
}
