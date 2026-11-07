namespace Com.StudentTools.MyDraw.Models;

public sealed class BrushToolSettings
{
    public DrawingTool Tool { get; set; }

    public float Size { get; set; }

    public float Opacity { get; set; }

    public bool UseSizePressure { get; set; }

    public bool UseOpacityPressure { get; set; }

    public float Softness { get; set; }

    public AirbrushMode AirbrushMode { get; set; }

    public PressureCurve PressureCurve { get; set; } = PressureCurve.Linear;

    public float Flow { get; set; } = 1;
}
