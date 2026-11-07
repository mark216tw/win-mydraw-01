using System.Numerics;
using Com.StudentTools.MyDraw.Helpers;
using Com.StudentTools.MyDraw.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Windows.Foundation;
using Windows.UI;

namespace Com.StudentTools.MyDraw.Rendering;

internal sealed class LiveStrokeCache : IDisposable
{
    private const int TileSize = 512;
    private readonly Dictionary<(int X, int Y), CanvasRenderTarget> _tiles = [];
    private CanvasDevice? _device;
    private float _dpi = 96f;
    private int _canvasHeight;
    private int _canvasWidth;
    private int _pointCount;
    private PreparedStroke? _prepared;
    private DrawingStroke? _stroke;

    public void Update(
        CanvasDevice device,
        DrawingStroke stroke,
        int canvasWidth,
        int canvasHeight,
        float dpi)
    {
        if (stroke.Points.Count == 0)
        {
            Reset();
            return;
        }

        bool needsRebuild =
            !ReferenceEquals(_stroke, stroke) ||
            _device is null ||
            !_device.Equals(device) ||
            _canvasWidth != canvasWidth ||
            _canvasHeight != canvasHeight ||
            MathF.Abs(_dpi - dpi) > 0.01f;
        if (needsRebuild)
        {
            ResetTiles();
            _stroke = stroke;
            _device = device;
            _canvasWidth = canvasWidth;
            _canvasHeight = canvasHeight;
            _dpi = dpi;
            _pointCount = stroke.Points.Count;
            _prepared?.Dispose();
            _prepared = BrushRenderer.Prepare(device, stroke);
            RenderTiles(stroke, 0);
            return;
        }

        if (_pointCount == stroke.Points.Count)
        {
            return;
        }

        int startIndex = Math.Max(0, _pointCount - 2);
        _pointCount = stroke.Points.Count;
        _prepared?.Dispose();
        _prepared = BrushRenderer.Prepare(device, stroke);
        RenderTiles(stroke, startIndex);
    }

    public void Draw(CanvasDrawingSession drawingSession, Rect? visibleBounds)
    {
        foreach (((int x, int y), CanvasRenderTarget tile) in _tiles)
        {
            if (visibleBounds is { } visible &&
                !Intersects(visible, GetTileBounds(tile, x, y)))
            {
                continue;
            }

            drawingSession.DrawImage(tile, new Vector2(x * TileSize, y * TileSize));
        }
    }

    public void Reset()
    {
        ResetTiles();
        _prepared?.Dispose();
        _prepared = null;
        _stroke = null;
        _device = null;
        _pointCount = 0;
        _canvasWidth = 0;
        _canvasHeight = 0;
        _dpi = 96f;
    }

    public void Dispose() => Reset();

    private void ResetTiles()
    {
        foreach (CanvasRenderTarget tile in _tiles.Values)
        {
            tile.Dispose();
        }

        _tiles.Clear();
    }

    private void RenderTiles(DrawingStroke stroke, int startIndex)
    {
        if (_device is null || _prepared is null)
        {
            return;
        }

        Rect bounds = GetBounds(stroke, startIndex);
        int firstTileX = Math.Max(0, (int)Math.Floor(bounds.Left / TileSize));
        int firstTileY = Math.Max(0, (int)Math.Floor(bounds.Top / TileSize));
        int lastTileX = Math.Min((_canvasWidth - 1) / TileSize, (int)Math.Floor(bounds.Right / TileSize));
        int lastTileY = Math.Min((_canvasHeight - 1) / TileSize, (int)Math.Floor(bounds.Bottom / TileSize));
        if (firstTileX > lastTileX || firstTileY > lastTileY)
        {
            return;
        }

        bool isHighlighter = stroke.Tool == DrawingTool.Highlighter;
        for (int tileY = firstTileY; tileY <= lastTileY; tileY++)
        {
            for (int tileX = firstTileX; tileX <= lastTileX; tileX++)
            {
                CanvasRenderTarget tile = GetOrCreateTile(tileX, tileY);
                using CanvasDrawingSession session = tile.CreateDrawingSession();
                session.Clear(Colors.Transparent);
                session.Transform = Matrix3x2.CreateTranslation(-tileX * TileSize, -tileY * TileSize);
                session.Blend = isHighlighter ? CanvasBlend.Copy : CanvasBlend.SourceOver;
                BrushRenderer.Draw(session, stroke, _prepared);
            }
        }
    }

    private static Rect GetBounds(DrawingStroke stroke, int startIndex)
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        for (int index = startIndex; index < stroke.Points.Count; index++)
        {
            Vector2 position = stroke.Points[index].Position;
            minX = MathF.Min(minX, position.X);
            minY = MathF.Min(minY, position.Y);
            maxX = MathF.Max(maxX, position.X);
            maxY = MathF.Max(maxY, position.Y);
        }

        float padding = Math.Max(2, stroke.Size);
        return new Rect(
            minX - padding,
            minY - padding,
            (maxX - minX) + (padding * 2),
            (maxY - minY) + (padding * 2));
    }

    private CanvasRenderTarget GetOrCreateTile(int x, int y)
    {
        if (_tiles.TryGetValue((x, y), out CanvasRenderTarget? tile))
        {
            return tile;
        }

        int width = Math.Min(TileSize, _canvasWidth - (x * TileSize));
        int height = Math.Min(TileSize, _canvasHeight - (y * TileSize));
        tile = new CanvasRenderTarget(
            _device ?? throw new InvalidOperationException("Live stroke device is not available."),
            width,
            height,
            _dpi);
        using (CanvasDrawingSession session = tile.CreateDrawingSession())
        {
            session.Clear(Colors.Transparent);
        }

        _tiles.Add((x, y), tile);
        return tile;
    }

    private static bool Intersects(Rect left, Rect right) =>
        left.Left < right.Right &&
        left.Right > right.Left &&
        left.Top < right.Bottom &&
        left.Bottom > right.Top;

    private static Rect GetTileBounds(CanvasRenderTarget tile, int x, int y)
    {
        float scale = 96f / tile.Dpi;
        return new Rect(
            x * TileSize,
            y * TileSize,
            tile.SizeInPixels.Width * scale,
            tile.SizeInPixels.Height * scale);
    }
}
