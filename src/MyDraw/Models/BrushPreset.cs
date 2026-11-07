namespace Com.StudentTools.MyDraw.Models;

public sealed record BrushPreset(
    string Id,
    DrawingTool Tool,
    float DefaultSize,
    float DefaultOpacity,
    bool UseSizePressure,
    bool UseOpacityPressure,
    float DefaultSoftness = 0,
    AirbrushMode DefaultAirbrushMode = AirbrushMode.Mist,
    float DefaultFlow = 1);
