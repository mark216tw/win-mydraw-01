using Com.StudentTools.MyDraw.Models;
using Microsoft.UI.Xaml;

namespace Com.StudentTools.MyDraw.Services;

public sealed class ThemeService
{
    private readonly ISettingsService _settingsService;
    private ResourceDictionary? _paletteDictionary;

    public ThemeService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void Apply(FrameworkElement root, DisplayMode displayMode, ColorTheme colorTheme)
    {
        root.RequestedTheme = displayMode switch
        {
            DisplayMode.Light => ElementTheme.Light,
            DisplayMode.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        if (_paletteDictionary is not null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(_paletteDictionary);
        }

        _paletteDictionary = new ResourceDictionary
        {
            Source = new Uri($"ms-appx:///Styles/Themes/{colorTheme}.xaml")
        };
        Application.Current.Resources.MergedDictionaries.Add(_paletteDictionary);

        _settingsService.Settings.DisplayMode = displayMode;
        _settingsService.Settings.ColorTheme = colorTheme;
        _settingsService.Save();
    }
}
