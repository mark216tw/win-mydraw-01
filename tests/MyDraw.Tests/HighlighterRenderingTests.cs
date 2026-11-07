using System.Numerics;
using Com.StudentTools.MyDraw.Helpers;
using Com.StudentTools.MyDraw.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Windows.UI;
using Xunit;

namespace Com.StudentTools.MyDraw.Tests;

public sealed class HighlighterRenderingTests
{
    [Fact]
    public void Highlighter_DrawnInOrder_ProducesYellowPixels()
    {
        CanvasDevice device = CanvasDevice.GetSharedDevice();
        DrawingStroke stroke = new(
            DrawingTool.Highlighter,
            Color.FromArgb(255, 255, 230, 0),
            16,
            0.35f,
            false,
            false);
        stroke.Points.Add(new StrokePoint(new Vector2(8, 32), 1, 0, 0));
        stroke.Points.Add(new StrokePoint(new Vector2(24, 32), 1, 0, 0));
        stroke.Points.Add(new StrokePoint(new Vector2(40, 32), 1, 0, 0));
        stroke.Points.Add(new StrokePoint(new Vector2(56, 32), 1, 0, 0));

        using CanvasRenderTarget target = new(device, 64, 64, 96);
        using (CanvasDrawingSession session = target.CreateDrawingSession())
        {
            session.Clear(Colors.Transparent);
            BrushRenderer.Draw(session, stroke);
        }

        Color[] pixels = target.GetPixelColors();
        Color center = pixels[(32 * 64) + 32];
        Assert.True(center.A > 0);
        Assert.True(center.R > center.B);
        Assert.True(center.G > center.B);
    }
}

