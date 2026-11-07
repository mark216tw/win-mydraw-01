namespace Com.StudentTools.MyDraw.Models;

public sealed class CanvasDefinition
{
    public CanvasDefinition(int width = 1080, int height = 1080)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public void Resize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 64);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, 8192);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 64);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, 8192);
        Width = width;
        Height = height;
    }
}
