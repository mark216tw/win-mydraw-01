namespace Com.StudentTools.MyDraw.Models;

public sealed class FavoriteBrush
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public DrawingTool Tool { get; set; }

    public uint ColorArgb { get; set; }

    public float Size { get; set; }

    public float Opacity { get; set; }

    public bool UseSizePressure { get; set; }

    public bool UseOpacityPressure { get; set; }

    public float Softness { get; set; }

    public AirbrushMode AirbrushMode { get; set; }

    public PressureCurve PressureCurve { get; set; } = PressureCurve.Linear;

    public float Flow { get; set; } = 1;
}
