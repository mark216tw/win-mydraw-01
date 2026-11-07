using Com.StudentTools.MyDraw.Models;

namespace Com.StudentTools.MyDraw.Services;

public sealed record ProjectLoadResult(
    int Width,
    int Height,
    IReadOnlyList<DrawingLayer> Layers,
    IReadOnlyList<DrawingStroke> Strokes);
