using System.Numerics;

namespace Com.StudentTools.MyDraw.Helpers;

public readonly record struct BrushSample(
    Vector2 Position,
    float Radius,
    float Pressure,
    float TiltX,
    float TiltY,
    float ArcLength);
