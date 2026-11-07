using System.Numerics;
using Com.StudentTools.MyDraw.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Foundation;
using Windows.UI;

namespace Com.StudentTools.MyDraw.Helpers;

public static class BrushRenderer
{
    private const int TipSize = 128;
    private static readonly object TipLock = new();
    private static readonly List<DeviceTips> TipCaches = [];

    public static void ResetTips()
    {
        lock (TipLock)
        {
            foreach (DeviceTips deviceTips in TipCaches)
            {
                deviceTips.Dispose();
            }

            TipCaches.Clear();
        }
    }

    public static void ResetTips(CanvasDevice device)
    {
        lock (TipLock)
        {
            int index = FindDeviceTips(device);
            if (index < 0)
            {
                return;
            }

            TipCaches[index].Dispose();
            TipCaches.RemoveAt(index);
        }
    }

    public static PreparedStroke Prepare(CanvasDevice device, DrawingStroke stroke)
    {
        IReadOnlyList<BrushSample> samples = StrokeSampler.Sample(stroke);
        IReadOnlyList<BrushSample> stamps = PrepareStamps(stroke, samples);
        CanvasGeometry? outline = ShouldPrepareOutline(stroke, samples)
            ? StrokeOutline.Create(device, stroke, samples)
            : null;
        return new PreparedStroke(samples, stamps, outline);
    }

    public static void Draw(CanvasDrawingSession session, DrawingStroke stroke)
    {
        if (stroke.Points.Count == 0)
        {
            return;
        }

        using PreparedStroke prepared = Prepare(session.Device, stroke);
        Draw(session, stroke, prepared);
    }

    public static void Draw(
        CanvasDrawingSession session,
        DrawingStroke stroke,
        PreparedStroke prepared)
    {
        if (stroke.Points.Count == 0)
        {
            return;
        }

        switch (stroke.Tool)
        {
            case DrawingTool.Pencil:
                DrawPencil(session, stroke, prepared);
                return;
            case DrawingTool.Eraser:
                DrawEraser(session, stroke, prepared);
                return;
            case DrawingTool.Airbrush:
                DrawAirbrush(session, stroke, prepared);
                return;
            case DrawingTool.Crayon:
                DrawCrayon(session, stroke, prepared);
                return;
        }

        if (stroke.UseOpacityPressure)
        {
            DrawWithOpacityMask(session, stroke, prepared);
            return;
        }

        DrawOutline(session, stroke, prepared);
    }

    private static IReadOnlyList<BrushSample> PrepareStamps(
        DrawingStroke stroke,
        IReadOnlyList<BrushSample> samples)
    {
        if (samples.Count <= 1)
        {
            return samples;
        }

        return stroke.Tool switch
        {
            DrawingTool.Airbrush => StrokeSampler.Resample(
                samples,
                MathF.Max(
                    0.6f,
                    stroke.Size * (stroke.AirbrushMode == AirbrushMode.Grain ? 0.07f : 0.09f))),
            DrawingTool.Crayon => StrokeSampler.Resample(
                samples,
                MathF.Max(0.6f, stroke.Size * 0.08f)),
            DrawingTool.Eraser when stroke.Softness > 0.02f => StrokeSampler.Resample(
                samples,
                MathF.Max(0.5f, stroke.Size * 0.06f)),
            DrawingTool.Pencil => StrokeSampler.Resample(
                samples,
                MathF.Max(0.5f, stroke.Size * 0.12f)),
            _ when stroke.UseOpacityPressure => StrokeSampler.Resample(
                samples,
                MathF.Max(0.6f, stroke.Size * 0.12f)),
            _ => samples
        };
    }

    private static bool ShouldPrepareOutline(
        DrawingStroke stroke,
        IReadOnlyList<BrushSample> samples)
    {
        if (samples.Count < 2)
        {
            return false;
        }

        return stroke.Tool switch
        {
            DrawingTool.Eraser => stroke.Softness <= 0.02f,
            DrawingTool.Airbrush or DrawingTool.Crayon or DrawingTool.Pencil => false,
            _ => !stroke.UseOpacityPressure
        };
    }

    private static void DrawOutline(
        CanvasDrawingSession session,
        DrawingStroke stroke,
        PreparedStroke prepared)
    {
        if (stroke.Points.Count == 1)
        {
            BrushSample sample = prepared.Samples[0];
            session.FillCircle(
                sample.Position,
                sample.Radius,
                WithOpacity(stroke.Color, GetToolOpacity(stroke)));
            return;
        }

        CanvasGeometry outline = prepared.Outline
            ?? StrokeOutline.Create(session.Device, stroke, prepared.Samples);
        try
        {
            session.FillGeometry(outline, WithOpacity(stroke.Color, GetToolOpacity(stroke)));
        }
        finally
        {
            if (prepared.Outline is null)
            {
                outline.Dispose();
            }
        }
    }

    private static void DrawWithOpacityMask(
        CanvasDrawingSession session,
        DrawingStroke stroke,
        PreparedStroke prepared)
    {
        IReadOnlyList<BrushSample> samples = prepared.Stamps;
        if (samples.Count == 0)
        {
            return;
        }

        using CanvasCommandList mask = new(session.Device);
        using (CanvasDrawingSession maskSession = mask.CreateDrawingSession())
        {
            maskSession.Blend = CanvasBlend.Copy;
            maskSession.Antialiasing = CanvasAntialiasing.Antialiased;
            foreach (BrushSample sample in samples)
            {
                float alpha = stroke.UseOpacityPressure
                    ? MathF.Max(0.12f, sample.Pressure)
                    : 1f;
                maskSession.FillCircle(sample.Position, sample.Radius, WithOpacity(stroke.Color, alpha));
            }
        }

        DrawMask(session, mask, GetToolOpacity(stroke), CanvasComposite.SourceOver);
    }

    private static void DrawPencil(
        CanvasDrawingSession session,
        DrawingStroke stroke,
        PreparedStroke prepared)
    {
        IReadOnlyList<BrushSample> samples = prepared.Stamps;
        if (samples.Count == 0)
        {
            return;
        }

        using CanvasCommandList mask = new(session.Device);
        using (CanvasDrawingSession maskSession = mask.CreateDrawingSession())
        {
            maskSession.Blend = CanvasBlend.Copy;
            maskSession.Antialiasing = CanvasAntialiasing.Antialiased;
            foreach (BrushSample sample in samples)
            {
                float alpha = stroke.UseOpacityPressure
                    ? MathF.Max(0.12f, sample.Pressure)
                    : 1f;
                maskSession.FillCircle(
                    sample.Position,
                    sample.Radius,
                    WithOpacity(stroke.Color, alpha));
            }

            maskSession.Blend = CanvasBlend.Copy;
            Random random = new(stroke.TextureSeed);
            foreach (BrushSample sample in samples)
            {
                float radius = MathF.Max(0.5f, sample.Radius);
                int pores = Math.Clamp((int)MathF.Ceiling(radius * 0.35f), 1, 4);
                for (int pore = 0; pore < pores; pore++)
                {
                    float angle = (float)(random.NextDouble() * Math.Tau);
                    float distance = radius * MathF.Sqrt((float)random.NextDouble());
                    Vector2 position = sample.Position +
                        (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance);
                    float poreRadius = MathF.Max(
                        0.35f,
                        radius * (0.08f + ((float)random.NextDouble() * 0.12f)));
                    maskSession.FillCircle(position, poreRadius, Microsoft.UI.Colors.Transparent);
                }
            }
        }

        DrawMask(session, mask, GetToolOpacity(stroke), CanvasComposite.SourceOver);
    }

    private static void DrawAirbrush(
        CanvasDrawingSession session,
        DrawingStroke stroke,
        PreparedStroke prepared)
    {
        IReadOnlyList<BrushSample> stamps = prepared.Stamps;
        if (stamps.Count == 0)
        {
            return;
        }

        float spacing = MathF.Max(
            0.6f,
            stroke.Size * (stroke.AirbrushMode == AirbrushMode.Grain ? 0.07f : 0.09f));
        if (stroke.AirbrushMode == AirbrushMode.Mist)
        {
            CanvasRenderTarget tip = GetTip(session.Device, stroke.Softness, stroke.Color);
            Rect source = new(0, 0, TipSize, TipSize);
            foreach (BrushSample stamp in stamps)
            {
                float radius = MathF.Max(0.4f, stamp.Radius);
                float overlap = MathF.Max(1f, (2f * radius) / spacing);
                float pressureOpacity = stroke.UseOpacityPressure
                    ? MathF.Max(0.08f, stamp.Pressure)
                    : 1f;
                float target = Math.Clamp(stroke.Opacity * stroke.Flow * pressureOpacity, 0, 1);
                float alpha = 1f - MathF.Pow(1f - target, 1f / overlap);
                alpha = Math.Clamp(alpha, 0.004f, 0.6f);
                Rect destination = new(
                    stamp.Position.X - radius,
                    stamp.Position.Y - radius,
                    radius * 2,
                    radius * 2);
                session.DrawImage(tip, destination, source, alpha, CanvasImageInterpolation.Linear);
            }

            return;
        }

        Random random = new(stroke.TextureSeed);
        foreach (BrushSample stamp in stamps)
        {
            float radius = MathF.Max(0.4f, stamp.Radius);
            int dots = Math.Clamp((int)MathF.Ceiling(radius * 0.5f), 2, 8);
            float pressureOpacity = stroke.UseOpacityPressure
                ? MathF.Max(0.08f, stamp.Pressure)
                : 1f;
            for (int dot = 0; dot < dots; dot++)
            {
                float angle = (float)(random.NextDouble() * Math.Tau);
                float distance = radius * 0.85f * MathF.Sqrt((float)random.NextDouble());
                Vector2 position = stamp.Position + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance);
                float dotRadius = MathF.Max(0.45f, radius * (0.035f + ((float)random.NextDouble() * 0.07f)));
                float alpha = stroke.Opacity * stroke.Flow * pressureOpacity *
                    (0.18f + ((float)random.NextDouble() * 0.22f));
                session.FillCircle(position, dotRadius, WithOpacity(stroke.Color, alpha));
            }
        }
    }

    private static void DrawCrayon(
        CanvasDrawingSession session,
        DrawingStroke stroke,
        PreparedStroke prepared)
    {
        IReadOnlyList<BrushSample> stamps = prepared.Stamps;
        if (stamps.Count == 0)
        {
            return;
        }

        CanvasRenderTarget tip = GetTip(session.Device, MathF.Max(0.5f, stroke.Softness), stroke.Color);
        Rect source = new(0, 0, TipSize, TipSize);
        foreach (BrushSample stamp in stamps)
        {
            float radius = MathF.Max(0.5f, stamp.Radius);
            float pressureOpacity = stroke.UseOpacityPressure
                ? MathF.Max(0.1f, stamp.Pressure)
                : 1f;
            float alpha = Math.Clamp(
                stroke.Opacity * stroke.Flow * 0.18f * pressureOpacity,
                0.01f,
                0.3f);
            Rect destination = new(
                stamp.Position.X - radius,
                stamp.Position.Y - radius,
                radius * 2,
                radius * 2);
            session.DrawImage(tip, destination, source, alpha, CanvasImageInterpolation.Linear);
        }

        Random random = new(stroke.TextureSeed);
        foreach (BrushSample stamp in stamps)
        {
            float radius = MathF.Max(0.5f, stamp.Radius);
            float pressureOpacity = stroke.UseOpacityPressure
                ? MathF.Max(0.1f, stamp.Pressure)
                : 1f;
            int grains = Math.Clamp((int)MathF.Ceiling(radius * 0.8f), 2, 10);
            for (int grain = 0; grain < grains; grain++)
            {
                float angle = (float)(random.NextDouble() * Math.Tau);
                float distance = radius * 0.8f * MathF.Sqrt((float)random.NextDouble());
                Vector2 position = stamp.Position + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance);
                float grainRadius = MathF.Max(0.4f, radius * (0.04f + ((float)random.NextDouble() * 0.05f)));
                float alpha = stroke.Opacity * stroke.Flow * pressureOpacity *
                    (0.2f + ((float)random.NextDouble() * 0.25f));
                session.FillCircle(position, grainRadius, WithOpacity(stroke.Color, alpha));
            }
        }
    }

    private static void DrawEraser(
        CanvasDrawingSession session,
        DrawingStroke stroke,
        PreparedStroke prepared)
    {
        IReadOnlyList<BrushSample> samples = prepared.Samples;
        if (samples.Count == 0)
        {
            return;
        }

        using CanvasCommandList mask = new(session.Device);
        using (CanvasDrawingSession maskSession = mask.CreateDrawingSession())
        {
            maskSession.Antialiasing = CanvasAntialiasing.Antialiased;
            if (stroke.Softness <= 0.02f)
            {
                DrawHardEraserMask(maskSession, stroke, prepared);
            }
            else
            {
                DrawSoftEraserMask(maskSession, stroke, prepared);
            }
        }

        DrawMask(session, mask, 1f, CanvasComposite.DestinationOut);
    }

    private static void DrawHardEraserMask(
        CanvasDrawingSession maskSession,
        DrawingStroke stroke,
        PreparedStroke prepared)
    {
        if (prepared.Samples.Count == 1)
        {
            maskSession.FillCircle(
                prepared.Samples[0].Position,
                prepared.Samples[0].Radius,
                Microsoft.UI.Colors.White);
            return;
        }

        CanvasGeometry outline = prepared.Outline
            ?? StrokeOutline.Create(maskSession.Device, stroke, prepared.Samples);
        try
        {
            maskSession.FillGeometry(outline, Microsoft.UI.Colors.White);
        }
        finally
        {
            if (prepared.Outline is null)
            {
                outline.Dispose();
            }
        }
    }

    private static void DrawSoftEraserMask(
        CanvasDrawingSession maskSession,
        DrawingStroke stroke,
        PreparedStroke prepared)
    {
        CanvasRenderTarget tip = GetTip(maskSession.Device, stroke.Softness, Microsoft.UI.Colors.White);
        Rect source = new(0, 0, TipSize, TipSize);
        foreach (BrushSample stamp in prepared.Stamps)
        {
            float radius = MathF.Max(0.4f, stamp.Radius);
            float alpha = stroke.UseOpacityPressure
                ? MathF.Max(0.1f, stamp.Pressure)
                : 1f;
            Rect destination = new(
                stamp.Position.X - radius,
                stamp.Position.Y - radius,
                radius * 2,
                radius * 2);
            maskSession.DrawImage(tip, destination, source, alpha, CanvasImageInterpolation.Linear);
        }
    }

    private static void DrawMask(
        CanvasDrawingSession session,
        ICanvasImage mask,
        float opacity,
        CanvasComposite composite)
    {
        Rect bounds = mask.GetBounds(session.Device);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        session.DrawImage(mask, bounds, bounds, opacity, CanvasImageInterpolation.Linear, composite);
    }

    private static CanvasRenderTarget GetTip(CanvasDevice device, float softness, Color color)
    {
        int bucket = Math.Clamp((int)MathF.Round(softness * 4f), 0, 4);
        (int Bucket, uint Color) key = (bucket, PackColor(color));
        lock (TipLock)
        {
            DeviceTips deviceTips = GetOrAddDeviceTips(device);
            if (deviceTips.Tips.TryGetValue(key, out CanvasRenderTarget? existing))
            {
                return existing;
            }

            CanvasRenderTarget created = CreateTip(device, bucket / 4f, color);
            deviceTips.Tips[key] = created;
            return created;
        }
    }

    private static DeviceTips GetOrAddDeviceTips(CanvasDevice device)
    {
        int index = FindDeviceTips(device);
        if (index >= 0)
        {
            return TipCaches[index];
        }

        DeviceTips created = new(device);
        TipCaches.Add(created);
        return created;
    }

    private static int FindDeviceTips(CanvasDevice device)
    {
        for (int index = 0; index < TipCaches.Count; index++)
        {
            if (TipCaches[index].Device.Equals(device))
            {
                return index;
            }
        }

        return -1;
    }

    private static CanvasRenderTarget CreateTip(CanvasDevice device, float softness, Color color)
    {
        CanvasRenderTarget tip = new(device, TipSize, TipSize, 96);
        using (CanvasDrawingSession session = tip.CreateDrawingSession())
        {
            session.Clear(Microsoft.UI.Colors.Transparent);
            session.Blend = CanvasBlend.Copy;
            const int steps = 64;
            float center = TipSize / 2f;
            float exponent = 0.2f + (softness * 1.6f);
            for (int index = 0; index < steps; index++)
            {
                float t = 1f - ((float)index / (steps - 1));
                float radius = center * t;
                if (radius < 0.5f)
                {
                    continue;
                }

                float alpha = MathF.Pow(1f - t, exponent);
                session.FillCircle(center, center, radius, WithOpacity(color, alpha));
            }
        }

        return tip;
    }

    private static uint PackColor(Color color) =>
        ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

    private static float GetToolOpacity(DrawingStroke stroke) => stroke.Tool switch
    {
        DrawingTool.Highlighter => MathF.Min(stroke.Opacity, 0.35f),
        DrawingTool.Pencil => MathF.Min(stroke.Opacity, 0.85f),
        _ => stroke.Opacity
    };

    private static Color WithOpacity(Color color, float opacity) =>
        Color.FromArgb((byte)Math.Clamp(color.A * opacity, 0, 255), color.R, color.G, color.B);

    private sealed class DeviceTips : IDisposable
    {
        public DeviceTips(CanvasDevice device) => Device = device;

        public CanvasDevice Device { get; }

        public Dictionary<(int Bucket, uint Color), CanvasRenderTarget> Tips { get; } = [];

        public void Dispose()
        {
            foreach (CanvasRenderTarget tip in Tips.Values)
            {
                tip.Dispose();
            }

            Tips.Clear();
        }
    }
}
