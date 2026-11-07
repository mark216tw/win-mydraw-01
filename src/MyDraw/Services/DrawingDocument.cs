using Com.StudentTools.MyDraw.Models;
using System.Collections.ObjectModel;

namespace Com.StudentTools.MyDraw.Services;

public sealed class DrawingDocument
{
    private readonly List<DrawingStroke> _strokes = [];
    private readonly Stack<HistoryEntry> _undo = new();
    private readonly Stack<HistoryEntry> _redo = new();

    public ObservableCollection<DrawingLayer> Layers { get; } = [];

    public DrawingLayer? SelectedLayer { get; set; }

    public void AddLayer(string name, bool markModified = true)
    {
        DrawingLayer layer = new(name);
        AttachLayer(layer);
        Layers.Insert(0, layer);
        SelectedLayer = layer;
        if (markModified) Touch();
    }

    public void Load(
        int width,
        int height,
        IReadOnlyList<DrawingLayer> layers,
        IReadOnlyList<DrawingStroke> strokes,
        string filePath)
    {
        if (layers.Count == 0)
        {
            throw new InvalidDataException("The project does not contain any layers.");
        }

        _strokes.Clear();
        _undo.Clear();
        _redo.Clear();
        CurrentStroke = null;
        Layers.Clear();
        Canvas.Resize(width, height);

        foreach (DrawingLayer layer in layers)
        {
            AttachLayer(layer);
            Layers.Add(layer);
        }

        _strokes.AddRange(strokes);
        SelectedLayer = Layers[0];
        FilePath = filePath;
        IsModified = false;
        StrokeVersion++;
        OnChanged();
    }

    private void AttachLayer(DrawingLayer layer) =>
        layer.PropertyChanged += (_, _) => Touch();

    public void DeleteSelectedLayer()
    {
        if (SelectedLayer is null || Layers.Count <= 1) return;
        Guid id = SelectedLayer.Id;
        Layers.Remove(SelectedLayer);
        _strokes.RemoveAll(stroke => stroke.LayerId == id);
        _undo.Clear();
        _redo.Clear();
        SelectedLayer = Layers[0];
        StrokeVersion++;
        Touch();
    }

    public void MoveSelectedLayer(int direction)
    {
        if (SelectedLayer is null) return;
        int index = Layers.IndexOf(SelectedLayer);
        int destination = index + direction;
        if (destination < 0 || destination >= Layers.Count) return;
        Layers.Move(index, destination);
        Touch();
    }

    private void Touch()
    {
        IsModified = true;
        OnChanged();
    }

    public CanvasDefinition Canvas { get; } = new();

    public event EventHandler? Changed;

    public event EventHandler? StrokePreviewChanged;

    public IReadOnlyList<DrawingStroke> Strokes => _strokes;

    public int StrokeVersion { get; private set; }

    public DrawingStroke? CurrentStroke { get; private set; }

    public string? FilePath { get; private set; }

    public bool IsModified { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void BeginStroke(DrawingStroke stroke)
    {
        if (SelectedLayer is null || SelectedLayer.IsLocked || !SelectedLayer.IsVisible) return;
        stroke.LayerId = SelectedLayer.Id;
        CurrentStroke = stroke;
        _redo.Clear();
        IsModified = true;
        OnChanged();
    }

    public void AddPoint(StrokePoint point) => AddPoints([point]);

    public void AddPoints(IReadOnlyList<StrokePoint> points)
    {
        if (CurrentStroke is null || points.Count == 0)
        {
            return;
        }

        foreach (StrokePoint point in points)
        {
            CurrentStroke.Points.Add(point);
        }

        StrokePreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    public void EndStroke()
    {
        if (CurrentStroke is null)
        {
            return;
        }

        if (CurrentStroke.Points.Count > 0)
        {
            DrawingStroke stroke = CurrentStroke;
            _strokes.Add(stroke);
            _undo.Push(new HistoryEntry(() => _strokes.Remove(stroke), () => _strokes.Add(stroke)));
            StrokeVersion++;
        }

        CurrentStroke = null;
        OnChanged();
    }

    public void Undo()
    {
        if (!_undo.TryPop(out HistoryEntry? entry))
        {
            return;
        }

        entry.Undo();
        _redo.Push(entry);
        StrokeVersion++;
        IsModified = true;
        OnChanged();
    }

    public void Redo()
    {
        if (!_redo.TryPop(out HistoryEntry? entry))
        {
            return;
        }

        entry.Redo();
        _undo.Push(entry);
        StrokeVersion++;
        IsModified = true;
        OnChanged();
    }

    public void ResizeCanvas(int width, int height)
    {
        if (Canvas.Width == width && Canvas.Height == height)
        {
            return;
        }

        Canvas.Resize(width, height);
        IsModified = true;
        OnChanged();
    }

    public void MarkSaved(string filePath, bool clearModified = true)
    {
        FilePath = filePath;
        if (clearModified)
        {
            IsModified = false;
        }

        OnChanged();
    }

    public void Reset(string defaultLayerName, int width = 1080, int height = 1080)
    {
        _strokes.Clear();
        _undo.Clear();
        _redo.Clear();
        CurrentStroke = null;
        FilePath = null;
        Layers.Clear();
        Canvas.Resize(width, height);
        AddLayer(defaultLayerName, false);
        StrokeVersion++;
        IsModified = false;
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private sealed record HistoryEntry(Action Undo, Action Redo);
}
