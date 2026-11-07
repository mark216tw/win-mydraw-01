using System.Numerics;

namespace Com.StudentTools.MyDraw.Models;

public readonly record struct StrokePoint(Vector2 Position, float Pressure, float TiltX, float TiltY);
