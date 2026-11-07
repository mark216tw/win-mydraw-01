using System.Numerics;
using Com.StudentTools.MyDraw.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;

namespace Com.StudentTools.MyDraw.Helpers;

public static class StrokeOutline
{
    public static CanvasGeometry Create(ICanvasResourceCreator resourceCreator, DrawingStroke stroke) =>
        Create(resourceCreator, stroke, StrokeSampler.Sample(stroke));

    public static CanvasGeometry Create(
        ICanvasResourceCreator resourceCreator,
        DrawingStroke stroke,
        IReadOnlyList<BrushSample> samples)
    {
        using CanvasPathBuilder builder = new(resourceCreator);
        // All contours use the same winding. Overlapping capsules form a union,
        // rather than punching holes at crossings or reversed portions of a stroke.
        builder.SetFilledRegionDetermination(CanvasFilledRegionDetermination.Winding);
        for (int index = 0; index < samples.Count; index++)
        {
            AddDisc(builder, samples[index].Position, samples[index].Radius);
            if (index == 0)
            {
                continue;
            }

            BrushSample previous = samples[index - 1];
            BrushSample current = samples[index];
            Vector2 delta = current.Position - previous.Position;
            if (delta.LengthSquared() < 0.0001f)
            {
                continue;
            }

            Vector2 direction = Vector2.Normalize(delta);
            Vector2 normal = new(-direction.Y, direction.X);
            builder.BeginFigure(previous.Position + (normal * previous.Radius));
            builder.AddLine(current.Position + (normal * current.Radius));
            builder.AddLine(current.Position - (normal * current.Radius));
            builder.AddLine(previous.Position - (normal * previous.Radius));
            builder.EndFigure(CanvasFigureLoop.Closed);
        }

        return CanvasGeometry.CreatePath(builder);
    }

    private static void AddDisc(CanvasPathBuilder builder, Vector2 position, float radius)
    {
        int segments = radius switch
        {
            < 2f => 8,
            < 6f => 12,
            < 12f => 16,
            _ => 20
        };
        builder.BeginFigure(position + new Vector2(radius, 0));
        for (int step = 1; step < segments; step++)
        {
            float current = -MathF.Tau * step / segments;
            builder.AddLine(position + (new Vector2(MathF.Cos(current), MathF.Sin(current)) * radius));
        }

        builder.EndFigure(CanvasFigureLoop.Closed);
    }
}
