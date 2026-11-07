using System.Numerics;
using Com.StudentTools.MyDraw.Models;
using Com.StudentTools.MyDraw.Rendering;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;
using Xunit;

namespace Com.StudentTools.MyDraw.Tests;

public sealed class LayerRenderCacheTests
{
    [Fact]
    public async Task UndoLastStroke_KeepsOtherStrokesInSameTile()
    {
        await Task.Run(() =>
        {
            const int canvasSize = 600;
            CanvasDevice device = CanvasDevice.GetSharedDevice();
            DrawingStroke first = CreateHorizontalStroke(30, 40);
            DrawingStroke second = CreateHorizontalStroke(380, 400);
            using LayerRenderCache cache = new();

            cache.Synchronize(device, [first, second], canvasSize, canvasSize, 96);
            cache.Synchronize(device, [first], canvasSize, canvasSize, 96);

            Color[] pixels = Render(cache, device, canvasSize);

            Assert.True(GetAlpha(pixels, canvasSize, 45, 40) > 0,
                "The first stroke in the same tile disappeared after undo.");
            Assert.Equal(0, GetAlpha(pixels, canvasSize, 400, 400));
        });
    }

    private static DrawingStroke CreateHorizontalStroke(float startX, float y)
    {
        DrawingStroke stroke = new(
            DrawingTool.Pen,
            Color.FromArgb(255, 0, 0, 0),
            12,
            1,
            true,
            false);
        stroke.Points.Add(new StrokePoint(new Vector2(startX, y), 1, 0, 0));
        stroke.Points.Add(new StrokePoint(new Vector2(startX + 40, y), 1, 0, 0));
        return stroke;
    }

    private static Color[] Render(LayerRenderCache cache, CanvasDevice device, int canvasSize)
    {
        using CanvasRenderTarget target = new(device, canvasSize, canvasSize, 96);
        using (CanvasDrawingSession session = target.CreateDrawingSession())
        {
            session.Clear(Microsoft.UI.Colors.Transparent);
            cache.Draw(session, new Rect(0, 0, canvasSize, canvasSize));
        }

        return target.GetPixelColors();
    }

    private static byte GetAlpha(Color[] pixels, int canvasSize, int x, int y) =>
        pixels[(y * canvasSize) + x].A;
}
