namespace Com.StudentTools.MyDraw.Services;

public sealed record RecoveryInfo(
    Guid SessionId,
    string RecoveryPath,
    string MetadataPath,
    string? OriginalPath,
    DateTimeOffset SavedAt);
