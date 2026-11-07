using System.Numerics;
using Com.StudentTools.MyDraw.Models;
using Com.StudentTools.MyDraw.Services;
using Windows.UI;
using Xunit;

namespace Com.StudentTools.MyDraw.Tests;

public sealed class ProjectFileServiceTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsDocument()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mydraw-{Guid.NewGuid():N}.mydraw");
        try
        {
            DrawingDocument document = new();
            document.AddLayer("Background");
            DrawingLayer layer = document.SelectedLayer!;
            DrawingStroke stroke = new(
                DrawingTool.Pencil,
                Color.FromArgb(255, 10, 20, 30),
                8,
                0.75f,
                true,
                true,
                "builtin.pencil",
                 42,
                 0.3f,
                 AirbrushMode.Grain,
                 0.42f)
            {
                LayerId = layer.Id
            };
            stroke.Points.Add(new StrokePoint(new Vector2(1, 2), 0.5f, 0.1f, 0.2f));
            stroke.Points.Add(new StrokePoint(new Vector2(3, 4), 0.6f, 0.3f, 0.4f));
            document.BeginStroke(stroke);
            document.EndStroke();

            ProjectFileService service = new();
            await service.SaveAsync(document, path);

            ProjectLoadResult loaded = await service.LoadAsync(path);

            Assert.Equal(document.Canvas.Width, loaded.Width);
            Assert.Equal(document.Canvas.Height, loaded.Height);
            DrawingLayer loadedLayer = Assert.Single(loaded.Layers);
            Assert.Equal(layer.Id, loadedLayer.Id);
            Assert.Equal("Background", loadedLayer.Name);

            DrawingStroke loadedStroke = Assert.Single(loaded.Strokes);
            Assert.Equal(DrawingTool.Pencil, loadedStroke.Tool);
            Assert.Equal(stroke.Color, loadedStroke.Color);
            Assert.Equal(stroke.Size, loadedStroke.Size);
            Assert.Equal(stroke.Opacity, loadedStroke.Opacity);
            Assert.Equal(stroke.TextureSeed, loadedStroke.TextureSeed);
            Assert.Equal(stroke.Softness, loadedStroke.Softness);
            Assert.Equal(stroke.AirbrushMode, loadedStroke.AirbrushMode);
            Assert.Equal(0.42f, loadedStroke.Flow);
            Assert.Equal(2, loadedStroke.Points.Count);
            Assert.Equal(new Vector2(1, 2), loadedStroke.Points[0].Position);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_CreatesBackupAndIncludesCurrentStroke()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mydraw-{Guid.NewGuid():N}.mydraw");
        try
        {
            DrawingDocument document = new();
            document.AddLayer("Background");
            DrawingLayer layer = document.SelectedLayer!;
            ProjectFileService service = new();
            await service.SaveAsync(document, path);

            DrawingStroke currentStroke = new(
                DrawingTool.Pen,
                Color.FromArgb(255, 10, 20, 30),
                6,
                1,
                true,
                false)
            {
                LayerId = layer.Id
            };
            currentStroke.Points.Add(new StrokePoint(new Vector2(10, 10), 1, 0, 0));
            document.BeginStroke(currentStroke);

            await service.SaveAsync(document, path, currentStroke);

            Assert.True(File.Exists($"{path}.bak"));
            ProjectLoadResult current = await service.LoadAsync(path);
            Assert.Single(current.Strokes);
            ProjectLoadResult backup = await service.LoadAsync($"{path}.bak");
            Assert.Empty(backup.Strokes);
        }
        finally
        {
            File.Delete(path);
            if (File.Exists($"{path}.bak"))
            {
                File.Delete($"{path}.bak");
            }
        }
    }

    [Fact]
    public async Task Load_WithInvalidFile_ThrowsInvalidDataException()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mydraw-{Guid.NewGuid():N}.mydraw");
        await File.WriteAllTextAsync(path, "not a project");
        try
        {
            ProjectFileService service = new();
            await Assert.ThrowsAsync<ProjectFileException>(() => service.LoadAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

