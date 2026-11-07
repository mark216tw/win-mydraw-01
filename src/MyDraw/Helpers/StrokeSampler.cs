using System.Numerics;
using Com.StudentTools.MyDraw.Models;

namespace Com.StudentTools.MyDraw.Helpers;

public static class StrokeSampler
{
    private const float MinRadius = 0.35f;
    private const float PressureFloor = 0.12f;

    public static IReadOnlyList<BrushSample> Sample(DrawingStroke stroke)
    {
        if (stroke.Points.Count == 0)
        {
            return [];
        }

        int count = stroke.Points.Count;
        List<Vector2> positions = new(count);
        List<float> pressures = new(count);
        List<float> tiltX = new(count);
        List<float> tiltY = new(count);
        foreach (StrokePoint point in stroke.Points)
        {
            positions.Add(point.Position);
            pressures.Add(point.Pressure);
            tiltX.Add(point.TiltX);
            tiltY.Add(point.TiltY);
        }

        Smooth(positions, pressures);

        if (count == 1)
        {
            float radius = GetRadius(stroke, pressures[0], tiltX[0], tiltY[0], Vector2.Zero);
            return [new BrushSample(positions[0], radius, pressures[0], tiltX[0], tiltY[0], 0)];
        }

        List<BrushSample> samples = new(count * 4);
        float arcLength = 0;
        for (int index = 0; index < count - 1; index++)
        {
            int previousIndex = Math.Max(0, index - 1);
            int nextIndex = Math.Min(count - 1, index + 2);
            Vector2 p0 = positions[previousIndex];
            Vector2 p1 = positions[index];
            Vector2 p2 = positions[index + 1];
            Vector2 p3 = positions[nextIndex];
            float segmentLength = Vector2.Distance(p1, p2);
            float stepLength = Math.Clamp(stroke.Size * 0.25f, 0.75f, 3f);
            int steps = Math.Clamp((int)MathF.Ceiling(segmentLength / stepLength), 2, 128);
            Vector2 direction = p2 - p1;
            for (int step = 0; step < steps; step++)
            {
                float t = (float)step / steps;
                Vector2 position = CatmullRom(p0, p1, p2, p3, t);
                float pressure = Lerp(pressures[index], pressures[index + 1], t);
                float sampleTiltX = Lerp(tiltX[index], tiltX[index + 1], t);
                float sampleTiltY = Lerp(tiltY[index], tiltY[index + 1], t);
                if (samples.Count > 0)
                {
                    arcLength += Vector2.Distance(samples[^1].Position, position);
                }

                float radius = GetRadius(stroke, pressure, sampleTiltX, sampleTiltY, direction);
                samples.Add(new BrushSample(position, radius, pressure, sampleTiltX, sampleTiltY, arcLength));
            }
        }

        Vector2 endDirection = positions[^1] - positions[^2];
        float endRadius = GetRadius(stroke, pressures[^1], tiltX[^1], tiltY[^1], endDirection);
        if (samples.Count > 0)
        {
            arcLength += Vector2.Distance(samples[^1].Position, positions[^1]);
        }

        samples.Add(new BrushSample(
            positions[^1],
            endRadius,
            pressures[^1],
            tiltX[^1],
            tiltY[^1],
            arcLength));

        ApplyTaper(stroke, samples);
        return samples;
    }

    public static IReadOnlyList<BrushSample> Resample(IReadOnlyList<BrushSample> samples, float spacing)
    {
        if (samples.Count <= 1 || spacing <= 0.01f)
        {
            return samples;
        }

        List<BrushSample> result = new(samples.Count) { samples[0] };
        float nextDistance = spacing;
        for (int index = 1; index < samples.Count; index++)
        {
            BrushSample previous = samples[index - 1];
            BrushSample current = samples[index];
            float segmentLength = current.ArcLength - previous.ArcLength;
            if (segmentLength <= 0.0001f)
            {
                continue;
            }

            while (nextDistance <= current.ArcLength)
            {
                float t = (nextDistance - previous.ArcLength) / segmentLength;
                result.Add(Lerp(previous, current, t));
                nextDistance += spacing;
            }
        }

        BrushSample last = samples[^1];
        if (Vector2.DistanceSquared(result[^1].Position, last.Position) > 0.0001f)
        {
            result.Add(last);
        }

        return result;
    }

    private static void Smooth(List<Vector2> positions, List<float> pressures)
    {
        if (positions.Count < 3)
        {
            return;
        }

        for (int pass = 0; pass < 2; pass++)
        {
            Vector2[] positionCopy = [.. positions];
            float[] pressureCopy = [.. pressures];
            for (int index = 1; index < positions.Count - 1; index++)
            {
                positions[index] =
                    (positionCopy[index - 1] * 0.25f) +
                    (positionCopy[index] * 0.5f) +
                    (positionCopy[index + 1] * 0.25f);
                pressures[index] =
                    (pressureCopy[index - 1] * 0.25f) +
                    (pressureCopy[index] * 0.5f) +
                    (pressureCopy[index + 1] * 0.25f);
            }
        }
    }

    private static float GetRadius(
        DrawingStroke stroke,
        float pressure,
        float tiltX,
        float tiltY,
        Vector2 direction)
    {
        float pressureFactor = stroke.UseSizePressure ? MathF.Max(PressureFloor, pressure) : 1f;
        float radius = stroke.Size * pressureFactor / 2f;
        if (stroke.Tool == DrawingTool.Calligraphy)
        {
            float tiltMagnitude = Math.Clamp((MathF.Abs(tiltX) + MathF.Abs(tiltY)) / 120f, 0, 1);
            float nibAngle = tiltMagnitude > 0.05f
                ? MathF.Atan2(tiltY, tiltX)
                : -MathF.PI / 4f;
            float strokeAngle = direction.LengthSquared() > 0.0001f
                ? MathF.Atan2(direction.Y, direction.X)
                : 0;
            float orientation = 0.55f + (0.45f * MathF.Abs(MathF.Sin(strokeAngle - nibAngle)));
            radius *= (0.55f + (tiltMagnitude * 0.9f)) * orientation;
        }

        return MathF.Max(MinRadius, radius);
    }

    private static void ApplyTaper(DrawingStroke stroke, List<BrushSample> samples)
    {
        if (samples.Count < 3 || !UsesTaper(stroke.Tool))
        {
            return;
        }

        float totalLength = samples[^1].ArcLength;
        float baseRadius = MathF.Max(0.5f, stroke.Size / 2f);
        float taperLength = MathF.Min(totalLength * 0.18f, baseRadius * 2.2f);
        if (taperLength <= 0.01f)
        {
            return;
        }

        for (int index = 0; index < samples.Count; index++)
        {
            float startDistance = samples[index].ArcLength;
            float endDistance = totalLength - startDistance;
            float startFactor = SmoothStep(0, taperLength, startDistance);
            float endFactor = SmoothStep(0, taperLength, endDistance);
            float factor = 0.25f + (0.75f * MathF.Min(startFactor, endFactor));
            samples[index] = samples[index] with { Radius = samples[index].Radius * factor };
        }
    }

    private static bool UsesTaper(DrawingTool tool) => tool is
        DrawingTool.Pen or
        DrawingTool.Pencil or
        DrawingTool.Ballpoint or
        DrawingTool.Marker or
        DrawingTool.Calligraphy or
        DrawingTool.Crayon;

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (edge1 <= edge0)
        {
            return value >= edge1 ? 1 : 0;
        }

        float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - (2 * t));
    }

    private static BrushSample Lerp(BrushSample a, BrushSample b, float t) => new(
        Vector2.Lerp(a.Position, b.Position, t),
        Lerp(a.Radius, b.Radius, t),
        Lerp(a.Pressure, b.Pressure, t),
        Lerp(a.TiltX, b.TiltX, t),
        Lerp(a.TiltY, b.TiltY, t),
        Lerp(a.ArcLength, b.ArcLength, t));

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t0 = 0;
        float t1 = t0 + MathF.Sqrt(Vector2.Distance(p0, p1) + 0.0001f);
        float t2 = t1 + MathF.Sqrt(Vector2.Distance(p1, p2) + 0.0001f);
        float t3 = t2 + MathF.Sqrt(Vector2.Distance(p2, p3) + 0.0001f);
        float current = t1 + ((t2 - t1) * t);
        Vector2 a1 = Vector2.Lerp(p0, p1, (current - t0) / (t1 - t0));
        Vector2 a2 = Vector2.Lerp(p1, p2, (current - t1) / (t2 - t1));
        Vector2 a3 = Vector2.Lerp(p2, p3, (current - t2) / (t3 - t2));
        Vector2 b1 = Vector2.Lerp(a1, a2, (current - t0) / (t2 - t0));
        Vector2 b2 = Vector2.Lerp(a2, a3, (current - t1) / (t3 - t1));
        return Vector2.Lerp(b1, b2, (current - t1) / (t2 - t1));
    }
}
