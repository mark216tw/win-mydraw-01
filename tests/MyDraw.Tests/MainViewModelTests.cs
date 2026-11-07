using Com.StudentTools.MyDraw.Models;
using Com.StudentTools.MyDraw.Services;
using Com.StudentTools.MyDraw.ViewModels;
using Xunit;

namespace Com.StudentTools.MyDraw.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void MoveFavoriteBrush_ReordersAndPersistsFavoriteList()
    {
        FakeSettingsService settings = new();
        MainViewModel viewModel = new(new DrawingDocument(), new BrushCatalog(), settings);
        FavoriteBrush first = viewModel.SaveFavoriteBrush("First");
        FavoriteBrush second = viewModel.SaveFavoriteBrush("Second");

        viewModel.MoveFavoriteBrush(second.Id, -1);

        Assert.Equal(second.Id, viewModel.FavoriteBrushes[0].Id);
        Assert.Equal(first.Id, viewModel.FavoriteBrushes[1].Id);
        Assert.Equal(second.Id, settings.Settings.FavoriteBrushes[0].Id);
        Assert.Equal(first.Id, settings.Settings.FavoriteBrushes[1].Id);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();

        public void Save()
        {
        }

        public void SaveDebounced()
        {
        }

        public void Flush()
        {
        }
    }
}
