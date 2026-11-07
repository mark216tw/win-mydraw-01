using System.Numerics;
using Com.StudentTools.MyDraw.Helpers;
using Com.StudentTools.MyDraw.Models;
using Windows.UI;
using Xunit;

namespace Com.StudentTools.MyDraw.Tests;

public sealed class StrokeSamplerTests
{
    [Fact]
    public void Sample_WithSinglePoint_ReturnsSingleSample()
    {
        DrawingStroke stroke = CreateStroke(8, new Vector2(12, 24));

        IReadOnlyList<BrushSample> samples = StrokeSampler.Sample(stroke);

        BrushSample sample = Assert.Single(samples);
        Assert.Equal(new Vector2(12, 24), sample.Position);
        Assert.True(sample.Radius > 0);
    }

    [Fact]
    public void Sample_PenStroke_TapersBothEnds()
    {
        DrawingStroke stroke = CreateStroke(
            20,
            new Vector2(0, 0),
            new Vector2(50, 0),
            new Vector2(100, 0),
            new Vector2(150, 0),
            new Vector2(200, 0));

        IReadOnlyList<BrushSample> samples = StrokeSampler.Sample(stroke);

        BrushSample middle = samples[samples.Count / 2];
        Assert.True(middle.Radius > samples[0].Radius);
        Assert.True(middle.Radius > samples[^1].Radius);
    }

    [Fact]
    public void Resample_PreservesEndpointsAndApproximateSpacing()
    {
        DrawingStroke stroke = CreateStroke(
            8,
            new Vector2(0, 0),
            new Vector2(60, 0),
            new Vector2(120, 0),
            new Vector2(180, 0));
        IReadOnlyList<BrushSample> samples = StrokeSampler.Sample(stroke);

        IReadOnlyList<BrushSample> resampled = StrokeSampler.Resample(samples, 10);

        Assert.True(resampled.Count > 2);
        Assert.Equal(samples[0].Position, resampled[0].Position);
        Assert.Equal(samples[^1].Position, resampled[^1].Position);
        for (int index = 1; index < resampled.Count - 1; index++)
        {
            float spacing = resampled[index].ArcLength - resampled[index - 1].ArcLength;
            Assert.InRange(spacing, 9.5f, 10.5f);
        }
    }

    [Fact]
    public void Sample_LargeBrush_UsesFewerAdaptiveSamples()
    {
        DrawingStroke smallBrush = CreateStroke(
            2,
            new Vector2(0, 0),
            new Vector2(100, 0),
            new Vector2(200, 0));
        DrawingStroke largeBrush = CreateStroke(
            80,
            new Vector2(0, 0),
            new Vector2(100, 0),
            new Vector2(200, 0));

        IReadOnlyList<BrushSample> smallSamples = StrokeSampler.Sample(smallBrush);
        IReadOnlyList<BrushSample> largeSamples = StrokeSampler.Sample(largeBrush);

        Assert.True(largeSamples.Count < smallSamples.Count);
    }

    private static DrawingStroke CreateStroke(float size, params Vector2[] positions)
    {
        DrawingStroke stroke = new(
            DrawingTool.Pen,
            Color.FromArgb(255, 0, 0, 0),
            size,
            1,
            true,
            false);
        foreach (Vector2 position in positions)
        {
            stroke.Points.Add(new StrokePoint(position, 1, 0, 0));
        }

        return stroke;
    }
}
