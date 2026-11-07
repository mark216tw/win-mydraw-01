using System.Text.Json;
using Com.StudentTools.MyDraw.Models;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Com.StudentTools.MyDraw.Services;

public sealed class RecoveryService : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILogger<RecoveryService> _logger;
    private readonly IProjectFileService _projectFileService;
    private readonly string _recoveryRoot;
    private Func<DrawingStroke?>? _currentStrokeProvider;
    private DrawingDocument? _document;
    private string? _metadataPath;
    private bool _saving;
    private Guid _sessionId;
    private string? _sessionPath;
    private DispatcherQueueTimer? _timer;

    public RecoveryService(
        IProjectFileService projectFileService,
        ILogger<RecoveryService> logger)
    {
        _projectFileService = projectFileService;
        _logger = logger;
        _recoveryRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyDraw",
            "Recovery");
    }

    public void Start(
        DrawingDocument document,
        DispatcherQueue dispatcherQueue,
        Func<DrawingStroke?> currentStrokeProvider)
    {
        Stop();
        _document = document;
        _currentStrokeProvider = currentStrokeProvider;
        _sessionId = Guid.NewGuid();
        _sessionPath = Path.Combine(_recoveryRoot, $"{_sessionId:N}.mydraw");
        _metadataPath = Path.Combine(_recoveryRoot, $"{_sessionId:N}.json");
        _timer = dispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(30);
        _timer.IsRepeating = true;
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    public void Stop()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _timer = null;
        }

        _document = null;
        _currentStrokeProvider = null;
    }

    public void Clear()
    {
        Stop();
        DeleteFiles(_sessionPath, _metadataPath);
        _sessionPath = null;
        _metadataPath = null;
    }

    public RecoveryInfo? GetLatestRecovery()
    {
        try
        {
            if (!Directory.Exists(_recoveryRoot))
            {
                return null;
            }

            RecoveryInfo? latest = null;
            foreach (string metadataPath in Directory.EnumerateFiles(_recoveryRoot, "*.json"))
            {
                try
                {
                    RecoveryMetadata? metadata = JsonSerializer.Deserialize<RecoveryMetadata>(
                        File.ReadAllText(metadataPath),
                        SerializerOptions);
                    if (metadata is null)
                    {
                        continue;
                    }

                    string recoveryPath = Path.Combine(_recoveryRoot, $"{metadata.SessionId:N}.mydraw");
                    if (!File.Exists(recoveryPath))
                    {
                        continue;
                    }

                    RecoveryInfo candidate = new(
                        metadata.SessionId,
                        recoveryPath,
                        metadataPath,
                        metadata.OriginalPath,
                        metadata.SavedAt);
                    if (latest is null || candidate.SavedAt > latest.SavedAt)
                    {
                        latest = candidate;
                    }
                }
                catch (Exception exception) when (exception is IOException or JsonException)
                {
                    _logger.LogWarning(exception, "Unable to read recovery metadata.");
                }
            }

            return latest;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Unable to inspect recovery files.");
            return null;
        }
    }

    public void Delete(RecoveryInfo recovery)
    {
        DeleteFiles(recovery.RecoveryPath, recovery.MetadataPath);
    }

    public void CleanupExpired()
    {
        DateTimeOffset cutoff = DateTimeOffset.Now.AddDays(-7);
        try
        {
            if (!Directory.Exists(_recoveryRoot))
            {
                return;
            }

            foreach (string metadataPath in Directory.EnumerateFiles(_recoveryRoot, "*.json"))
            {
                try
                {
                    RecoveryMetadata? metadata = JsonSerializer.Deserialize<RecoveryMetadata>(
                        File.ReadAllText(metadataPath),
                        SerializerOptions);
                    if (metadata is null || metadata.SavedAt >= cutoff)
                    {
                        continue;
                    }

                    DeleteFiles(
                        Path.Combine(_recoveryRoot, $"{metadata.SessionId:N}.mydraw"),
                        metadataPath);
                }
                catch (Exception exception) when (exception is IOException or JsonException)
                {
                    _logger.LogWarning(exception, "Unable to clean up recovery metadata.");
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Unable to clean up recovery files.");
        }
    }

    public async Task SaveRecoveryAsync()
    {
        if (_saving ||
            _document is null ||
            !_document.IsModified ||
            _sessionPath is null ||
            _metadataPath is null)
        {
            return;
        }

        _saving = true;
        try
        {
            Directory.CreateDirectory(_recoveryRoot);
            await _projectFileService.SaveAsync(
                _document,
                _sessionPath,
                _currentStrokeProvider?.Invoke(),
                createBackup: false);
            await WriteMetadataAsync(new RecoveryMetadata(
                _sessionId,
                _document.FilePath,
                DateTimeOffset.Now));
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _logger.LogWarning(exception, "Unable to write recovery snapshot.");
        }
        finally
        {
            _saving = false;
        }
    }

    public void Dispose() => Stop();

    private async void Timer_Tick(DispatcherQueueTimer sender, object args) =>
        await SaveRecoveryAsync();

    private async Task WriteMetadataAsync(RecoveryMetadata metadata)
    {
        if (_metadataPath is null)
        {
            return;
        }

        Directory.CreateDirectory(_recoveryRoot);
        string temporaryPath = $"{_metadataPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(metadata, SerializerOptions));
            File.Move(temporaryPath, _metadataPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void DeleteFiles(string? recoveryPath, string? metadataPath)
    {
        if (recoveryPath is not null && File.Exists(recoveryPath))
        {
            try
            {
                File.Delete(recoveryPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        if (metadataPath is not null && File.Exists(metadataPath))
        {
            try
            {
                File.Delete(metadataPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed record RecoveryMetadata(
        Guid SessionId,
        string? OriginalPath,
        DateTimeOffset SavedAt);
}
