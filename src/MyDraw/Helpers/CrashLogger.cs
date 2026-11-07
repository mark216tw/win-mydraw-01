namespace Com.StudentTools.MyDraw.Helpers;

public static class CrashLogger
{
    private static readonly object Sync = new();

    public static string LogDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyDraw",
            "logs");

    public static void Log(string source, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            string path = Path.Combine(LogDirectory, $"crash-{DateTime.Now:yyyyMMdd}.log");
            string message =
                $"[{DateTimeOffset.Now:O}] {source}{Environment.NewLine}" +
                $"{exception}{Environment.NewLine}{Environment.NewLine}";
            lock (Sync)
            {
                File.AppendAllText(path, message);
            }
        }
        catch (Exception loggingException) when (loggingException is IOException or UnauthorizedAccessException)
        {
        }
    }
}
