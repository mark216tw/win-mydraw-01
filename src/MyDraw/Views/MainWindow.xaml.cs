using System.ComponentModel;
using System.Numerics;
using System.Reflection;
using System.Security;
using System.Text.Json;
using Com.StudentTools.MyDraw.Helpers;
using Com.StudentTools.MyDraw.Models;
using Com.StudentTools.MyDraw.Rendering;
using Com.StudentTools.MyDraw.Services;
using Com.StudentTools.MyDraw.ViewModels;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Pointer = Microsoft.UI.Xaml.Input.Pointer;

namespace Com.StudentTools.MyDraw.Views;

public sealed partial class MainWindow : Window
{
    private static readonly InputCursor PanCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    private static readonly InputCursor PanningCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
    private readonly BrushCatalog _brushCatalog;
    private readonly ILogger<MainWindow> _logger;
    private readonly Dictionary<Guid, LayerRenderCache> _layerCaches = [];
    private readonly LiveStrokeCache _liveStrokeCache = new();
    private readonly DrawingDocument _document;
    private readonly FileAssociationService _fileAssociationService;
    private readonly IProjectFileService _projectFileService;
    private readonly ResourceLoader _resourceLoader = new();
    private readonly ISettingsService _settingsService;
    private readonly ThemeService _themeService;
    private readonly CanvasViewport _viewport = new();
    private CanvasDevice? _cacheDevice;
    private Task? _activeExport;
    private float _renderDpi = 96f;
    private string _appTitle = string.Empty;
    private uint? _activePointerId;
    private bool _hasFittedCanvas;
    private bool _isCloseConfirmed;
    private bool _isDialogOpen;
    private bool _isFullScreen;
    private bool _isImmersive;
    private bool _isLoaded;
    private bool _isPanning;
    private bool _isPointerOverCanvas;
    private bool _isRestoringPlacement;
    private bool _isSpaceHeld;
    private bool _hoverIsPenEraser;
    private float _hoverTiltX;
    private float _hoverTiltY;
    private PressureCurve _activeStrokePressureCurve = PressureCurve.Linear;
    private string? _pendingProjectPath;
    private Vector2 _hoverScreenPosition;
    private Vector2 _lastPointerPosition;
    private Button? _selectedSecondaryBrushButton;
    private Button? _selectedToolButton;
    private int _syncedStrokeVersion = -1;
    private string _windowTitle = string.Empty;

    public MainWindow(
        MainViewModel viewModel,
        DrawingDocument document,
        BrushCatalog brushCatalog,
        ISettingsService settingsService,
        ThemeService themeService,
        FileAssociationService fileAssociationService,
        IProjectFileService projectFileService,
        ILogger<MainWindow> logger)
    {
        ViewModel = viewModel;
        _document = document;
        _brushCatalog = brushCatalog;
        _settingsService = settingsService;
        _themeService = themeService;
        _fileAssociationService = fileAssociationService;
        _projectFileService = projectFileService;
        _logger = logger;

        InitializeComponent();
        RootLayout.DataContext = ViewModel;
        MoreBrushesFlyoutRoot.DataContext = ViewModel;
        FavoriteBrushesFlyoutRoot.DataContext = ViewModel;
        BrushSettingsFlyoutRoot.DataContext = ViewModel;
        ColorFlyoutRoot.DataContext = ViewModel;
        LayersFlyoutRoot.DataContext = ViewModel;

        _document.AddLayer(_resourceLoader.GetString("DefaultLayerName/Text"), false);
        LayerList.ItemsSource = _document.Layers;
        LayerList.SelectedItem = _document.SelectedLayer;
        _appTitle = _resourceLoader.GetString("AppTitle");
        _windowTitle = _appTitle;
        Title = _appTitle;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "MyDraw.ico"));

        RestoreWindowPlacement();
        Activated += MainWindow_Activated;
        AppWindow.Changed += AppWindow_Changed;
        AppWindow.Closing += AppWindow_Closing;
        _document.Changed += Document_Changed;
        _document.StrokePreviewChanged += Document_StrokePreviewChanged;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        RootLayout.Loaded += RootLayout_Loaded;
        DrawingCanvas.SizeChanged += DrawingCanvas_SizeChanged;
        DrawingCanvas.PointerEntered += DrawingCanvas_PointerEntered;
        DrawingCanvas.PointerExited += DrawingCanvas_PointerExited;
        Closed += MainWindow_Closed;
        UpdateFavoriteBrushEmptyState();
    }

    public MainViewModel ViewModel { get; }

    public void QueueOpenProject(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => QueueOpenProject(filePath));
            return;
        }

        if (_isLoaded && !_isDialogOpen)
        {
            _ = OpenProjectFromPathAsync(filePath);
            return;
        }

        _pendingProjectPath = filePath;
    }

    private void ProcessPendingProject()
    {
        if (!_isLoaded || _isDialogOpen || _pendingProjectPath is not { } filePath)
        {
            return;
        }

        _pendingProjectPath = null;
        _ = OpenProjectFromPathAsync(filePath);
    }

    private Vector2 CanvasSize => new(_document.Canvas.Width, _document.Canvas.Height);

    private void DrawingCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args) =>
        RenderDocument(args.DrawingSession, GetCanvasTransform());

    private void OverlayCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        CanvasDrawingSession drawingSession = args.DrawingSession;
        drawingSession.Clear(Colors.Transparent);
        drawingSession.Transform = GetCanvasTransform();
        if (ViewModel.IsGridVisible)
        {
            DrawGrid(drawingSession);
        }

        DrawBrushCursor(drawingSession);
    }

    private void RenderDocument(CanvasDrawingSession drawingSession, Matrix3x2 transform)
    {
        drawingSession.Clear(Colors.Transparent);
        drawingSession.Transform = transform;

        using CanvasActiveLayer paperLayer = drawingSession.CreateLayer(
            1,
            new Rect(0, 0, _document.Canvas.Width, _document.Canvas.Height));
        drawingSession.FillRectangle(0, 0, CanvasSize.X, CanvasSize.Y, Colors.White);

        CanvasDevice device = drawingSession.Device;
        EnsureRenderCache(device);

        Rect visibleBounds = GetVisibleCanvasBounds();
        DrawingStroke? currentStroke = _document.CurrentStroke;
        foreach (DrawingLayer drawingLayer in _document.Layers.Reverse())
        {
            if (!drawingLayer.IsVisible || drawingLayer.Opacity <= 0) continue;
            LayerRenderCache cache = GetOrAddLayerCache(drawingLayer.Id);
            if (drawingLayer.Opacity < 1)
            {
                using CanvasActiveLayer opacityLayer = drawingSession.CreateLayer((float)drawingLayer.Opacity);
                DrawLayer(drawingSession, device, cache, drawingLayer, currentStroke, visibleBounds);
            }
            else
            {
                DrawLayer(drawingSession, device, cache, drawingLayer, currentStroke, visibleBounds);
            }
        }
    }

    private void DrawLayer(
        CanvasDrawingSession drawingSession,
        CanvasDevice device,
        LayerRenderCache cache,
        DrawingLayer drawingLayer,
        DrawingStroke? currentStroke,
        Rect visibleBounds)
    {
        if (currentStroke is not null && currentStroke.LayerId == drawingLayer.Id)
        {
            DrawActiveLayer(drawingSession, device, cache, currentStroke, visibleBounds);
            return;
        }

        cache.Draw(drawingSession, visibleBounds);
    }

    private Rect GetVisibleCanvasBounds()
    {
        float width = (float)DrawingCanvas.ActualWidth;
        float height = (float)DrawingCanvas.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return new Rect(0, 0, CanvasSize.X, CanvasSize.Y);
        }

        return _viewport.GetVisibleCanvasBounds(ViewModel.Zoom, new Vector2(width, height));
    }

    private void EnsureRenderCache(CanvasDevice device)
    {
        float dpi = GetRenderDpi();
        if (_cacheDevice is null || !_cacheDevice.Equals(device))
        {
            DisposeLayerCaches();
            _cacheDevice = device;
            _syncedStrokeVersion = -1;
        }

        if (_syncedStrokeVersion == _document.StrokeVersion &&
            MathF.Abs(_renderDpi - dpi) <= 0.01f)
        {
            return;
        }

        _syncedStrokeVersion = _document.StrokeVersion;
        _renderDpi = dpi;
        Dictionary<Guid, List<DrawingStroke>> strokesByLayer = [];
        foreach (DrawingStroke stroke in _document.Strokes)
        {
            if (!strokesByLayer.TryGetValue(stroke.LayerId, out List<DrawingStroke>? strokes))
            {
                strokes = [];
                strokesByLayer.Add(stroke.LayerId, strokes);
            }

            strokes.Add(stroke);
        }

        foreach (DrawingLayer drawingLayer in _document.Layers)
        {
            GetOrAddLayerCache(drawingLayer.Id)
                .Synchronize(
                    device,
                    strokesByLayer.GetValueOrDefault(drawingLayer.Id) ?? [],
                    _document.Canvas.Width,
                    _document.Canvas.Height,
                    dpi);
        }
    }

    private float GetRenderDpi()
    {
        long area = (long)_document.Canvas.Width * _document.Canvas.Height;
        if (area > 12_000_000)
        {
            return 96f;
        }

        return Math.Clamp((float)DrawingCanvas.Dpi, 96f, 144f);
    }

    private LayerRenderCache GetOrAddLayerCache(Guid layerId)
    {
        if (!_layerCaches.TryGetValue(layerId, out LayerRenderCache? cache))
        {
            cache = new LayerRenderCache();
            _layerCaches.Add(layerId, cache);
        }

        return cache;
    }

    private void DrawActiveLayer(
        CanvasDrawingSession drawingSession,
        CanvasDevice device,
        LayerRenderCache cache,
        DrawingStroke currentStroke,
        Rect visibleBounds)
    {
        if (currentStroke.Tool == DrawingTool.Eraser)
        {
            _liveStrokeCache.Reset();
            using CanvasCommandList combined = new(device);
            using (CanvasDrawingSession combinedSession = combined.CreateDrawingSession())
            {
                cache.Draw(combinedSession, visibleBounds);
                BrushRenderer.Draw(combinedSession, currentStroke);
            }

            drawingSession.DrawImage(combined);
            return;
        }

        cache.Draw(drawingSession, visibleBounds);
        _liveStrokeCache.Update(
            device,
            currentStroke,
            _document.Canvas.Width,
            _document.Canvas.Height,
            _renderDpi);
        _liveStrokeCache.Draw(drawingSession, visibleBounds);
    }

    private void DisposeLayerCaches()
    {
        _liveStrokeCache.Reset();
        foreach (LayerRenderCache cache in _layerCaches.Values)
        {
            cache.Dispose();
        }

        _layerCaches.Clear();
        CanvasDevice? previousDevice = _cacheDevice;
        _cacheDevice = null;
        _renderDpi = 96f;
        if (previousDevice is not null)
        {
            BrushRenderer.ResetTips(previousDevice);
        }
    }

    private void DrawGrid(CanvasDrawingSession drawingSession)
    {
        Color gridColor = Color.FromArgb(75, 71, 85, 105);
        float width = Math.Max(0.75f / ViewModel.Zoom, 0.5f);
        for (float x = 0; x <= CanvasSize.X; x += 64)
        {
            drawingSession.DrawLine(x, 0, x, CanvasSize.Y, gridColor, width);
        }

        for (float y = 0; y <= CanvasSize.Y; y += 64)
        {
            drawingSession.DrawLine(0, y, CanvasSize.X, y, gridColor, width);
        }
    }

    private void DrawBrushCursor(CanvasDrawingSession drawingSession)
    {
        if (!_isPointerOverCanvas || _isPanning || _isSpaceHeld ||
            ViewModel.SelectedTool == DrawingTool.Pan)
        {
            return;
        }

        DrawingTool cursorTool = _hoverIsPenEraser
            ? DrawingTool.Eraser
            : ViewModel.SelectedTool;
        float brushSize = cursorTool == DrawingTool.Eraser
            ? ViewModel.GetToolSettings(DrawingTool.Eraser).Size
            : ViewModel.BrushSize;
        float radius = MathF.Max(1.5f, (brushSize * ViewModel.Zoom) / 2f);
        drawingSession.Transform = Matrix3x2.Identity;
        Color outer = Color.FromArgb(180, 255, 255, 255);
        Color inner = Color.FromArgb(150, 0, 0, 0);

        if (cursorTool == DrawingTool.Calligraphy)
        {
            float nibAngle = GetNibAngle();
            drawingSession.Transform =
                Matrix3x2.CreateRotation(nibAngle) *
                Matrix3x2.CreateTranslation(_hoverScreenPosition);
            float radiusX = MathF.Max(0.75f, radius * 0.35f);
            drawingSession.DrawEllipse(Vector2.Zero, radiusX, radius, inner, 1f);
            drawingSession.DrawEllipse(
                Vector2.Zero,
                MathF.Max(0.6f, radiusX - 0.5f),
                MathF.Max(0.75f, radius - 0.5f),
                outer,
                1f);
            drawingSession.Transform = Matrix3x2.Identity;
            return;
        }

        if (cursorTool == DrawingTool.Highlighter)
        {
            drawingSession.DrawEllipse(
                _hoverScreenPosition,
                radius * 1.35f,
                radius * 0.65f,
                inner,
                1f);
            drawingSession.DrawEllipse(
                _hoverScreenPosition,
                MathF.Max(0.75f, (radius * 1.35f) - 0.5f),
                MathF.Max(0.75f, (radius * 0.65f) - 0.5f),
                outer,
                1f);
            return;
        }

        drawingSession.DrawCircle(_hoverScreenPosition, radius + 0.5f, inner, 1f);
        drawingSession.DrawCircle(_hoverScreenPosition, radius - 0.5f, outer, 1f);
    }

    private float GetNibAngle()
    {
        float magnitude = MathF.Abs(_hoverTiltX) + MathF.Abs(_hoverTiltY);
        return magnitude > 5f
            ? MathF.Atan2(_hoverTiltY, _hoverTiltX)
            : -MathF.PI / 4f;
    }

    private void UpdateHoverState(PointerPoint point)
    {
        _hoverScreenPosition = ToVector(point.Position);
        _hoverIsPenEraser = point.Properties.IsEraser;
        _hoverTiltX = point.Properties.XTilt;
        _hoverTiltY = point.Properties.YTilt;
    }

    private void DrawingCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_activePointerId is not null)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(DrawingCanvas);
        bool shouldPan = ViewModel.SelectedTool == DrawingTool.Pan ||
            _isSpaceHeld ||
            (point.PointerDeviceType == PointerDeviceType.Mouse && point.Properties.IsRightButtonPressed);
        if (!shouldPan && point.PointerDeviceType == PointerDeviceType.Mouse && !point.Properties.IsLeftButtonPressed) return;
        if (!shouldPan && _document.SelectedLayer is not { IsLocked: false, IsVisible: true }) return;
        if (!shouldPan && (!point.IsInContact || !IsInsideCanvas(ScreenToCanvas(ToVector(point.Position)))))
        {
            return;
        }

        _activePointerId = e.Pointer.PointerId;
        UpdateHoverState(point);
        _lastPointerPosition = ToVector(point.Position);
        _isPointerOverCanvas = true;
        _isPanning = shouldPan;
        UpdateCanvasCursor();
        DrawingCanvas.CapturePointer(e.Pointer);

        if (!shouldPan)
        {
            bool isPenEraser = point.Properties.IsEraser;
            DrawingTool tool = isPenEraser ? DrawingTool.Eraser : ViewModel.SelectedTool;
            BrushPreset preset = _brushCatalog.GetPreset(tool);
            BrushToolSettings toolSettings = ViewModel.GetToolSettings(tool);
            bool useEraserSettings = isPenEraser;
            _activeStrokePressureCurve = useEraserSettings
                ? toolSettings.PressureCurve
                : ViewModel.PressureCurve;
            DrawingStroke stroke = new(
                tool,
                ViewModel.SelectedColor,
                useEraserSettings ? toolSettings.Size : ViewModel.BrushSize,
                useEraserSettings ? toolSettings.Opacity : ViewModel.BrushOpacity,
                useEraserSettings ? toolSettings.UseSizePressure : ViewModel.UseSizePressure,
                useEraserSettings ? toolSettings.UseOpacityPressure : ViewModel.UseOpacityPressure,
                preset.Id,
                softness: useEraserSettings ? toolSettings.Softness : ViewModel.BrushSoftness,
                airbrushMode: useEraserSettings ? toolSettings.AirbrushMode : ViewModel.SelectedAirbrushMode,
                flow: useEraserSettings ? toolSettings.Flow : ViewModel.BrushFlow);
            _document.BeginStroke(stroke);
            AddStrokePoints(e.GetIntermediatePoints(DrawingCanvas).Reverse());
        }

        e.Handled = true;
    }

    private void DrawingCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(DrawingCanvas);
        UpdateHoverState(point);
        Vector2 screenPosition = _hoverScreenPosition;
        _isPointerOverCanvas = true;

        OverlayCanvas.Invalidate();
        if (_activePointerId != e.Pointer.PointerId)
        {
            return;
        }

        if (_isPanning)
        {
            _viewport.Pan(screenPosition - _lastPointerPosition);
            _lastPointerPosition = screenPosition;
            InvalidateCanvasAndOverlay();
        }
        else
        {
            AddStrokePoints(e.GetIntermediatePoints(DrawingCanvas).Reverse());
        }

        e.Handled = true;
    }

    private void DrawingCanvas_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverCanvas = true;
        UpdateHoverState(e.GetCurrentPoint(DrawingCanvas));
        OverlayCanvas.Invalidate();
    }

    private void DrawingCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverCanvas = false;
        _hoverIsPenEraser = false;
        OverlayCanvas.Invalidate();
    }

    private void DrawingCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_activePointerId == e.Pointer.PointerId)
        {
            if (!_isPanning) AddStrokePoints([e.GetCurrentPoint(DrawingCanvas)]);
            FinishPointerInteraction(e.Pointer);
            e.Handled = true;
        }
    }

    private void DrawingCanvas_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_activePointerId == e.Pointer.PointerId)
        {
            FinishPointerInteraction(null);
        }
    }

    private void DrawingCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(DrawingCanvas);
        Vector2 pointerPosition = ToVector(point.Position);
        Vector2 canvasPosition = ScreenToCanvas(pointerPosition);
        float zoomFactor = point.Properties.MouseWheelDelta > 0 ? 1.1f : 1 / 1.1f;
        ViewModel.Zoom = Math.Clamp(ViewModel.Zoom * zoomFactor, 0.1f, 4);
        _viewport.SetOffsetForZoom(pointerPosition, canvasPosition, ViewModel.Zoom);
        InvalidateCanvasAndOverlay();
        e.Handled = true;
    }

    private void AddStrokePoints(IEnumerable<PointerPoint> points)
    {
        if (_document.CurrentStroke is not { } stroke)
        {
            return;
        }

        List<StrokePoint> samples = [];
        StrokePoint? previous = stroke.Points.Count > 0 ? stroke.Points[^1] : null;
        foreach (PointerPoint point in points)
        {
            Vector2 canvasPosition = ScreenToCanvas(ToVector(point.Position));
            float minimumDistance = 0.75f / ViewModel.Zoom;
            if (previous is { } last &&
                Vector2.DistanceSquared(last.Position, canvasPosition) < minimumDistance * minimumDistance &&
                Math.Abs(last.Pressure - point.Properties.Pressure) < 0.02f)
            {
                continue;
            }

            float rawPressure = point.PointerDeviceType == PointerDeviceType.Pen ? point.Properties.Pressure : 1;
            StrokePoint sample = new(
                canvasPosition,
                MainViewModel.ApplyPressureCurve(
                    Math.Clamp(rawPressure, 0, 1),
                    _activeStrokePressureCurve),
                point.Properties.XTilt,
                point.Properties.YTilt);
            samples.Add(sample);
            previous = sample;
        }

        _document.AddPoints(samples);
    }

    private void FinishPointerInteraction(Pointer? pointer)
    {
        if (!_isPanning)
        {
            _document.EndStroke();
            _liveStrokeCache.Reset();
        }

        if (pointer is not null)
        {
            DrawingCanvas.ReleasePointerCapture(pointer);
        }

        _activePointerId = null;
        _isPanning = false;
        UpdateCanvasCursor();
        InvalidateCanvasAndOverlay();
    }

    private Matrix3x2 GetCanvasTransform() => _viewport.GetTransform(ViewModel.Zoom);

    private Vector2 ScreenToCanvas(Vector2 position) =>
        _viewport.ScreenToCanvas(position, ViewModel.Zoom);

    private void InvalidateCanvasAndOverlay()
    {
        DrawingCanvas.Invalidate();
        OverlayCanvas.Invalidate();
    }

    private bool IsInsideCanvas(Vector2 point) =>
        point.X >= 0 && point.Y >= 0 && point.X <= CanvasSize.X && point.Y <= CanvasSize.Y;

    private static Vector2 ToVector(Point point) => new((float)point.X, (float)point.Y);

    private void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string toolName } &&
            Enum.TryParse(toolName, out DrawingTool tool))
        {
            ApplyToolSelection(tool);
            FocusCanvasWorkspace();
        }
    }

    private void SecondaryBrushButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string toolName } &&
            Enum.TryParse(toolName, out DrawingTool tool))
        {
            ApplyToolSelection(tool);
            MoreBrushesFlyout.Hide();
            FocusCanvasWorkspace();
        }
    }

    private async void SaveCurrentFavoriteBrushButton_Click(object sender, RoutedEventArgs e)
    {
        FavoriteBrushesFlyout.Hide();
        string defaultName = string.Format(
            _resourceLoader.GetString("DefaultFavoriteBrushNameFormat"),
            GetToolDisplayName(ViewModel.SelectedTool),
            ViewModel.FavoriteBrushes.Count + 1);
        string? name = await PromptFavoriteBrushNameAsync(
            _resourceLoader.GetString("SaveFavoriteBrushTitle"),
            defaultName);
        if (name is null)
        {
            return;
        }

        ViewModel.SaveFavoriteBrush(name);
        UpdateFavoriteBrushEmptyState();
    }

    private void ApplyFavoriteBrushButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: FavoriteBrush favorite })
        {
            return;
        }

        ViewModel.ApplyFavoriteBrush(favorite);
        ApplyToolSelection(favorite.Tool);
        FavoriteBrushesFlyout.Hide();
        FocusCanvasWorkspace();
    }

    private async void RenameFavoriteBrushButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: FavoriteBrush favorite })
        {
            return;
        }

        FavoriteBrushesFlyout.Hide();
        string? name = await PromptFavoriteBrushNameAsync(
            _resourceLoader.GetString("RenameFavoriteBrushTitle"),
            favorite.Name);
        if (name is not null)
        {
            ViewModel.RenameFavoriteBrush(favorite.Id, name);
        }
    }

    private async void DeleteFavoriteBrushButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: FavoriteBrush favorite })
        {
            return;
        }

        FavoriteBrushesFlyout.Hide();
        ContentDialog dialog = CreateDialog(
            _resourceLoader.GetString("DeleteFavoriteBrushTitle"),
            string.Format(_resourceLoader.GetString("DeleteFavoriteBrushMessage"), favorite.Name),
            _resourceLoader.GetString("DeleteButtonText"));
        if (await ShowDialogAsync(dialog) == ContentDialogResult.Primary)
        {
            ViewModel.DeleteFavoriteBrush(favorite.Id);
            UpdateFavoriteBrushEmptyState();
        }
    }

    private void MoveFavoriteBrushUpButton_Click(object sender, RoutedEventArgs e) =>
        MoveFavoriteBrush(sender, -1);

    private void MoveFavoriteBrushDownButton_Click(object sender, RoutedEventArgs e) =>
        MoveFavoriteBrush(sender, 1);

    private void MoveFavoriteBrush(object sender, int direction)
    {
        if (sender is Button { DataContext: FavoriteBrush favorite })
        {
            ViewModel.MoveFavoriteBrush(favorite.Id, direction);
        }
    }

    private async Task<string?> PromptFavoriteBrushNameAsync(string title, string initialName)
    {
        TextBox editor = new()
        {
            Header = _resourceLoader.GetString("FavoriteBrushNameHeader"),
            MaxLength = 40,
            Text = initialName,
            SelectionStart = initialName.Length
        };
        ContentDialog dialog = CreateDialog(title, editor, _resourceLoader.GetString("SaveButtonText"));
        AutomationProperties.SetAutomationId(dialog, "FavoriteBrushNameDialog");
        AutomationProperties.SetAutomationId(editor, "FavoriteBrushNameEditor");
        dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(initialName);
        editor.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(editor.Text);
        return await ShowDialogAsync(dialog) == ContentDialogResult.Primary ? editor.Text.Trim() : null;
    }

    private void UpdateFavoriteBrushEmptyState() =>
        FavoriteBrushEmptyState.Visibility = ViewModel.FavoriteBrushes.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void ApplyToolSelection(DrawingTool tool)
    {
        ViewModel.SelectTool(tool);
        SetSelectedButton(_selectedToolButton, false);
        SetSelectedButton(_selectedSecondaryBrushButton, false);
        _selectedSecondaryBrushButton = null;

        _selectedToolButton = tool switch
        {
            DrawingTool.Pencil => PencilToolButton,
            DrawingTool.Ballpoint => BallpointToolButton,
            DrawingTool.Highlighter => HighlighterToolButton,
            DrawingTool.Eraser => EraserToolButton,
            DrawingTool.Pan => PanToolButton,
            _ => MoreBrushesButton
        };
        SetSelectedButton(_selectedToolButton, true);

        if (_selectedToolButton == MoreBrushesButton)
        {
            UpdateMoreBrushesIcon(tool);
            _selectedSecondaryBrushButton = tool switch
            {
                DrawingTool.Marker => MarkerToolButton,
                DrawingTool.Airbrush => AirbrushToolButton,
                DrawingTool.Pen => PenToolButton,
                DrawingTool.Crayon => CrayonToolButton,
                DrawingTool.Calligraphy => CalligraphyToolButton,
                _ => null
            };
            if (_selectedSecondaryBrushButton is not null)
            {
                SetSelectedButton(_selectedSecondaryBrushButton, true);
            }

            ToolTipService.SetToolTip(
                MoreBrushesButton,
                string.Format(_resourceLoader.GetString("MoreBrushesFormat"), GetToolDisplayName(tool)));
        }

        UpdateActiveToolStatusText();
        UpdateCanvasCursor();
        UpdateBrushSettingsVisibility();
        OverlayCanvas.Invalidate();
    }

    private static void SetSelectedButton(Button? button, bool selected)
    {
        if (button is null) return;
        if (!selected)
        {
            button.ClearValue(Control.BackgroundProperty);
            button.ClearValue(Control.TemplateProperty);
            return;
        }

        Brush background = (Brush)Application.Current.Resources["ToolbarSelectedBrush"];
        button.Background = background;
        button.Template = (ControlTemplate)Application.Current.Resources["SelectedToolButtonTemplate"];
    }

    private void UpdateMoreBrushesIcon(DrawingTool tool)
    {
        MoreIconMarker.Visibility = tool == DrawingTool.Marker ? Visibility.Visible : Visibility.Collapsed;
        MoreIconAirbrush.Visibility = tool == DrawingTool.Airbrush ? Visibility.Visible : Visibility.Collapsed;
        MoreIconPen.Visibility = tool == DrawingTool.Pen ? Visibility.Visible : Visibility.Collapsed;
        MoreIconCrayon.Visibility = tool == DrawingTool.Crayon ? Visibility.Visible : Visibility.Collapsed;
        MoreIconCalligraphy.Visibility = tool == DrawingTool.Calligraphy ? Visibility.Visible : Visibility.Collapsed;
    }

    private string GetToolDisplayName(DrawingTool tool) =>
        _resourceLoader.GetString($"ToolName{tool}");

    private void UpdateActiveToolStatusText()
    {
        string toolName = GetToolDisplayName(ViewModel.SelectedTool);
        ActiveToolStatusText.Text = ViewModel.SelectedTool == DrawingTool.Pan
            ? toolName
            : $"{toolName} · {ViewModel.BrushSize:0} px";
    }

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Background: SolidColorBrush brush })
        {
            ViewModel.SelectedColor = brush.Color;
            ColorFlyout.Hide();
            FocusCanvasWorkspace();
        }
    }

    private void Flyout_Opening(object? sender, object e)
    {
        if (sender is Flyout { Content: FrameworkElement element })
        {
            element.RequestedTheme = RootLayout.ActualTheme;
        }
    }

    private void BrushSettingsFlyout_Opening(object? sender, object e)
    {
        Flyout_Opening(sender, e);
        ((ComboBoxItem)AirbrushModeComboBox.Items[0]).Content = _resourceLoader.GetString("AirbrushModeMistItemText");
        ((ComboBoxItem)AirbrushModeComboBox.Items[1]).Content = _resourceLoader.GetString("AirbrushModeGrainItemText");
        ((ComboBoxItem)PressureCurveComboBox.Items[0]).Content = _resourceLoader.GetString("PressureCurveSoftItemText");
        ((ComboBoxItem)PressureCurveComboBox.Items[1]).Content = _resourceLoader.GetString("PressureCurveLinearItemText");
        ((ComboBoxItem)PressureCurveComboBox.Items[2]).Content = _resourceLoader.GetString("PressureCurveHardItemText");
        AirbrushModeComboBox.SelectedIndex = (int)ViewModel.SelectedAirbrushMode;
        PressureCurveComboBox.SelectedIndex = (int)ViewModel.PressureCurve;
        UpdateBrushSettingsVisibility();
    }

    private void AirbrushModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AirbrushModeComboBox.SelectedIndex >= 0)
        {
            ViewModel.SelectedAirbrushMode = (AirbrushMode)AirbrushModeComboBox.SelectedIndex;
        }
    }

    private void PressureCurveComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PressureCurveComboBox.SelectedIndex >= 0)
        {
            ViewModel.PressureCurve = (PressureCurve)PressureCurveComboBox.SelectedIndex;
        }
    }

    private void UpdateBrushSettingsVisibility()
    {
        DrawingTool tool = ViewModel.SelectedTool;
        SoftnessPanel.Visibility = tool is
            DrawingTool.Airbrush or
            DrawingTool.Crayon or
            DrawingTool.Eraser
            ? Visibility.Visible
            : Visibility.Collapsed;
        AirbrushModePanel.Visibility = tool == DrawingTool.Airbrush
            ? Visibility.Visible
            : Visibility.Collapsed;
        FlowPanel.Visibility = tool is DrawingTool.Airbrush or DrawingTool.Crayon
            ? Visibility.Visible
            : Visibility.Collapsed;
        PressureCurvePanel.Visibility = tool == DrawingTool.Pan
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ToolFlyout_Closed(object? sender, object e) => FocusCanvasWorkspace();

    private void FocusCanvasWorkspace()
    {
        if (!_isDialogOpen)
        {
            RootLayout.Focus(FocusState.Programmatic);
        }
    }

    private void UpdateCanvasCursor()
    {
        InputCursor? cursor = _isPanning
            ? PanningCursor
            : ViewModel.SelectedTool == DrawingTool.Pan || _isSpaceHeld
                ? PanCursor
                : null;
        CanvasWorkspace.SetCursor(cursor);
        OverlayCanvas.Invalidate();
    }

    private async void NewDocumentMenuItem_Click(object sender, RoutedEventArgs e) => await NewDocumentAsync();

    private async Task NewDocumentAsync()
    {
        if (!await ConfirmDiscardOrSaveAsync(_resourceLoader.GetString("NewDocumentUnsavedMessage")))
        {
            return;
        }

        _document.Reset(_resourceLoader.GetString("DefaultLayerName/Text"));
        LayerList.SelectedItem = _document.SelectedLayer;
        FitCanvasToViewport();
    }

    private async void OpenDocumentMenuItem_Click(object sender, RoutedEventArgs e) =>
        await OpenProjectAsync();

    private async Task OpenProjectAsync()
    {
        if (!await ConfirmDiscardOrSaveAsync(
            _resourceLoader.GetString("OpenDocumentUnsavedMessage")))
        {
            return;
        }

        FileOpenPicker picker = CreateOpenPicker();
        picker.FileTypeFilter.Add(".mydraw");
        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        await LoadProjectFromPathAsync(file.Path);
    }

    private async Task OpenProjectFromPathAsync(string filePath)
    {
        if (!await ConfirmDiscardOrSaveAsync(
            _resourceLoader.GetString("OpenDocumentUnsavedMessage")))
        {
            return;
        }

        await LoadProjectFromPathAsync(filePath);
    }

    private async Task LoadProjectFromPathAsync(string filePath)
    {
        try
        {
            ProjectLoadResult result = await _projectFileService.LoadAsync(filePath);
            _document.Load(
                result.Width,
                result.Height,
                result.Layers,
                result.Strokes,
                filePath);
            LayerList.SelectedItem = _document.SelectedLayer;
            FitCanvasToViewport();
        }
        catch (Exception exception) when (exception is
            IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            JsonException or
            NotSupportedException or
            ArgumentException)
        {
            _logger.LogError(exception, "Unable to open project.");
            await ShowErrorAsync(
                _resourceLoader.GetString("OpenFailedTitle"),
                exception.Message,
                "OpenFailedDialog");
        }
    }

    private FileOpenPicker CreateOpenPicker()
    {
        FileOpenPicker picker = new();
        nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
        return picker;
    }

    private async void CanvasSizeButton_Click(object sender, RoutedEventArgs e) => await ShowCanvasSizeDialogAsync();

    private async Task ShowCanvasSizeDialogAsync()
    {
        ComboBox presets = new() { SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        presets.Items.Add(_resourceLoader.GetString("CanvasPresetCurrent"));
        presets.Items.Add("1920 × 1080");
        presets.Items.Add("1080 × 1080");
        presets.Items.Add("A4 直式 2480 × 3508");
        presets.Items.Add("A4 橫式 3508 × 2480");
        presets.Items.Add(_resourceLoader.GetString("CanvasPresetCustom"));

        NumberBox widthBox = new()
        {
            Header = _resourceLoader.GetString("CanvasWidth"),
            Minimum = 64,
            Maximum = 8192,
            Value = _document.Canvas.Width,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
        };
        NumberBox heightBox = new()
        {
            Header = _resourceLoader.GetString("CanvasHeight"),
            Minimum = 64,
            Maximum = 8192,
            Value = _document.Canvas.Height,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
        };
        presets.SelectionChanged += (_, _) => ApplyCanvasPreset(presets.SelectedIndex, widthBox, heightBox);

        StackPanel content = new() { Spacing = 12, MinWidth = 280 };
        content.Children.Add(presets);
        content.Children.Add(widthBox);
        content.Children.Add(heightBox);
        ContentDialog dialog = CreateDialog(
            _resourceLoader.GetString("CanvasDialogTitle"),
            content,
            _resourceLoader.GetString("ApplyButton"));
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        int width = double.IsNaN(widthBox.Value) ? _document.Canvas.Width : (int)widthBox.Value;
        int height = double.IsNaN(heightBox.Value) ? _document.Canvas.Height : (int)heightBox.Value;
        if (width < _document.Canvas.Width || height < _document.Canvas.Height)
        {
            ContentDialog warning = CreateDialog(
                _resourceLoader.GetString("CanvasShrinkTitle"),
                new TextBlock
                {
                    Text = _resourceLoader.GetString("CanvasShrinkMessage"),
                    TextWrapping = TextWrapping.Wrap
                },
                _resourceLoader.GetString("ContinueButton"));
            if (await ShowDialogAsync(warning) != ContentDialogResult.Primary)
            {
                return;
            }
        }

        _document.ResizeCanvas(width, height);
    }

    private static void ApplyCanvasPreset(int index, NumberBox widthBox, NumberBox heightBox)
    {
        (double Width, double Height)? size = index switch
        {
            1 => (1920, 1080),
            2 => (1080, 1080),
            3 => (2480, 3508),
            4 => (3508, 2480),
            _ => null
        };
        if (size is not null)
        {
            widthBox.Value = size.Value.Width;
            heightBox.Value = size.Value.Height;
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e) => await SaveProjectAsync(false);

    private async void SaveAsMenuItem_Click(object sender, RoutedEventArgs e) => await SaveProjectAsync(true);

    private async Task<bool> SaveProjectAsync(bool saveAs)
    {
        string? filePath = saveAs ? null : _document.FilePath;
        if (filePath is null)
        {
            FileSavePicker picker = CreateSavePicker();
            picker.SuggestedFileName = _resourceLoader.GetString("UntitledFileName");
            picker.FileTypeChoices.Add(_resourceLoader.GetString("ProjectFileType"), [".mydraw"]);
            StorageFile? file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return false;
            }

            filePath = file.Path;
        }

        try
        {
            int strokeVersion = _document.StrokeVersion;
            await _projectFileService.SaveAsync(_document, filePath);
            _document.MarkSaved(filePath, strokeVersion == _document.StrokeVersion);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Unable to save project.");
            await ShowErrorAsync(
                _resourceLoader.GetString("SaveFailedTitle"),
                exception.Message,
                "SaveFailedDialog");
            return false;
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e) => await ExportPngAsync();

    private async Task ExportPngAsync()
    {
        FileSavePicker picker = CreateSavePicker();
        picker.SuggestedFileName = _resourceLoader.GetString("UntitledFileName");
        picker.FileTypeChoices.Add("PNG", [".png"]);
        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        ExportSnapshot snapshot = CreateExportSnapshot();
        try
        {
            Task exportTask = Task.Run(async () =>
            {
                CanvasDevice device = CanvasDevice.GetSharedDevice();
                using CanvasRenderTarget renderTarget = new(
                    device,
                    snapshot.Width,
                    snapshot.Height,
                    96);
                using (CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession())
                {
                    RenderExport(drawingSession, snapshot);
                }

                await renderTarget.SaveAsync(file.Path, CanvasBitmapFileFormat.Png);
            });
            _activeExport = exportTask;
            try
            {
                await exportTask;
            }
            finally
            {
                _activeExport = null;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Unable to export PNG.");
            await ShowErrorAsync(
                _resourceLoader.GetString("ExportFailedTitle"),
                exception.Message,
                "ExportFailedDialog");
        }
    }

    private ExportSnapshot CreateExportSnapshot()
    {
        Dictionary<Guid, List<DrawingStroke>> strokesByLayer = [];
        foreach (DrawingStroke stroke in _document.Strokes)
        {
            if (!strokesByLayer.TryGetValue(stroke.LayerId, out List<DrawingStroke>? strokes))
            {
                strokes = [];
                strokesByLayer.Add(stroke.LayerId, strokes);
            }

            strokes.Add(stroke);
        }

        if (_document.CurrentStroke is { Points.Count: > 0 } currentStroke)
        {
            if (!strokesByLayer.TryGetValue(currentStroke.LayerId, out List<DrawingStroke>? strokes))
            {
                strokes = [];
                strokesByLayer.Add(currentStroke.LayerId, strokes);
            }

            strokes.Add(CloneStroke(currentStroke));
        }

        List<ExportLayer> layers = _document.Layers
            .Reverse()
            .Select(layer => new ExportLayer(layer.Id, layer.IsVisible, layer.Opacity))
            .ToList();
        return new ExportSnapshot(
            _document.Canvas.Width,
            _document.Canvas.Height,
            layers,
            strokesByLayer);
    }

    private static void RenderExport(CanvasDrawingSession drawingSession, ExportSnapshot snapshot)
    {
        drawingSession.Clear(Colors.Transparent);
        using (CanvasActiveLayer paperLayer = drawingSession.CreateLayer(
            1,
            new Rect(0, 0, snapshot.Width, snapshot.Height)))
        {
            drawingSession.FillRectangle(0, 0, snapshot.Width, snapshot.Height, Colors.White);
        }

        foreach (ExportLayer layer in snapshot.Layers)
        {
            if (!layer.IsVisible || layer.Opacity <= 0)
            {
                continue;
            }

            if (!snapshot.StrokesByLayer.TryGetValue(layer.Id, out List<DrawingStroke>? strokes) ||
                strokes.Count == 0)
            {
                continue;
            }

            if (layer.Opacity < 1)
            {
                using CanvasActiveLayer opacityLayer = drawingSession.CreateLayer((float)layer.Opacity);
                DrawExportStrokes(drawingSession, strokes);
            }
            else
            {
                DrawExportStrokes(drawingSession, strokes);
            }
        }
    }

    private static void DrawExportStrokes(
        CanvasDrawingSession drawingSession,
        IReadOnlyList<DrawingStroke> strokes)
    {
        bool hasHighlighter = false;
        foreach (DrawingStroke stroke in strokes)
        {
            if (stroke.Tool == DrawingTool.Highlighter)
            {
                hasHighlighter = true;
            }
            else
            {
                BrushRenderer.Draw(drawingSession, stroke);
            }
        }

        if (!hasHighlighter)
        {
            return;
        }

        using CanvasCommandList highlighter = new(drawingSession.Device);
        using (CanvasDrawingSession highlighterSession = highlighter.CreateDrawingSession())
        {
            highlighterSession.Blend = CanvasBlend.Copy;
            foreach (DrawingStroke stroke in strokes)
            {
                if (stroke.Tool == DrawingTool.Highlighter)
                {
                    BrushRenderer.Draw(highlighterSession, stroke);
                }
            }
        }

        drawingSession.DrawImage(highlighter);
    }

    private static DrawingStroke CloneStroke(DrawingStroke source)
    {
        DrawingStroke clone = new(
            source.Tool,
            source.Color,
            source.Size,
            source.Opacity,
            source.UseSizePressure,
            source.UseOpacityPressure,
            source.BrushId,
            source.TextureSeed,
            source.Softness,
            source.AirbrushMode,
            source.Flow)
        {
            LayerId = source.LayerId
        };
        foreach (StrokePoint point in source.Points)
        {
            clone.Points.Add(point);
        }

        return clone;
    }

    private FileSavePicker CreateSavePicker()
    {
        FileSavePicker picker = new();
        nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
        return picker;
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e) => await ShowSettingsDialogAsync();

    private async void FileAssociationMenuItem_Click(object sender, RoutedEventArgs e) =>
        await ShowFileAssociationDialogAsync();

    private async Task ShowFileAssociationDialogAsync()
    {
        bool isRegistered;
        try
        {
            isRegistered = _fileAssociationService.IsRegistered();
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or SecurityException)
        {
            _logger.LogError(exception, "Unable to read file association state.");
            await ShowErrorAsync(
                _resourceLoader.GetString("FileAssociationFailedTitle"),
                exception.Message,
                "FileAssociationFailedDialog");
            return;
        }

        StackPanel content = new() { Spacing = 12, MaxWidth = 420 };
        content.Children.Add(new TextBlock
        {
            Text = _resourceLoader.GetString(
                isRegistered
                    ? "FileAssociationStatusRegistered"
                    : "FileAssociationStatusUnregistered"),
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = _resourceLoader.GetString("FileAssociationDescription"),
            TextWrapping = TextWrapping.Wrap
        });

        ContentDialog dialog = CreateDialog(
            _resourceLoader.GetString("FileAssociationTitle"),
            content,
            _resourceLoader.GetString(
                isRegistered ? "RemoveAssociationButton" : "RegisterAssociationButton"));
        AutomationProperties.SetAutomationId(dialog, "FileAssociationDialog");
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            if (isRegistered)
            {
                _fileAssociationService.Unregister();
            }
            else
            {
                _fileAssociationService.Register(_resourceLoader.GetString("ProjectFileType"));
            }

            await ShowMessageAsync(
                _resourceLoader.GetString("FileAssociationSuccessTitle"),
                _resourceLoader.GetString("FileAssociationSuccessMessage"),
                "FileAssociationSuccessDialog");
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or SecurityException or InvalidOperationException)
        {
            _logger.LogError(exception, "Unable to update file association.");
            await ShowErrorAsync(
                _resourceLoader.GetString("FileAssociationFailedTitle"),
                exception.Message,
                "FileAssociationFailedDialog");
        }
    }

    private async Task ShowSettingsDialogAsync()
    {
        AppSettings settings = _settingsService.Settings;
        ComboBox modeBox = new()
        {
            Header = _resourceLoader.GetString("DisplayModeHeader"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        modeBox.Items.Add(new ComboBoxItem { Content = _resourceLoader.GetString("SystemModeText"), Tag = DisplayMode.System });
        modeBox.Items.Add(new ComboBoxItem { Content = _resourceLoader.GetString("LightModeText"), Tag = DisplayMode.Light });
        modeBox.Items.Add(new ComboBoxItem { Content = _resourceLoader.GetString("DarkModeText"), Tag = DisplayMode.Dark });
        modeBox.SelectedItem = modeBox.Items
            .OfType<ComboBoxItem>()
            .First(item => Equals(item.Tag, settings.DisplayMode));

        ComboBox themeBox = new()
        {
            Header = _resourceLoader.GetString("ColorThemeHeader"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        themeBox.Items.Add(new ComboBoxItem { Content = _resourceLoader.GetString("EyeCareThemeText"), Tag = ColorTheme.EyeCare });
        themeBox.Items.Add(new ComboBoxItem { Content = _resourceLoader.GetString("OceanThemeText"), Tag = ColorTheme.Ocean });
        themeBox.Items.Add(new ComboBoxItem { Content = _resourceLoader.GetString("ForestThemeText"), Tag = ColorTheme.Forest });
        themeBox.Items.Add(new ComboBoxItem { Content = _resourceLoader.GetString("LavenderThemeText"), Tag = ColorTheme.Lavender });
        themeBox.Items.Add(new ComboBoxItem { Content = _resourceLoader.GetString("NeutralThemeText"), Tag = ColorTheme.Neutral });
        themeBox.SelectedItem = themeBox.Items
            .OfType<ComboBoxItem>()
            .First(item => Equals(item.Tag, settings.ColorTheme));

        StackPanel content = new() { Spacing = 16, MinWidth = 280 };
        ComboBox toolbarBox = new()
        {
            Header = _resourceLoader.GetString("ToolbarPositionHeader"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = settings.IsToolbarOnRight ? 1 : 0
        };
        ComboBoxItem leftChoice = new() { Content = _resourceLoader.GetString("ToolbarLeftText") };
        ComboBoxItem rightChoice = new() { Content = _resourceLoader.GetString("ToolbarRightText") };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(leftChoice, "ToolbarLeftChoice");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(rightChoice, "ToolbarRightChoice");
        toolbarBox.Items.Add(leftChoice);
        toolbarBox.Items.Add(rightChoice);
        toolbarBox.SelectedIndex = settings.IsToolbarOnRight ? 1 : 0;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(toolbarBox, "ToolbarPositionComboBox");
        content.Children.Add(modeBox);
        content.Children.Add(themeBox);
        content.Children.Add(toolbarBox);
        ContentDialog dialog = CreateDialog(
            _resourceLoader.GetString("SettingsTitle"),
            content,
            _resourceLoader.GetString("ApplyButton"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(dialog, "AppearanceSettingsDialog");
        if (await ShowDialogAsync(dialog) == ContentDialogResult.Primary &&
            modeBox.SelectedItem is ComboBoxItem { Tag: DisplayMode displayMode } &&
            themeBox.SelectedItem is ComboBoxItem { Tag: ColorTheme colorTheme })
        {
            settings.IsToolbarOnRight = toolbarBox.SelectedIndex == 1;
            ApplyAppearance(displayMode, colorTheme);
            ApplyToolbarPosition();
        }
    }

    private void ApplyAppearance(DisplayMode displayMode, ColorTheme colorTheme)
    {
        _themeService.Apply(RootLayout, displayMode, colorTheme);
        SetSelectedButton(_selectedToolButton, true);
        SetSelectedButton(_selectedSecondaryBrushButton, true);
    }

    private async void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Assembly assembly = typeof(App).Assembly;
        string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString() ?? string.Empty;
        string fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? string.Empty;
        StackPanel content = new() { Spacing = 12, MaxWidth = 400 };
        content.Children.Add(new Image
        {
            Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/MyDraw-256.png")),
            Width = 80,
            Height = 80,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        content.Children.Add(new TextBlock { Text = _resourceLoader.GetString("AppTitle"), Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"] });
        content.Children.Add(new TextBlock { Text = string.Format(_resourceLoader.GetString("AboutVersionFormat"), version, fileVersion), TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = _resourceLoader.GetString("AboutLicense"), TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = _resourceLoader.GetString("AboutPrivacy"), TextWrapping = TextWrapping.Wrap });
        ContentDialog dialog = new()
        {
            XamlRoot = RootLayout.XamlRoot,
            RequestedTheme = RootLayout.ActualTheme,
            Title = _resourceLoader.GetString("AboutTitle"),
            Content = content,
            CloseButtonText = _resourceLoader.GetString("CloseButton")
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(dialog, "AboutDialog");
        await ShowDialogAsync(dialog);
    }

    private void AddLayerButton_Click(object sender, RoutedEventArgs e)
    {
        _document.AddLayer(string.Format(_resourceLoader.GetString("NewLayerName"), _document.Layers.Count + 1));
        LayerList.SelectedItem = _document.SelectedLayer;
    }

    private void LayerList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _document.SelectedLayer = LayerList.SelectedItem as DrawingLayer;

    private void LayerUpButton_Click(object sender, RoutedEventArgs e) => _document.MoveSelectedLayer(-1);

    private void LayerDownButton_Click(object sender, RoutedEventArgs e) => _document.MoveSelectedLayer(1);

    private async void DeleteLayerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_document.Layers.Count <= 1) return;
        LayersFlyout.Hide();
        ContentDialog dialog = CreateDialog(
            _resourceLoader.GetString("DeleteLayerTitle"),
            _resourceLoader.GetString("DeleteLayerMessage"),
            _resourceLoader.GetString("DeleteButtonText"));
        if (await ShowDialogAsync(dialog) == ContentDialogResult.Primary)
        {
            _document.DeleteSelectedLayer();
            LayerList.SelectedItem = _document.SelectedLayer;
        }
    }

    private void GridToggle_Click(object sender, RoutedEventArgs e) => OverlayCanvas.Invalidate();

    private void ZoomInMenuItem_Click(object sender, RoutedEventArgs e) => ZoomByFactor(1.2f);

    private void ZoomOutMenuItem_Click(object sender, RoutedEventArgs e) => ZoomByFactor(1 / 1.2f);

    private void ZoomResetMenuItem_Click(object sender, RoutedEventArgs e) => ResetZoom();

    private void ZoomByFactor(float factor)
    {
        Vector2 center = new(
            Math.Max((float)DrawingCanvas.ActualWidth / 2, 1),
            Math.Max((float)DrawingCanvas.ActualHeight / 2, 1));
        Vector2 canvasPoint = ScreenToCanvas(center);
        ViewModel.Zoom = Math.Clamp(ViewModel.Zoom * factor, 0.1f, 4);
        _viewport.SetOffsetForZoom(center, canvasPoint, ViewModel.Zoom);
        InvalidateCanvasAndOverlay();
    }

    private void ResetZoom()
    {
        ViewModel.Zoom = 1f;
        _viewport.Reset(
            1f,
            new Vector2((float)DrawingCanvas.ActualWidth, (float)DrawingCanvas.ActualHeight),
            CanvasSize);
        InvalidateCanvasAndOverlay();
        FocusCanvasWorkspace();
    }

    private void DrawingCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_hasFittedCanvas || e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
        {
            return;
        }

        _hasFittedCanvas = true;
        FitCanvasToViewport();
    }

    private void FitCanvasToViewport()
    {
        float viewportWidth = (float)DrawingCanvas.ActualWidth;
        float viewportHeight = (float)DrawingCanvas.ActualHeight;
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        const float padding = 48;
        float availableWidth = Math.Max(viewportWidth - padding, 1);
        float availableHeight = Math.Max(viewportHeight - padding, 1);
        float zoom = Math.Clamp(Math.Min(availableWidth / CanvasSize.X, availableHeight / CanvasSize.Y), 0.1f, 4);
        ViewModel.Zoom = zoom;
        _viewport.Fit(
            zoom,
            new Vector2(viewportWidth, viewportHeight),
            CanvasSize);
        InvalidateCanvasAndOverlay();
    }

    private void ImmersiveButton_Click(object sender, RoutedEventArgs e) => SetImmersive(!_isImmersive);

    private void FullScreenMenuItem_Click(object sender, RoutedEventArgs e) => ToggleFullScreen();

    private void ToggleFullScreen()
    {
        _isFullScreen = !_isFullScreen;
        AppWindow.SetPresenter(_isFullScreen ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Default);
        SetImmersive(_isFullScreen);
    }

    private async void RootLayout_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_isDialogOpen)
        {
            return;
        }

        bool isCtrl = IsKeyDown(VirtualKey.Control);
        bool isShift = IsKeyDown(VirtualKey.Shift);
        bool isAlt = IsKeyDown(VirtualKey.Menu);
        bool isTextInputFocused = FocusManager.GetFocusedElement(RootLayout.XamlRoot) is TextBox or NumberBox;

        if (isCtrl && isAlt && e.Key == VirtualKey.C)
        {
            e.Handled = true;
            await ShowCanvasSizeDialogAsync();
            return;
        }

        if (isCtrl && !isAlt)
        {
            switch (e.Key)
            {
                case VirtualKey.N:
                    e.Handled = true;
                    await NewDocumentAsync();
                    return;
                case VirtualKey.O:
                    e.Handled = true;
                    await OpenProjectAsync();
                    return;
                case VirtualKey.S:
                    e.Handled = true;
                    await SaveProjectAsync(isShift);
                    return;
                case VirtualKey.E:
                    e.Handled = true;
                    await ExportPngAsync();
                    return;
                case VirtualKey.Z:
                    if (!isTextInputFocused)
                    {
                        if (isShift) ViewModel.RedoCommand.Execute(null);
                        else ViewModel.UndoCommand.Execute(null);
                        e.Handled = true;
                    }

                    return;
                case VirtualKey.Y:
                    if (!isTextInputFocused)
                    {
                        ViewModel.RedoCommand.Execute(null);
                        e.Handled = true;
                    }

                    return;
                case VirtualKey.G:
                    ViewModel.IsGridVisible = !ViewModel.IsGridVisible;
                    e.Handled = true;
                    return;
                case VirtualKey.Add:
                case (VirtualKey)187:
                    ZoomByFactor(1.2f);
                    e.Handled = true;
                    return;
                case VirtualKey.Subtract:
                case (VirtualKey)189:
                    ZoomByFactor(1 / 1.2f);
                    e.Handled = true;
                    return;
                case VirtualKey.Number0:
                case VirtualKey.NumberPad0:
                    ResetZoom();
                    e.Handled = true;
                    return;
                case (VirtualKey)188:
                    e.Handled = true;
                    await ShowSettingsDialogAsync();
                    return;
            }
        }

        if (e.Key == VirtualKey.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Escape && _isFullScreen)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        if (isCtrl || isAlt || isTextInputFocused)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Space:
                _isSpaceHeld = true;
                UpdateCanvasCursor();
                e.Handled = true;
                break;
            case VirtualKey.Tab:
                SetImmersive(!_isImmersive);
                e.Handled = true;
                break;
            case VirtualKey.P:
                ApplyToolSelection(DrawingTool.Pen);
                e.Handled = true;
                break;
            case VirtualKey.M:
                ApplyToolSelection(DrawingTool.Highlighter);
                e.Handled = true;
                break;
            case VirtualKey.E:
                ApplyToolSelection(DrawingTool.Eraser);
                e.Handled = true;
                break;
            case VirtualKey.H:
                ApplyToolSelection(DrawingTool.Pan);
                e.Handled = true;
                break;
            case (VirtualKey)219:
                ViewModel.BrushSize = Math.Clamp(ViewModel.BrushSize - 2, 1, 80);
                e.Handled = true;
                break;
            case (VirtualKey)221:
                ViewModel.BrushSize = Math.Clamp(ViewModel.BrushSize + 2, 1, 80);
                e.Handled = true;
                break;
        }
    }

    private void RootLayout_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Space)
        {
            _isSpaceHeld = false;
            UpdateCanvasCursor();
        }
    }

    private void MainWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
        {
            _isSpaceHeld = false;
            UpdateCanvasCursor();
        }
    }

    private static bool IsKeyDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    private void SetImmersive(bool value)
    {
        _isImmersive = value;
        TopBar.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        ToolBar.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        TopBarRow.Height = value ? new GridLength(0) : new GridLength(40);
        ApplyToolbarPosition();
        FocusCanvasWorkspace();
    }

    private void ApplyToolbarPosition()
    {
        bool right = _settingsService.Settings.IsToolbarOnRight;
        Grid.SetColumn(ToolBar, right ? 1 : 0);
        Grid.SetColumn(CanvasWorkspace, right ? 0 : 1);
        GridLength toolWidth = new(_isImmersive ? 0 : 52);
        ToolColumn.Width = right ? new GridLength(1, GridUnitType.Star) : toolWidth;
        WorkspaceColumn.Width = right ? toolWidth : new GridLength(1, GridUnitType.Star);
        ToolBar.BorderThickness = right ? new Thickness(1, 0, 0, 0) : new Thickness(0, 0, 1, 0);
        FlyoutPlacementMode placement = right ? FlyoutPlacementMode.LeftEdgeAlignedTop : FlyoutPlacementMode.RightEdgeAlignedTop;
        MoreBrushesFlyout.Placement = placement;
        FavoriteBrushesFlyout.Placement = placement;
        BrushSettingsFlyout.Placement = placement;
        ColorFlyout.Placement = placement;
        LayersFlyout.Placement = placement;
    }

    private void RootLayout_Loaded(object sender, RoutedEventArgs e)
    {
        AppSettings settings = _settingsService.Settings;
        _themeService.Apply(RootLayout, settings.DisplayMode, settings.ColorTheme);
        ApplyToolbarPosition();
        ApplyToolSelection(DrawingTool.Pencil);
        _isLoaded = true;
        ProcessPendingProject();

        if (settings.IsMaximized && AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }

        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            _hasFittedCanvas = true;
            FitCanvasToViewport();
        });
        UpdateCanvasCursor();
        FocusCanvasWorkspace();
    }

    private void RestoreWindowPlacement()
    {
        AppSettings settings = _settingsService.Settings;
        RectInt32 requested = settings.HasWindowPlacement
            ? new RectInt32(settings.WindowX, settings.WindowY, settings.WindowWidth, settings.WindowHeight)
            : new RectInt32(0, 0, 1280, 800);
        DisplayArea displayArea = DisplayArea.GetFromRect(requested, DisplayAreaFallback.Primary);
        RectInt32 workArea = displayArea.WorkArea;
        int width = Math.Clamp(requested.Width, 800, workArea.Width);
        int height = Math.Clamp(requested.Height, 600, workArea.Height);
        int x = settings.HasWindowPlacement
            ? Math.Clamp(requested.X, workArea.X, workArea.X + workArea.Width - width)
            : workArea.X + ((workArea.Width - width) / 2);
        int y = settings.HasWindowPlacement
            ? Math.Clamp(requested.Y, workArea.Y, workArea.Y + workArea.Height - height)
            : workArea.Y + ((workArea.Height - height) / 2);

        _isRestoringPlacement = true;
        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        _isRestoringPlacement = false;
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_isRestoringPlacement || _isFullScreen ||
            sender.Presenter is OverlappedPresenter { State: not OverlappedPresenterState.Restored })
        {
            return;
        }

        AppSettings settings = _settingsService.Settings;
        settings.WindowX = sender.Position.X;
        settings.WindowY = sender.Position.Y;
        settings.WindowWidth = sender.Size.Width;
        settings.WindowHeight = sender.Size.Height;
        settings.HasWindowPlacement = true;
    }

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isCloseConfirmed || !_document.IsModified)
        {
            return;
        }

        args.Cancel = true;
        if (await ConfirmDiscardOrSaveAsync(_resourceLoader.GetString("UnsavedMessage")))
        {
            _isCloseConfirmed = true;
            Close();
        }
    }

    private async Task<bool> ConfirmDiscardOrSaveAsync(string message)
    {
        if (!_document.IsModified)
        {
            return true;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = RootLayout.XamlRoot,
            RequestedTheme = RootLayout.ActualTheme,
            Title = _resourceLoader.GetString("UnsavedTitle"),
            Content = message,
            PrimaryButtonText = _resourceLoader.GetString("SaveButtonText"),
            SecondaryButtonText = _resourceLoader.GetString("DontSaveButton"),
            CloseButtonText = _resourceLoader.GetString("CancelButton"),
            DefaultButton = ContentDialogButton.Primary
        };
        ContentDialogResult result = await ShowDialogAsync(dialog);
        if (result == ContentDialogResult.Primary)
        {
            return await SaveProjectAsync(false);
        }

        return result == ContentDialogResult.Secondary;
    }

    private void Document_Changed(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(LayerList.SelectedItem, _document.SelectedLayer))
        {
            LayerList.SelectedItem = _document.SelectedLayer;
        }

        HashSet<Guid> layerIds = _document.Layers.Select(layer => layer.Id).ToHashSet();
        foreach (Guid id in _layerCaches.Keys.Where(id => !layerIds.Contains(id)).ToArray())
        {
            _layerCaches[id].Dispose();
            _layerCaches.Remove(id);
        }

        if (_document.CurrentStroke is null)
        {
            _liveStrokeCache.Reset();
        }

        UpdateWindowTitle();
        DrawingCanvas.Invalidate();
    }

    private void Document_StrokePreviewChanged(object? sender, EventArgs e) => DrawingCanvas.Invalidate();

    private void UpdateWindowTitle()
    {
        string title = _document.IsModified ? $"{_appTitle} *" : _appTitle;
        if (title == _windowTitle)
        {
            return;
        }

        _windowTitle = title;
        Title = title;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsGridVisible))
        {
            OverlayCanvas.Invalidate();
        }
        else if (e.PropertyName == nameof(MainViewModel.Zoom))
        {
            InvalidateCanvasAndOverlay();
        }
        else if (e.PropertyName == nameof(MainViewModel.BrushSize))
        {
            UpdateActiveToolStatusText();
            OverlayCanvas.Invalidate();
        }
    }

    private ContentDialog CreateDialog(string title, object content, string primaryButtonText) => new()
    {
        XamlRoot = RootLayout.XamlRoot,
        RequestedTheme = RootLayout.ActualTheme,
        Title = title,
        Content = content,
        PrimaryButtonText = primaryButtonText,
        CloseButtonText = _resourceLoader.GetString("CancelButton"),
        DefaultButton = ContentDialogButton.Primary
    };

    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        _isDialogOpen = true;
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            _isDialogOpen = false;
            FocusCanvasWorkspace();
            ProcessPendingProject();
        }
    }

    private async Task ShowErrorAsync(
        string title,
        string message,
        string? automationId = null)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = RootLayout.XamlRoot,
            RequestedTheme = RootLayout.ActualTheme,
            Title = title,
            Content = message,
            CloseButtonText = _resourceLoader.GetString("CloseButton")
        };
        if (automationId is not null)
        {
            AutomationProperties.SetAutomationId(dialog, automationId);
        }

        await ShowDialogAsync(dialog);
    }

    private async Task ShowMessageAsync(
        string title,
        string message,
        string automationId)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = RootLayout.XamlRoot,
            RequestedTheme = RootLayout.ActualTheme,
            Title = title,
            Content = message,
            CloseButtonText = _resourceLoader.GetString("CloseButton")
        };
        AutomationProperties.SetAutomationId(dialog, automationId);
        await ShowDialogAsync(dialog);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        Activated -= MainWindow_Activated;
        AppWindow.Changed -= AppWindow_Changed;
        AppWindow.Closing -= AppWindow_Closing;
        AppSettings settings = _settingsService.Settings;
        settings.IsMaximized = AppWindow.Presenter is OverlappedPresenter
        {
            State: OverlappedPresenterState.Maximized
        };
        ViewModel.PersistToolSettings();
        _settingsService.Save();
        _document.Changed -= Document_Changed;
        _document.StrokePreviewChanged -= Document_StrokePreviewChanged;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        DrawingCanvas.SizeChanged -= DrawingCanvas_SizeChanged;
        DrawingCanvas.PointerEntered -= DrawingCanvas_PointerEntered;
        DrawingCanvas.PointerExited -= DrawingCanvas_PointerExited;
        DisposeLayerCaches();
        _liveStrokeCache.Dispose();
        if (_activeExport is null || _activeExport.IsCompleted)
        {
            BrushRenderer.ResetTips();
        }

        DrawingCanvas.RemoveFromVisualTree();
        OverlayCanvas.RemoveFromVisualTree();
    }

    private sealed record ExportSnapshot(
        int Width,
        int Height,
        IReadOnlyList<ExportLayer> Layers,
        IReadOnlyDictionary<Guid, List<DrawingStroke>> StrokesByLayer);

    private sealed record ExportLayer(Guid Id, bool IsVisible, double Opacity);
}
