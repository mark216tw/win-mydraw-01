using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Com.StudentTools.MyDraw.Services;

public sealed class FileAssociationService
{
    public const string Extension = ".mydraw";
    private const string ProgId = "MyDraw.Project";
    private const int AssocChangedEvent = 0x08000000;
    private const uint IdListFlag = 0;

    public string ExecutablePath =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "MyDraw.exe");

    public bool IsRegistered()
    {
        using RegistryKey? extension = Registry.CurrentUser.OpenSubKey(
            $@"Software\Classes\{Extension}");
        if (!string.Equals(extension?.GetValue(null) as string, ProgId, StringComparison.Ordinal))
        {
            return false;
        }

        using RegistryKey? command = Registry.CurrentUser.OpenSubKey(
            $@"Software\Classes\{ProgId}\shell\open\command");
        return string.Equals(
            command?.GetValue(null) as string,
            BuildOpenCommand(),
            StringComparison.OrdinalIgnoreCase);
    }

    public void Register(string displayName)
    {
        using RegistryKey classes = Registry.CurrentUser.CreateSubKey(
            @"Software\Classes") ?? throw new InvalidOperationException(
            "Unable to open the current user classes registry key.");
        using RegistryKey extension = classes.CreateSubKey(Extension) ?? throw new InvalidOperationException(
            "Unable to create the file extension registry key.");
        extension.SetValue(null, ProgId);

        using RegistryKey progId = classes.CreateSubKey(ProgId) ?? throw new InvalidOperationException(
            "Unable to create the file type registry key.");
        progId.SetValue(null, displayName);
        progId.SetValue("FriendlyTypeName", displayName);

        using RegistryKey icon = progId.CreateSubKey("DefaultIcon") ?? throw new InvalidOperationException(
            "Unable to create the file icon registry key.");
        icon.SetValue(null, $"\"{ExecutablePath}\",0");

        using RegistryKey command = progId.CreateSubKey(@"shell\open\command") ?? throw new InvalidOperationException(
            "Unable to create the file command registry key.");
        command.SetValue(null, BuildOpenCommand());

        NotifyShell();
    }

    public void Unregister()
    {
        bool removeExtensionKey = false;
        using (RegistryKey? extension = Registry.CurrentUser.OpenSubKey(
            $@"Software\Classes\{Extension}",
            writable: true))
        {
            if (extension is not null &&
                string.Equals(
                    extension.GetValue(null) as string,
                    ProgId,
                    StringComparison.Ordinal))
            {
                extension.DeleteValue(string.Empty, throwOnMissingValue: false);
                removeExtensionKey = extension.ValueCount == 0 && extension.SubKeyCount == 0;
            }
        }

        Registry.CurrentUser.DeleteSubKeyTree(
            $@"Software\Classes\{ProgId}",
            throwOnMissingSubKey: false);
        if (removeExtensionKey)
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Classes\{Extension}",
                throwOnMissingSubKey: false);
        }

        NotifyShell();
    }

    private string BuildOpenCommand() => $"\"{ExecutablePath}\" \"%1\"";

    private static void NotifyShell() =>
        SHChangeNotify(AssocChangedEvent, IdListFlag, IntPtr.Zero, IntPtr.Zero);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(
        int eventId,
        uint flags,
        IntPtr item1,
        IntPtr item2);
}
