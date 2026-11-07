using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Com.StudentTools.MyDraw.Models;
using Com.StudentTools.MyDraw.Services;
using System.Collections.ObjectModel;
using Windows.UI;

namespace Com.StudentTools.MyDraw.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DrawingDocument _document;
    private readonly BrushCatalog _brushCatalog;
    private readonly ISettingsService _settingsService;
    private readonly Dictionary<DrawingTool, BrushToolSettings> _toolSettings = [];
    private float _brushFlow = 1;
    private float _brushOpacity = 1;
    private float _brushSize = 8;
    private float _brushSoftness;
    private bool _isGridVisible;
    private PressureCurve _pressureCurve = PressureCurve.Linear;
    private AirbrushMode _selectedAirbrushMode = AirbrushMode.Mist;
    private Color _selectedColor = Color.FromArgb(255, 31, 41, 55);
    private DrawingTool _selectedTool = DrawingTool.Ballpoint;
    private bool _useOpacityPressure;
    private bool _useSizePressure = true;
    private float _zoom = 0.5f;

    public DrawingTool SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (SetProperty(ref _selectedTool, value))
            {
                OnPropertyChanged(nameof(CanSaveCurrentBrush));
            }
        }
    }

    public PressureCurve PressureCurve
    {
        get => _pressureCurve;
        set => SetProperty(ref _pressureCurve, value);
    }

    public float BrushSize
    {
        get => _brushSize;
        set
        {
            if (SetProperty(ref _brushSize, value))
            {
                OnPropertyChanged(nameof(BrushSizeText));
                OnPropertyChanged(nameof(BrushPreviewSize));
            }
        }
    }

    public float BrushOpacity
    {
        get => _brushOpacity;
        set
        {
            if (SetProperty(ref _brushOpacity, value))
            {
                OnPropertyChanged(nameof(BrushOpacityText));
            }
        }
    }

    public float BrushSoftness
    {
        get => _brushSoftness;
        set
        {
            if (SetProperty(ref _brushSoftness, Math.Clamp(value, 0, 1)))
            {
                OnPropertyChanged(nameof(BrushSoftnessText));
            }
        }
    }

    public float BrushFlow
    {
        get => _brushFlow;
        set
        {
            if (SetProperty(ref _brushFlow, Math.Clamp(value, 0.05f, 1)))
            {
                OnPropertyChanged(nameof(BrushFlowText));
            }
        }
    }

    public AirbrushMode SelectedAirbrushMode
    {
        get => _selectedAirbrushMode;
        set => SetProperty(ref _selectedAirbrushMode, value);
    }

    public float Zoom
    {
        get => _zoom;
        set
        {
            if (SetProperty(ref _zoom, value))
            {
                OnPropertyChanged(nameof(ZoomText));
            }
        }
    }

    public bool IsGridVisible
    {
        get => _isGridVisible;
        set => SetProperty(ref _isGridVisible, value);
    }

    public bool UseSizePressure
    {
        get => _useSizePressure;
        set => SetProperty(ref _useSizePressure, value);
    }

    public bool UseOpacityPressure
    {
        get => _useOpacityPressure;
        set => SetProperty(ref _useOpacityPressure, value);
    }

    public Color SelectedColor
    {
        get => _selectedColor;
        set => SetProperty(ref _selectedColor, value);
    }

    public MainViewModel(DrawingDocument document, BrushCatalog brushCatalog, ISettingsService settingsService)
    {
        _document = document;
        _brushCatalog = brushCatalog;
        _settingsService = settingsService;
        LoadToolSettings();
        LoadFavoriteBrushes();
        _document.Changed += Document_Changed;
    }

    public ObservableCollection<FavoriteBrush> FavoriteBrushes { get; } = [];

    public bool CanSaveCurrentBrush => IsFavoriteTool(SelectedTool);

    public string ZoomText => $"{Zoom * 100:0}%";

    public string CanvasSizeText => $"{_document.Canvas.Width} × {_document.Canvas.Height}";

    public string BrushSizeText => $"{BrushSize:0}";

    public string BrushOpacityText => $"{BrushOpacity * 100:0}%";

    public string BrushSoftnessText => $"{BrushSoftness * 100:0}%";

    public string BrushFlowText => $"{BrushFlow * 100:0}%";

    public double BrushPreviewSize => Math.Clamp((BrushSize * 0.28) + 4, 4, 18);

    public string LayerCountText => $"{_document.Layers.Count}";

    public bool IsModified => _document.IsModified;

    public void SelectTool(DrawingTool tool)
    {
        if (tool == _selectedTool)
        {
            return;
        }

        RememberCurrentToolSettings();
        SelectedTool = tool;
        ApplyToolSettings(tool);
        PersistToolSettingsToAppSettings();
        _settingsService.SaveDebounced();
    }

    public void PersistToolSettings()
    {
        RememberCurrentToolSettings();
        PersistToolSettingsToAppSettings();
    }

    public BrushToolSettings GetToolSettings(DrawingTool tool)
    {
        if (_toolSettings.TryGetValue(tool, out BrushToolSettings? settings))
        {
            return settings;
        }

        BrushPreset preset = _brushCatalog.GetPreset(tool);
        return new BrushToolSettings
        {
            Tool = tool,
            Size = preset.DefaultSize,
            Opacity = preset.DefaultOpacity,
            UseSizePressure = preset.UseSizePressure,
            UseOpacityPressure = preset.UseOpacityPressure,
            Softness = preset.DefaultSoftness,
            AirbrushMode = preset.DefaultAirbrushMode,
            PressureCurve = PressureCurve.Linear,
            Flow = preset.DefaultFlow
        };
    }

    public FavoriteBrush SaveFavoriteBrush(string name)
    {
        if (!CanSaveCurrentBrush)
        {
            throw new InvalidOperationException("The selected tool cannot be saved as a favorite brush.");
        }

        FavoriteBrush favorite = new()
        {
            Name = NormalizeFavoriteName(name),
            Tool = SelectedTool,
            ColorArgb = ToArgb(SelectedColor),
            Size = BrushSize,
            Opacity = BrushOpacity,
            UseSizePressure = UseSizePressure,
            UseOpacityPressure = UseOpacityPressure,
            Softness = BrushSoftness,
            AirbrushMode = SelectedAirbrushMode,
            PressureCurve = PressureCurve,
            Flow = BrushFlow
        };
        FavoriteBrushes.Add(favorite);
        PersistFavoriteBrushes();
        return favorite;
    }

    public void ApplyFavoriteBrush(FavoriteBrush favorite)
    {
        ArgumentNullException.ThrowIfNull(favorite);
        if (!IsFavoriteTool(favorite.Tool))
        {
            return;
        }

        SelectTool(favorite.Tool);
        SelectedColor = FromArgb(favorite.ColorArgb);
        BrushSize = Math.Clamp(favorite.Size, 1, 80);
        BrushOpacity = Math.Clamp(favorite.Opacity, 0.05f, 1);
        UseSizePressure = favorite.UseSizePressure;
        UseOpacityPressure = favorite.UseOpacityPressure;
        BrushSoftness = Math.Clamp(favorite.Softness, 0, 1);
        SelectedAirbrushMode = favorite.AirbrushMode;
        PressureCurve = favorite.PressureCurve;
        BrushFlow = Math.Clamp(favorite.Flow, 0.05f, 1);
        RememberCurrentToolSettings();
        PersistToolSettingsToAppSettings();
        _settingsService.SaveDebounced();
    }

    public void RenameFavoriteBrush(Guid id, string name)
    {
        int index = FindFavoriteIndex(id);
        if (index < 0)
        {
            return;
        }

        FavoriteBrush favorite = CloneFavorite(FavoriteBrushes[index]);
        favorite.Name = NormalizeFavoriteName(name);
        FavoriteBrushes[index] = favorite;
        PersistFavoriteBrushes();
    }

    public void DeleteFavoriteBrush(Guid id)
    {
        int index = FindFavoriteIndex(id);
        if (index < 0)
        {
            return;
        }

        FavoriteBrushes.RemoveAt(index);
        PersistFavoriteBrushes();
    }

    public IReadOnlyList<string> RecentProjects => _settingsService.Settings.RecentProjects;

    public void AddRecentProject(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        List<string> recentProjects = _settingsService.Settings.RecentProjects;
        recentProjects.RemoveAll(path =>
            string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase));
        recentProjects.Insert(0, filePath);
        if (recentProjects.Count > 10)
        {
            recentProjects.RemoveRange(10, recentProjects.Count - 10);
        }

        _settingsService.Save();
    }

    public void ClearRecentProjects()
    {
        _settingsService.Settings.RecentProjects.Clear();
        _settingsService.Save();
    }

    public void PruneMissingRecentProjects()
    {
        List<string> recentProjects = _settingsService.Settings.RecentProjects;
        int removed = recentProjects.RemoveAll(path =>
            string.IsNullOrWhiteSpace(path) || !File.Exists(path));
        if (removed > 0)
        {
            _settingsService.Save();
        }
    }

    public void MoveFavoriteBrush(Guid id, int direction)
    {
        if (direction is not (-1 or 1))
        {
            return;
        }

        int index = FindFavoriteIndex(id);
        int destination = index + direction;
        if (index < 0 || destination < 0 || destination >= FavoriteBrushes.Count)
        {
            return;
        }

        FavoriteBrushes.Move(index, destination);
        PersistFavoriteBrushes();
    }

    private void LoadToolSettings()
    {
        foreach (BrushPreset preset in _brushCatalog.Presets)
        {
            _toolSettings[preset.Tool] = new BrushToolSettings
            {
                Tool = preset.Tool,
                Size = preset.DefaultSize,
                Opacity = preset.DefaultOpacity,
                UseSizePressure = preset.UseSizePressure,
                UseOpacityPressure = preset.UseOpacityPressure,
                Softness = preset.DefaultSoftness,
                AirbrushMode = preset.DefaultAirbrushMode,
                PressureCurve = PressureCurve.Linear,
                Flow = preset.DefaultFlow
            };
        }

        bool restoreExtendedSettings = _settingsService.Settings.SettingsVersion >= 1;
        foreach (BrushToolSettings saved in _settingsService.Settings.BrushToolSettings)
        {
            if (!_toolSettings.TryGetValue(saved.Tool, out BrushToolSettings? settings))
            {
                continue;
            }

            settings.Size = Math.Clamp(saved.Size, 1, 80);
            settings.Opacity = Math.Clamp(saved.Opacity, 0.05f, 1);
            settings.UseSizePressure = saved.UseSizePressure;
            settings.UseOpacityPressure = saved.UseOpacityPressure;
            settings.Flow = Math.Clamp(saved.Flow, 0.05f, 1);
            if (restoreExtendedSettings)
            {
                settings.Softness = Math.Clamp(saved.Softness, 0, 1);
                settings.AirbrushMode = saved.AirbrushMode;
                settings.PressureCurve = saved.PressureCurve;
            }
            else if (saved.Tool == DrawingTool.Airbrush &&
                MathF.Abs(saved.Size - 36) < 0.01f &&
                MathF.Abs(saved.Opacity - 0.2f) < 0.01f)
            {
                BrushPreset preset = _brushCatalog.GetPreset(saved.Tool);
                settings.Size = preset.DefaultSize;
                settings.Opacity = preset.DefaultOpacity;
                settings.UseSizePressure = preset.UseSizePressure;
                settings.UseOpacityPressure = preset.UseOpacityPressure;
            }
            else if (saved.Tool == DrawingTool.Eraser &&
                MathF.Abs(saved.Size - 24) < 0.01f &&
                saved.UseSizePressure)
            {
                settings.UseSizePressure = false;
            }
        }

        ApplyToolSettings(_selectedTool);
    }

    private void LoadFavoriteBrushes()
    {
        foreach (FavoriteBrush saved in _settingsService.Settings.FavoriteBrushes)
        {
            if (!IsFavoriteTool(saved.Tool) || string.IsNullOrWhiteSpace(saved.Name))
            {
                continue;
            }

            FavoriteBrush favorite = CloneFavorite(saved);
            favorite.Id = favorite.Id == Guid.Empty ? Guid.NewGuid() : favorite.Id;
            favorite.Name = NormalizeFavoriteName(favorite.Name);
            favorite.Size = Math.Clamp(favorite.Size, 1, 80);
            favorite.Opacity = Math.Clamp(favorite.Opacity, 0.05f, 1);
            favorite.Softness = Math.Clamp(favorite.Softness, 0, 1);
            favorite.Flow = Math.Clamp(favorite.Flow, 0.05f, 1);
            FavoriteBrushes.Add(favorite);
        }
    }

    private void ApplyToolSettings(DrawingTool tool)
    {
        if (!_toolSettings.TryGetValue(tool, out BrushToolSettings? settings))
        {
            return;
        }

        BrushSize = settings.Size;
        BrushOpacity = settings.Opacity;
        UseSizePressure = settings.UseSizePressure;
        UseOpacityPressure = settings.UseOpacityPressure;
        BrushSoftness = settings.Softness;
        SelectedAirbrushMode = settings.AirbrushMode;
        PressureCurve = settings.PressureCurve;
        BrushFlow = settings.Flow;
    }

    private void RememberCurrentToolSettings()
    {
        if (!_toolSettings.TryGetValue(_selectedTool, out BrushToolSettings? settings))
        {
            return;
        }

        settings.Size = BrushSize;
        settings.Opacity = BrushOpacity;
        settings.UseSizePressure = UseSizePressure;
        settings.UseOpacityPressure = UseOpacityPressure;
        settings.Softness = BrushSoftness;
        settings.AirbrushMode = SelectedAirbrushMode;
        settings.PressureCurve = PressureCurve;
        settings.Flow = BrushFlow;
    }

    private void PersistToolSettingsToAppSettings()
    {
        _settingsService.Settings.SettingsVersion = 3;
        _settingsService.Settings.BrushToolSettings = _toolSettings.Values
            .Select(settings => new BrushToolSettings
            {
                Tool = settings.Tool,
                Size = settings.Size,
                Opacity = settings.Opacity,
                UseSizePressure = settings.UseSizePressure,
                UseOpacityPressure = settings.UseOpacityPressure,
                Softness = settings.Softness,
                AirbrushMode = settings.AirbrushMode,
                PressureCurve = settings.PressureCurve,
                Flow = settings.Flow
            })
            .ToList();
    }

    private void PersistFavoriteBrushes()
    {
        _settingsService.Settings.FavoriteBrushes = FavoriteBrushes.Select(CloneFavorite).ToList();
        _settingsService.Save();
    }

    private int FindFavoriteIndex(Guid id)
    {
        for (int index = 0; index < FavoriteBrushes.Count; index++)
        {
            if (FavoriteBrushes[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static FavoriteBrush CloneFavorite(FavoriteBrush favorite) => new()
    {
        Id = favorite.Id,
        Name = favorite.Name,
        Tool = favorite.Tool,
        ColorArgb = favorite.ColorArgb,
        Size = favorite.Size,
        Opacity = favorite.Opacity,
        UseSizePressure = favorite.UseSizePressure,
        UseOpacityPressure = favorite.UseOpacityPressure,
        Softness = favorite.Softness,
        AirbrushMode = favorite.AirbrushMode,
        PressureCurve = favorite.PressureCurve,
        Flow = favorite.Flow
    };

    private static string NormalizeFavoriteName(string name)
    {
        string normalized = name.Trim();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("A favorite brush name is required.", nameof(name));
        }

        return normalized.Length <= 40 ? normalized : normalized[..40];
    }

    private static bool IsFavoriteTool(DrawingTool tool) => tool is
        DrawingTool.Pen or
        DrawingTool.Pencil or
        DrawingTool.Ballpoint or
        DrawingTool.Marker or
        DrawingTool.Highlighter or
        DrawingTool.Calligraphy or
        DrawingTool.Airbrush or
        DrawingTool.Crayon;

    private static uint ToArgb(Color color) =>
        ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

    private static Color FromArgb(uint argb) => Color.FromArgb(
        (byte)(argb >> 24),
        (byte)(argb >> 16),
        (byte)(argb >> 8),
        (byte)argb);

    public float ApplyPressureCurve(float pressure) =>
        ApplyPressureCurve(pressure, PressureCurve);

    public static float ApplyPressureCurve(float pressure, PressureCurve curve) => curve switch
    {
        PressureCurve.Soft => MathF.Sqrt(pressure),
        PressureCurve.Hard => pressure * pressure,
        _ => pressure
    };

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _document.Undo();

    private bool CanUndo() => _document.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _document.Redo();

    private bool CanRedo() => _document.CanRedo;

    private void Document_Changed(object? sender, EventArgs e)
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanvasSizeText));
        OnPropertyChanged(nameof(LayerCountText));
        OnPropertyChanged(nameof(IsModified));
    }
}
