using Microsoft.Graphics.Canvas.Geometry;

namespace Com.StudentTools.MyDraw.Helpers;

public sealed class PreparedStroke : IDisposable
{
    internal PreparedStroke(
        IReadOnlyList<BrushSample> samples,
        IReadOnlyList<BrushSample> stamps,
        CanvasGeometry? outline)
    {
        Samples = samples;
        Stamps = stamps;
        Outline = outline;
    }

    public IReadOnlyList<BrushSample> Samples { get; }

    public IReadOnlyList<BrushSample> Stamps { get; }

    internal CanvasGeometry? Outline { get; }

    public void Dispose() => Outline?.Dispose();
}
