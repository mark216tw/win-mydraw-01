using CommunityToolkit.Mvvm.ComponentModel;

namespace Com.StudentTools.MyDraw.Models;

public sealed class DrawingLayer : ObservableObject
{
    private bool _isVisible = true;
    private bool _isLocked;
    private double _opacity = 1;
    private string _name;

    public DrawingLayer(string name)
        : this(Guid.NewGuid(), name)
    {
    }

    public DrawingLayer(Guid id, string name)
    {
        Id = id;
        _name = name;
    }

    public Guid Id { get; }

    public string Name { get => _name; set => SetProperty(ref _name, value); }

    public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }

    public bool IsLocked { get => _isLocked; set => SetProperty(ref _isLocked, value); }

    public double Opacity { get => _opacity; set => SetProperty(ref _opacity, Math.Clamp(value, 0, 1)); }
}
