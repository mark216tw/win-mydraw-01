using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace Com.StudentTools.MyDraw.Helpers;

public static class ProjectActivation
{
    private const string ProjectExtension = ".mydraw";

    public static string? GetProjectPath(IEnumerable<string> arguments)
    {
        foreach (string argument in arguments)
        {
            string candidate = argument.Trim().Trim('"');
            if (candidate.Length == 0 ||
                !candidate.EndsWith(ProjectExtension, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(candidate))
            {
                continue;
            }

            return Path.GetFullPath(candidate);
        }

        return null;
    }

    public static string? GetProjectPath(AppActivationArguments args)
    {
        if (args.Kind == ExtendedActivationKind.Launch &&
            args.Data is ILaunchActivatedEventArgs launch)
        {
            return GetProjectPath([launch.Arguments]);
        }

        return null;
    }
}
