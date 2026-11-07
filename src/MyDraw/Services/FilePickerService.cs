using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Com.StudentTools.MyDraw.Services;

public sealed class FilePickerService
{
    public async Task<string?> PickOpenProjectAsync(Window window)
    {
        FileOpenPicker picker = new();
        InitializeWithWindow(picker, window);
        picker.FileTypeFilter.Add(".mydraw");
        StorageFile? file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickSaveProjectAsync(
        Window window,
        string suggestedFileName,
        string projectTypeName)
    {
        FileSavePicker picker = new();
        InitializeWithWindow(picker, window);
        picker.SuggestedFileName = suggestedFileName;
        picker.FileTypeChoices.Add(projectTypeName, [".mydraw"]);
        StorageFile? file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickExportPngAsync(
        Window window,
        string suggestedFileName)
    {
        FileSavePicker picker = new();
        InitializeWithWindow(picker, window);
        picker.SuggestedFileName = suggestedFileName;
        picker.FileTypeChoices.Add("PNG", [".png"]);
        StorageFile? file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private static void InitializeWithWindow(object picker, Window window)
    {
        nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
    }
}
