namespace Com.StudentTools.MyDraw.Models;

public sealed class AppSettings
{
    public int SettingsVersion { get; set; }

    public int WindowX { get; set; }

    public int WindowY { get; set; }

    public int WindowWidth { get; set; } = 1280;

    public int WindowHeight { get; set; } = 800;

    public bool HasWindowPlacement { get; set; }

    public bool IsMaximized { get; set; }

    public DisplayMode DisplayMode { get; set; } = DisplayMode.System;

    public ColorTheme ColorTheme { get; set; } = ColorTheme.EyeCare;

    public bool IsToolbarOnRight { get; set; }

    public List<BrushToolSettings> BrushToolSettings { get; set; } = [];

    public List<FavoriteBrush> FavoriteBrushes { get; set; } = [];
}
