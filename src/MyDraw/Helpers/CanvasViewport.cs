using System.Numerics;
using Windows.Foundation;

namespace Com.StudentTools.MyDraw.Helpers;

public sealed class CanvasViewport
{
    private Vector2 _offset = new(32, 32);

    public Vector2 Offset
    {
        get => _offset;
        set => _offset = value;
    }

    public Matrix3x2 GetTransform(float zoom) =>
        Matrix3x2.CreateScale(zoom) * Matrix3x2.CreateTranslation(_offset);

    public Vector2 ScreenToCanvas(Vector2 position, float zoom)
    {
        Matrix3x2.Invert(GetTransform(zoom), out Matrix3x2 inverse);
        return Vector2.Transform(position, inverse);
    }

    public void Pan(Vector2 delta) => _offset += delta;

    public void SetOffsetForZoom(Vector2 screenPosition, Vector2 canvasPosition, float zoom) =>
        _offset = screenPosition - (canvasPosition * zoom);

    public void Fit(float zoom, Vector2 viewportSize, Vector2 canvasSize) =>
        _offset = new Vector2(
            (viewportSize.X - (canvasSize.X * zoom)) / 2,
            (viewportSize.Y - (canvasSize.Y * zoom)) / 2);

    public void Reset(float zoom, Vector2 viewportSize, Vector2 canvasSize)
    {
        float offsetX = viewportSize.X > canvasSize.X ? (viewportSize.X - canvasSize.X) / 2 : 32;
        float offsetY = viewportSize.Y > canvasSize.Y ? (viewportSize.Y - canvasSize.Y) / 2 : 32;
        _offset = new Vector2(Math.Max(24, offsetX), Math.Max(24, offsetY));
    }

    public Rect GetVisibleCanvasBounds(float zoom, Vector2 viewportSize)
    {
        Matrix3x2.Invert(GetTransform(zoom), out Matrix3x2 inverse);
        Vector2 topLeft = Vector2.Transform(Vector2.Zero, inverse);
        Vector2 bottomRight = Vector2.Transform(viewportSize, inverse);
        return new Rect(
            Math.Min(topLeft.X, bottomRight.X),
            Math.Min(topLeft.Y, bottomRight.Y),
            Math.Abs(bottomRight.X - topLeft.X),
            Math.Abs(bottomRight.Y - topLeft.Y));
    }
}
