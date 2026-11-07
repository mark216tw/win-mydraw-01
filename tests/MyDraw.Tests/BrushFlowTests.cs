using System.Numerics;
using Com.StudentTools.MyDraw.Helpers;
using Com.StudentTools.MyDraw.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Windows.UI;
using Xunit;

namespace Com.StudentTools.MyDraw.Tests;

public sealed class BrushFlowTests
{
    [Fact]
    public void AirbrushFlow_ChangesStrokeDensity()
    {
        byte lowFlowAlpha = RenderAirbrushCenterAlpha(0.2f);
        byte highFlowAlpha = RenderAirbrushCenterAlpha(1f);

        Assert.True(highFlowAlpha > lowFlowAlpha);
    }

    private static byte RenderAirbrushCenterAlpha(float flow)
    {
        CanvasDevice device = CanvasDevice.GetSharedDevice();
        DrawingStroke stroke = new(
            DrawingTool.Airbrush,
            Color.FromArgb(255, 20, 40, 60),
            20,
            1,
            false,
            false,
            textureSeed: 1,
            softness: 0.5f,
            flow: flow);
        stroke.Points.Add(new StrokePoint(new Vector2(8, 32), 1, 0, 0));
        stroke.Points.Add(new StrokePoint(new Vector2(32, 32), 1, 0, 0));
        stroke.Points.Add(new StrokePoint(new Vector2(56, 32), 1, 0, 0));

        using CanvasRenderTarget target = new(device, 64, 64, 96);
        using (CanvasDrawingSession session = target.CreateDrawingSession())
        {
            session.Clear(Colors.Transparent);
            BrushRenderer.Draw(session, stroke);
        }

        return target.GetPixelColors()[(32 * 64) + 32].A;
    }
}
