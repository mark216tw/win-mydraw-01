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

    [Fact]
    public void ApplyFavoriteBrush_RestoresToolAndSize()
    {
        FakeSettingsService settings = new();
        MainViewModel viewModel = new(new DrawingDocument(), new BrushCatalog(), settings);
        viewModel.BrushSize = 12;
        FavoriteBrush favorite = viewModel.SaveFavoriteBrush("Restore me");

        viewModel.SelectTool(DrawingTool.Highlighter);
        viewModel.BrushSize = 30;
        viewModel.ApplyFavoriteBrush(favorite);

        Assert.Equal(DrawingTool.Ballpoint, viewModel.SelectedTool);
        Assert.Equal(12, viewModel.BrushSize);
    }

    [Fact]
    public void RecentProjects_AreOrderedLimitedAndPruned()
    {
        FakeSettingsService settings = new();
        MainViewModel viewModel = new(new DrawingDocument(), new BrushCatalog(), settings);
        List<string> files = [];
        try
        {
            for (int index = 0; index < 12; index++)
            {
                string path = Path.Combine(Path.GetTempPath(), $"mydraw-recent-{Guid.NewGuid():N}.mydraw");
                File.WriteAllText(path, "test");
                files.Add(path);
                viewModel.AddRecentProject(path);
            }

            Assert.Equal(10, viewModel.RecentProjects.Count);
            Assert.Equal(files[^1], viewModel.RecentProjects[0]);
            Assert.DoesNotContain(files[0], viewModel.RecentProjects);

            File.Delete(files[^1]);
            viewModel.PruneMissingRecentProjects();
            Assert.Equal(9, viewModel.RecentProjects.Count);

            viewModel.ClearRecentProjects();
            Assert.Empty(viewModel.RecentProjects);
            Assert.Empty(settings.Settings.RecentProjects);
        }
        finally
        {
            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
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
