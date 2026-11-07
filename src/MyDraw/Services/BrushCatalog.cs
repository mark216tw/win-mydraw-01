using Com.StudentTools.MyDraw.Models;

namespace Com.StudentTools.MyDraw.Services;

public sealed class BrushCatalog
{
    private readonly IReadOnlyDictionary<DrawingTool, BrushPreset> _presets =
        new Dictionary<DrawingTool, BrushPreset>
        {
            [DrawingTool.Pen] = new("builtin.pen", DrawingTool.Pen, 8, 1, true, false),
            [DrawingTool.Pencil] = new("builtin.pencil", DrawingTool.Pencil, 3, 0.85f, true, true, 0.35f),
            [DrawingTool.Ballpoint] = new("builtin.ballpoint", DrawingTool.Ballpoint, 4, 1, false, false),
            [DrawingTool.Marker] = new("builtin.marker", DrawingTool.Marker, 18, 0.7f, true, false, 0.15f),
            [DrawingTool.Highlighter] = new("builtin.highlighter", DrawingTool.Highlighter, 28, 0.3f, false, false),
            [DrawingTool.Calligraphy] = new("builtin.calligraphy", DrawingTool.Calligraphy, 16, 1, true, false),
            [DrawingTool.Airbrush] = new("builtin.airbrush", DrawingTool.Airbrush, 40, 0.8f, true, true, 0.8f),
            [DrawingTool.Crayon] = new("builtin.crayon", DrawingTool.Crayon, 14, 0.85f, true, true, 0.65f),
            [DrawingTool.Eraser] = new("builtin.eraser", DrawingTool.Eraser, 24, 1, false, false, 0.35f),
            [DrawingTool.Pan] = new("tool.pan", DrawingTool.Pan, 8, 1, false, false)
        };

    public IEnumerable<BrushPreset> Presets => _presets.Values;

    public BrushPreset GetPreset(DrawingTool tool) => _presets[tool];
}
