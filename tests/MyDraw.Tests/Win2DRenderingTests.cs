using System.Numerics;
using Com.StudentTools.MyDraw.Helpers;
using Com.StudentTools.MyDraw.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Windows.UI;
using Xunit;

namespace Com.StudentTools.MyDraw.Tests;

public sealed class Win2DRenderingTests
{
    [Fact]
    public async Task BackgroundRender_CanSavePngWithPreparedStroke()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mydraw-{Guid.NewGuid():N}.png");
        try
        {
            await Task.Run(async () =>
            {
                CanvasDevice device = CanvasDevice.GetSharedDevice();
                DrawingStroke stroke = new(
                    DrawingTool.Pen,
                    Color.FromArgb(255, 20, 40, 60),
                    12,
                    1,
                    true,
                    false);
                stroke.Points.Add(new StrokePoint(new Vector2(16, 32), 0.5f, 0, 0));
                stroke.Points.Add(new StrokePoint(new Vector2(48, 32), 1f, 0, 0));

                using CanvasRenderTarget target = new(device, 64, 64, 96);
                using (CanvasDrawingSession session = target.CreateDrawingSession())
                {
                    session.Clear(Colors.White);
                    using PreparedStroke prepared = BrushRenderer.Prepare(device, stroke);
                    BrushRenderer.Draw(session, stroke, prepared);
                }

                await target.SaveAsync(path, CanvasBitmapFileFormat.Png);
            });

            FileInfo file = new(path);
            Assert.True(file.Exists);
            Assert.True(file.Length > 0);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
