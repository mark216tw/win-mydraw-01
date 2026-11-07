using Windows.UI;

namespace Com.StudentTools.MyDraw.Models;

public sealed class DrawingStroke
{
    public DrawingStroke(
        DrawingTool tool,
        Color color,
        float size,
        float opacity,
        bool useSizePressure,
        bool useOpacityPressure,
        string? brushId = null,
        int? textureSeed = null,
        float softness = 0,
        AirbrushMode airbrushMode = AirbrushMode.Mist,
        float flow = 1)
    {
        Tool = tool;
        Color = color;
        Size = size;
        Opacity = opacity;
        UseSizePressure = useSizePressure;
        UseOpacityPressure = useOpacityPressure;
        BrushId = brushId ?? $"builtin.{tool.ToString().ToLowerInvariant()}";
        TextureSeed = textureSeed ?? Random.Shared.Next();
        Softness = Math.Clamp(softness, 0, 1);
        AirbrushMode = airbrushMode;
        Flow = Math.Clamp(flow, 0.05f, 1);
    }

    public DrawingTool Tool { get; }

    public Guid LayerId { get; set; }

    public string BrushId { get; }

    public int TextureSeed { get; }

    public Color Color { get; }

    public float Size { get; }

    public float Opacity { get; }

    public bool UseSizePressure { get; }

    public bool UseOpacityPressure { get; }

    public float Softness { get; }

    public AirbrushMode AirbrushMode { get; }

    public float Flow { get; }

    public IList<StrokePoint> Points { get; } = new List<StrokePoint>();
}
