using System.Numerics;
using Com.StudentTools.MyDraw.Models;
using Com.StudentTools.MyDraw.Services;
using Windows.UI;
using Xunit;

namespace Com.StudentTools.MyDraw.Tests;

public sealed class DrawingDocumentTests
{
    [Fact]
    public void EndStroke_AddsStrokeAndSupportsUndoRedo()
    {
        DrawingDocument document = new();
        document.AddLayer("Layer 1");
        DrawingStroke stroke = CreateStroke(document.SelectedLayer!.Id);

        document.BeginStroke(stroke);
        document.AddPoint(new StrokePoint(new Vector2(10, 10), 1, 0, 0));
        document.EndStroke();

        DrawingStroke stored = Assert.Single(document.Strokes);
        Assert.Same(stroke, stored);
        Assert.True(document.CanUndo);
        Assert.False(document.CanRedo);

        document.Undo();
        Assert.Empty(document.Strokes);
        Assert.True(document.CanRedo);

        document.Redo();
        Assert.Single(document.Strokes);
        Assert.True(document.CanUndo);
    }

    [Fact]
    public void DeleteSelectedLayer_RemovesStrokesAndClearsHistory()
    {
        DrawingDocument document = new();
        document.AddLayer("Layer 1");
        document.AddLayer("Layer 2");
        DrawingStroke stroke = CreateStroke(document.SelectedLayer!.Id);
        document.BeginStroke(stroke);
        document.AddPoint(new StrokePoint(Vector2.Zero, 1, 0, 0));
        document.EndStroke();

        document.DeleteSelectedLayer();

        Assert.Single(document.Layers);
        Assert.Empty(document.Strokes);
        Assert.False(document.CanUndo);
        Assert.False(document.CanRedo);
    }

    [Fact]
    public void Load_ReplacesDocumentAndClearsModifiedState()
    {
        DrawingDocument document = new();
        document.AddLayer("Old layer");
        DrawingStroke oldStroke = CreateStroke(document.SelectedLayer!.Id);
        document.BeginStroke(oldStroke);
        document.AddPoint(new StrokePoint(Vector2.Zero, 1, 0, 0));
        document.EndStroke();

        DrawingLayer layer = new("Loaded layer");
        DrawingStroke stroke = CreateStroke(layer.Id);
        stroke.Points.Add(new StrokePoint(new Vector2(5, 5), 1, 0, 0));

        document.Load(800, 600, [layer], [stroke], "C:\\example.mydraw");

        Assert.Equal(800, document.Canvas.Width);
        Assert.Equal(600, document.Canvas.Height);
        Assert.Same(layer, Assert.Single(document.Layers));
        Assert.Same(stroke, Assert.Single(document.Strokes));
        Assert.Same(layer, document.SelectedLayer);
        Assert.Equal("C:\\example.mydraw", document.FilePath);
        Assert.False(document.IsModified);
    }

    private static DrawingStroke CreateStroke(Guid layerId) => new(
        DrawingTool.Pen,
        Color.FromArgb(255, 0, 0, 0),
        8,
        1,
        true,
        false)
    {
        LayerId = layerId
    };
}
