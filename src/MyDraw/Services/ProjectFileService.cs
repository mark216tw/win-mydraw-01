using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using Com.StudentTools.MyDraw.Models;
using Windows.UI;

namespace Com.StudentTools.MyDraw.Services;

public sealed class ProjectFileService : IProjectFileService
{
    private const int CurrentFormatVersion = 6;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public Task SaveAsync(
        DrawingDocument document,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ProjectData snapshot = CreateSnapshot(document);
        return Task.Run(() => SaveSnapshot(snapshot, filePath, cancellationToken), cancellationToken);
    }

    public Task<ProjectLoadResult> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => LoadSnapshot(filePath, cancellationToken), cancellationToken);

    private static ProjectData CreateSnapshot(DrawingDocument document)
    {
        List<StrokeData> strokes = document.Strokes.Select(stroke => new StrokeData(
            stroke.LayerId,
            stroke.BrushId,
            stroke.Tool.ToString(),
            PackColor(stroke.Color),
            stroke.Size,
            stroke.Opacity,
            stroke.UseSizePressure,
            stroke.UseOpacityPressure,
            stroke.TextureSeed,
            stroke.Softness,
            stroke.AirbrushMode,
            stroke.Flow,
            stroke.Points.Select(point => new PointData(
                point.Position.X,
                point.Position.Y,
                point.Pressure,
                point.TiltX,
                point.TiltY)).ToList())).ToList();

        return new ProjectData(document.Canvas.Width, document.Canvas.Height,
            document.Layers.Select(layer => new LayerData(layer.Id, layer.Name, layer.IsVisible, layer.IsLocked, layer.Opacity)).ToList(),
            strokes);
    }

    private static void SaveSnapshot(ProjectData snapshot, string filePath, CancellationToken cancellationToken)
    {
        string temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (FileStream stream = File.Create(temporaryPath))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create))
            {
                string version = typeof(ProjectFileService).Assembly.GetName().Version?.ToString() ?? string.Empty;
                WriteJson(archive, "manifest.json", new ManifestData("MyDraw", CurrentFormatVersion, version), cancellationToken);
                WriteJson(archive, "document.json", snapshot, cancellationToken);
            }

            File.Move(temporaryPath, filePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static ProjectLoadResult LoadSnapshot(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using FileStream stream = File.OpenRead(filePath);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read);

        ManifestData manifest = ReadJson<ManifestData>(archive, "manifest.json", cancellationToken)
            ?? throw new InvalidDataException("The project manifest is missing or invalid.");
        if (!string.Equals(manifest.Format, "MyDraw", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The file is not a MyDraw project.");
        }

        if (manifest.FormatVersion < 1 || manifest.FormatVersion > CurrentFormatVersion)
        {
            throw new NotSupportedException(
                $"Project format version {manifest.FormatVersion} is not supported.");
        }

        ProjectData document = ReadJson<ProjectData>(archive, "document.json", cancellationToken)
            ?? throw new InvalidDataException("The project document is missing or invalid.");
        if (document.Width is < 64 or > 8192 || document.Height is < 64 or > 8192)
        {
            throw new InvalidDataException("The project canvas size is invalid.");
        }

        if (document.Layers is null || document.Strokes is null)
        {
            throw new InvalidDataException("The project layers or strokes are missing.");
        }

        List<DrawingLayer> layers = LoadLayers(document.Layers);
        HashSet<Guid> layerIds = layers.Select(layer => layer.Id).ToHashSet();
        List<DrawingStroke> strokes = LoadStrokes(document.Strokes, layerIds);
        return new ProjectLoadResult(document.Width, document.Height, layers, strokes);
    }

    private static List<DrawingLayer> LoadLayers(IReadOnlyList<LayerData> source)
    {
        List<DrawingLayer> layers = [];
        HashSet<Guid> ids = [];
        foreach (LayerData data in source)
        {
            Guid id = data.Id == Guid.Empty ? Guid.NewGuid() : data.Id;
            if (!ids.Add(id))
            {
                continue;
            }

            layers.Add(new DrawingLayer(id, string.IsNullOrWhiteSpace(data.Name) ? "Layer" : data.Name)
            {
                IsVisible = data.IsVisible,
                IsLocked = data.IsLocked,
                Opacity = Math.Clamp(data.Opacity, 0, 1)
            });
        }

        if (layers.Count == 0)
        {
            throw new InvalidDataException("The project does not contain any layers.");
        }

        return layers;
    }

    private static List<DrawingStroke> LoadStrokes(
        IReadOnlyList<StrokeData> source,
        HashSet<Guid> layerIds)
    {
        List<DrawingStroke> strokes = [];
        foreach (StrokeData data in source)
        {
            if (!layerIds.Contains(data.LayerId) ||
                !Enum.TryParse(data.Tool, true, out DrawingTool tool) ||
                tool == DrawingTool.Pan)
            {
                continue;
            }

            DrawingStroke stroke = new(
                tool,
                FromArgb(data.Color),
                Math.Clamp(data.Size, 1, 80),
                Math.Clamp(data.Opacity, 0.05f, 1),
                data.UseSizePressure,
                data.UseOpacityPressure,
                string.IsNullOrWhiteSpace(data.BrushId) ? null : data.BrushId,
                data.TextureSeed,
                Math.Clamp(data.Softness, 0, 1),
                data.AirbrushMode,
                Math.Clamp(data.Flow ?? 1f, 0.05f, 1))
            {
                LayerId = data.LayerId
            };

            if (data.Points is null)
            {
                continue;
            }

            foreach (PointData point in data.Points)
            {
                stroke.Points.Add(new StrokePoint(
                    new Vector2(point.X, point.Y),
                    Math.Clamp(point.Pressure, 0, 1),
                    point.TiltX,
                    point.TiltY));
            }

            strokes.Add(stroke);
        }

        return strokes;
    }

    private static T? ReadJson<T>(
        ZipArchive archive,
        string entryName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ZipArchiveEntry? entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            throw new InvalidDataException($"The project is missing {entryName}.");
        }

        using Stream entryStream = entry.Open();
        return JsonSerializer.Deserialize<T>(entryStream, SerializerOptions);
    }

    private static void WriteJson<T>(
        ZipArchive archive,
        string entryName,
        T value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using Stream entryStream = entry.Open();
        JsonSerializer.Serialize(entryStream, value, SerializerOptions);
    }

    private static uint PackColor(Color color) =>
        ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

    private static Color FromArgb(uint argb) => Color.FromArgb(
        (byte)(argb >> 24),
        (byte)(argb >> 16),
        (byte)(argb >> 8),
        (byte)argb);

    private sealed record ManifestData(string Format, int FormatVersion, string AppVersion);

    private sealed record ProjectData(int Width, int Height, IReadOnlyList<LayerData> Layers, IReadOnlyList<StrokeData> Strokes);

    private sealed record LayerData(Guid Id, string Name, bool IsVisible, bool IsLocked, double Opacity);

    private sealed record StrokeData(
        Guid LayerId,
        string BrushId,
        string Tool,
        uint Color,
        float Size,
        float Opacity,
        bool UseSizePressure,
        bool UseOpacityPressure,
        int TextureSeed,
        float Softness,
        AirbrushMode AirbrushMode,
        float? Flow,
        IReadOnlyList<PointData> Points);

    private sealed record PointData(float X, float Y, float Pressure, float TiltX, float TiltY);
}
