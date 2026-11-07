using System.Text.Json;
using System.Text.Json.Serialization;
using Com.StudentTools.MyDraw.Models;
using Microsoft.Extensions.Logging;

namespace Com.StudentTools.MyDraw.Services;

public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogger<JsonSettingsService> _logger;
    private readonly object _stateLock = new();
    private readonly string _settingsPath;
    private string? _pendingJson;
    private Timer? _saveTimer;

    public JsonSettingsService(ILogger<JsonSettingsService> logger)
    {
        _logger = logger;
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyDraw");
        _settingsPath = Path.Combine(directory, "settings.json");
        Settings = Load();
    }

    public AppSettings Settings { get; }

    public void Save()
    {
        lock (_stateLock)
        {
            _saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _pendingJson = null;
            WriteAtomic(SerializeSettings());
        }
    }

    public void SaveDebounced()
    {
        string json = SerializeSettings();
        lock (_stateLock)
        {
            _pendingJson = json;
            _saveTimer ??= new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
            _saveTimer.Change(300, Timeout.Infinite);
        }
    }

    public void Flush()
    {
        lock (_stateLock)
        {
            _saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            string? json = _pendingJson;
            _pendingJson = null;
            if (json is not null)
            {
                WriteAtomic(json);
            }
        }
    }

    private string SerializeSettings() =>
        JsonSerializer.Serialize(Settings, SerializerOptions);

    private void WriteAtomic(string json)
    {
        string temporaryPath = $"{_settingsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            string? directory = Path.GetDirectoryName(_settingsPath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _settingsPath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Unable to save application settings.");
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(exception, "Unable to remove temporary settings file.");
                }
            }
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(exception, "Unable to load application settings; defaults will be used.");
            return new AppSettings();
        }
    }
}
