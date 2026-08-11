namespace RandomWatermarkTool.Models;

public sealed record WatermarkRenderState(
    int SlotIndex,
    float Opacity,
    float Angle,
    int Seed,
    double AnchorX,
    double AnchorY);
