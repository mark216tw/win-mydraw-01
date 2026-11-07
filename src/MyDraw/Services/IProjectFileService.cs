using Com.StudentTools.MyDraw.Models;

namespace Com.StudentTools.MyDraw.Services;

public interface IProjectFileService
{
    Task SaveAsync(
        DrawingDocument document,
        string filePath,
        DrawingStroke? currentStroke = null,
        bool createBackup = true,
        CancellationToken cancellationToken = default);

    Task<ProjectLoadResult> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
