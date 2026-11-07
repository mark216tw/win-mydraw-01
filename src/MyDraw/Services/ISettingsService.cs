using Com.StudentTools.MyDraw.Models;

namespace Com.StudentTools.MyDraw.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }

    void Save();

    void SaveDebounced();

    void Flush();
}
