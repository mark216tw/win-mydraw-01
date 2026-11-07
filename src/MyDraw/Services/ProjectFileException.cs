namespace Com.StudentTools.MyDraw.Services;

public sealed class ProjectFileException : Exception
{
    public ProjectFileException(string resourceKey, string? detail = null)
        : base(detail ?? resourceKey)
    {
        ResourceKey = resourceKey;
    }

    public string ResourceKey { get; }
}
