namespace Com.StudentTools.MyDraw.Services;

public interface IProjectFileService
{
    Task SaveAsync(
        DrawingDocument document,
        string filePath,
        CancellationToken cancellationToken = default);

    Task<ProjectLoadResult> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
