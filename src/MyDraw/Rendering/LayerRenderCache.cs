using System.Numerics;
using Com.StudentTools.MyDraw.Helpers;
using Com.StudentTools.MyDraw.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Windows.Foundation;
using Windows.UI;

namespace Com.StudentTools.MyDraw.Rendering;

internal sealed class LayerRenderCache
{
    private const int TileSize = 512;
    private readonly Dictionary<(int X, int Y), CanvasRenderTarget> _tiles = [];
    private readonly Dictionary<(int X, int Y), CanvasRenderTarget> _highlighterTiles = [];
    private readonly List<DrawingStroke> _strokes = [];
    private int _canvasHeight;
    private int _canvasWidth;
    private float _dpi = 96f;

    public void Synchronize(
        CanvasDevice device,
        IReadOnlyList<DrawingStroke> strokes,
        int canvasWidth,
        int canvasHeight,
        float dpi)
    {
        if (_canvasWidth != canvasWidth ||
            _canvasHeight != canvasHeight ||
            MathF.Abs(_dpi - dpi) > 0.01f ||
            _strokes.Count > strokes.Count ||
            !HasSamePrefix(strokes))
        {
            Rebuild(device, strokes, canvasWidth, canvasHeight, dpi);
            return;
        }

        if (_strokes.Count == strokes.Count)
        {
            return;
        }

        for (int index = _strokes.Count; index < strokes.Count; index++)
        {
            RenderStroke(device, strokes[index]);
            _strokes.Add(strokes[index]);
        }
    }

    public void Draw(CanvasDrawingSession drawingSession, Rect? visibleBounds)
    {
        DrawTiles(drawingSession, _tiles, visibleBounds);
        DrawTiles(drawingSession, _highlighterTiles, visibleBounds);
    }

    public void Dispose()
    {
        DisposeTiles(_tiles);
        DisposeTiles(_highlighterTiles);
        _strokes.Clear();
        _canvasWidth = 0;
        _canvasHeight = 0;
        _dpi = 96f;
    }

    private static void DrawTiles(
        CanvasDrawingSession drawingSession,
        Dictionary<(int X, int Y), CanvasRenderTarget> tiles,
        Rect? visibleBounds)
    {
        foreach (((int x, int y), CanvasRenderTarget tile) in tiles)
        {
            if (visibleBounds is { } visible &&
                !Intersects(visible, GetTileBounds(tile, x, y)))
            {
                continue;
            }

            drawingSession.DrawImage(tile, new Vector2(x * TileSize, y * TileSize));
        }
    }

    private static void DisposeTiles(
        Dictionary<(int X, int Y), CanvasRenderTarget> tiles)
    {
        foreach (CanvasRenderTarget tile in tiles.Values)
        {
            tile.Dispose();
        }

        tiles.Clear();
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

    private bool HasSamePrefix(IReadOnlyList<DrawingStroke> strokes)
    {
        for (int index = 0; index < _strokes.Count; index++)
        {
            if (!ReferenceEquals(_strokes[index], strokes[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void Rebuild(
        CanvasDevice device,
        IReadOnlyList<DrawingStroke> strokes,
        int canvasWidth,
        int canvasHeight,
        float dpi)
    {
        Dispose();
        _canvasWidth = canvasWidth;
        _canvasHeight = canvasHeight;
        _dpi = dpi;
        foreach (DrawingStroke stroke in strokes)
        {
            RenderStroke(device, stroke);
            _strokes.Add(stroke);
        }
    }

    private void RenderStroke(CanvasDevice device, DrawingStroke stroke)
    {
        if (stroke.Points.Count == 0)
        {
            return;
        }

        Rect bounds = GetStrokeBounds(stroke);
        int firstTileX = Math.Max(0, (int)Math.Floor(bounds.Left / TileSize));
        int firstTileY = Math.Max(0, (int)Math.Floor(bounds.Top / TileSize));
        int lastTileX = Math.Min((_canvasWidth - 1) / TileSize, (int)Math.Floor(bounds.Right / TileSize));
        int lastTileY = Math.Min((_canvasHeight - 1) / TileSize, (int)Math.Floor(bounds.Bottom / TileSize));
        if (firstTileX > lastTileX || firstTileY > lastTileY)
        {
            return;
        }

        using PreparedStroke prepared = BrushRenderer.Prepare(device, stroke);
        if (stroke.Tool == DrawingTool.Eraser)
        {
            RenderTiles(device, stroke, prepared, _tiles, firstTileX, firstTileY, lastTileX, lastTileY, false);
            RenderTiles(device, stroke, prepared, _highlighterTiles, firstTileX, firstTileY, lastTileX, lastTileY, false);
            return;
        }

        bool isHighlighter = stroke.Tool == DrawingTool.Highlighter;
        RenderTiles(
            device,
            stroke,
            prepared,
            isHighlighter ? _highlighterTiles : _tiles,
            firstTileX,
            firstTileY,
            lastTileX,
            lastTileY,
            isHighlighter);
    }

    private void RenderTiles(
        CanvasDevice device,
        DrawingStroke stroke,
        PreparedStroke prepared,
        Dictionary<(int X, int Y), CanvasRenderTarget> tiles,
        int firstTileX,
        int firstTileY,
        int lastTileX,
        int lastTileY,
        bool copyBlend)
    {
        for (int tileY = firstTileY; tileY <= lastTileY; tileY++)
        {
            for (int tileX = firstTileX; tileX <= lastTileX; tileX++)
            {
                CanvasRenderTarget? tile = GetOrCreateTile(
                    device,
                    tiles,
                    tileX,
                    tileY,
                    stroke.Tool != DrawingTool.Eraser);
                if (tile is null)
                {
                    continue;
                }

                using CanvasDrawingSession session = tile.CreateDrawingSession();
                session.Transform = Matrix3x2.CreateTranslation(-tileX * TileSize, -tileY * TileSize);
                session.Blend = copyBlend ? CanvasBlend.Copy : CanvasBlend.SourceOver;
                BrushRenderer.Draw(session, stroke, prepared);
            }
        }
    }

    private static Rect GetStrokeBounds(DrawingStroke stroke)
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        foreach (StrokePoint point in stroke.Points)
        {
            minX = MathF.Min(minX, point.Position.X);
            minY = MathF.Min(minY, point.Position.Y);
            maxX = MathF.Max(maxX, point.Position.X);
            maxY = MathF.Max(maxY, point.Position.Y);
        }

        float padding = Math.Max(2, stroke.Size);
        return new Rect(
            minX - padding,
            minY - padding,
            (maxX - minX) + (padding * 2),
            (maxY - minY) + (padding * 2));
    }

    private CanvasRenderTarget? GetOrCreateTile(
        CanvasDevice device,
        Dictionary<(int X, int Y), CanvasRenderTarget> tiles,
        int x,
        int y,
        bool create)
    {
        if (tiles.TryGetValue((x, y), out CanvasRenderTarget? tile) || !create)
        {
            return tile;
        }

        int width = Math.Min(TileSize, _canvasWidth - (x * TileSize));
        int height = Math.Min(TileSize, _canvasHeight - (y * TileSize));
        tile = new CanvasRenderTarget(device, width, height, _dpi);
        using (CanvasDrawingSession session = tile.CreateDrawingSession())
        {
            session.Clear(Colors.Transparent);
        }

        tiles.Add((x, y), tile);
        return tile;
    }
}
