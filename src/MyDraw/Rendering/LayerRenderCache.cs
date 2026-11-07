using System.Numerics;
using Com.StudentTools.MyDraw.Helpers;
using Com.StudentTools.MyDraw.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Windows.Foundation;
using Windows.UI;

namespace Com.StudentTools.MyDraw.Rendering;

internal sealed class LayerRenderCache : IDisposable
{
    private const int TileSize = 512;
    private readonly Dictionary<(int X, int Y), CanvasRenderTarget> _tiles = [];
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
        bool sizeChanged =
            _canvasWidth != canvasWidth ||
            _canvasHeight != canvasHeight ||
            MathF.Abs(_dpi - dpi) > 0.01f;
        if (sizeChanged ||
            _strokes.Count > strokes.Count + 1 ||
            !HasSamePrefix(strokes, Math.Min(_strokes.Count, strokes.Count)))
        {
            Rebuild(device, strokes, canvasWidth, canvasHeight, dpi);
            return;
        }

        if (_strokes.Count == strokes.Count)
        {
            return;
        }

        if (_strokes.Count == strokes.Count + 1)
        {
            RemoveLastStroke(device, strokes);
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

    public void Dispose()
    {
        foreach (CanvasRenderTarget tile in _tiles.Values)
        {
            tile.Dispose();
        }

        _tiles.Clear();
        _strokes.Clear();
        _canvasWidth = 0;
        _canvasHeight = 0;
        _dpi = 96f;
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

    private bool HasSamePrefix(IReadOnlyList<DrawingStroke> strokes, int count)
    {
        for (int index = 0; index < count; index++)
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

    private void RemoveLastStroke(
        CanvasDevice device,
        IReadOnlyList<DrawingStroke> strokes)
    {
        DrawingStroke removed = _strokes[^1];
        _strokes.RemoveAt(_strokes.Count - 1);

        HashSet<(int X, int Y)> dirtyTiles = GetTiles(GetStrokeBounds(removed));
        if (dirtyTiles.Count == 0)
        {
            return;
        }

        List<(DrawingStroke Stroke, HashSet<(int X, int Y)> Tiles)> remaining = [];
        foreach (DrawingStroke stroke in _strokes)
        {
            remaining.Add((stroke, GetTiles(GetStrokeBounds(stroke))));
        }

        bool expanded = true;
        while (expanded)
        {
            expanded = false;
            foreach ((DrawingStroke Stroke, HashSet<(int X, int Y)> Tiles) entry in remaining)
            {
                if (!entry.Tiles.Overlaps(dirtyTiles))
                {
                    continue;
                }

                foreach ((int X, int Y) tile in entry.Tiles)
                {
                    expanded |= dirtyTiles.Add(tile);
                }
            }
        }

        int totalTiles = GetTotalTileCount();
        if (totalTiles > 0 && dirtyTiles.Count * 4 >= totalTiles * 3)
        {
            Rebuild(device, strokes, _canvasWidth, _canvasHeight, _dpi);
            return;
        }

        foreach ((int X, int Y) tileKey in dirtyTiles)
        {
            if (_tiles.Remove(tileKey, out CanvasRenderTarget? tile))
            {
                tile.Dispose();
            }
        }

        foreach ((DrawingStroke Stroke, HashSet<(int X, int Y)> Tiles) entry in remaining)
        {
            if (entry.Tiles.Overlaps(dirtyTiles))
            {
                RenderStroke(device, entry.Stroke);
            }
        }
    }

    private HashSet<(int X, int Y)> GetTiles(Rect bounds)
    {
        HashSet<(int X, int Y)> tiles = [];
        int firstTileX = Math.Max(0, (int)Math.Floor(bounds.Left / TileSize));
        int firstTileY = Math.Max(0, (int)Math.Floor(bounds.Top / TileSize));
        int lastTileX = Math.Min((_canvasWidth - 1) / TileSize, (int)Math.Floor(bounds.Right / TileSize));
        int lastTileY = Math.Min((_canvasHeight - 1) / TileSize, (int)Math.Floor(bounds.Bottom / TileSize));
        for (int tileY = firstTileY; tileY <= lastTileY; tileY++)
        {
            for (int tileX = firstTileX; tileX <= lastTileX; tileX++)
            {
                tiles.Add((tileX, tileY));
            }
        }

        return tiles;
    }

    private int GetTotalTileCount()
    {
        int columns = Math.Max(1, (_canvasWidth + TileSize - 1) / TileSize);
        int rows = Math.Max(1, (_canvasHeight + TileSize - 1) / TileSize);
        return columns * rows;
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
        for (int tileY = firstTileY; tileY <= lastTileY; tileY++)
        {
            for (int tileX = firstTileX; tileX <= lastTileX; tileX++)
            {
                CanvasRenderTarget? tile = GetOrCreateTile(
                    device,
                    tileX,
                    tileY,
                    stroke.Tool != DrawingTool.Eraser);
                if (tile is null)
                {
                    continue;
                }

                using CanvasDrawingSession session = tile.CreateDrawingSession();
                session.Transform = Matrix3x2.CreateTranslation(-tileX * TileSize, -tileY * TileSize);
                session.Blend = CanvasBlend.SourceOver;
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
        int x,
        int y,
        bool create)
    {
        if (_tiles.TryGetValue((x, y), out CanvasRenderTarget? tile) || !create)
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

        _tiles.Add((x, y), tile);
        return tile;
    }
}
