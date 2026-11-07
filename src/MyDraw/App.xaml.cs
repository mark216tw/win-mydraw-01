using Com.StudentTools.MyDraw.Helpers;
using Com.StudentTools.MyDraw.Services;
using Com.StudentTools.MyDraw.ViewModels;
using Com.StudentTools.MyDraw.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Com.StudentTools.MyDraw;

public partial class App : Application
{
    private readonly IHost _host;
    private AppInstance? _appInstance;
    private ProjectForwardingService? _projectForwarding;
    private Window? _window;

    public App()
    {
        InitializeComponent();

        UnhandledException += (_, args) =>
            CrashLogger.Log("Application.UnhandledException", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                CrashLogger.Log("AppDomain.UnhandledException", exception);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLogger.Log("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
#if DEBUG
                logging.AddDebug();
#endif
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton<ISettingsService, JsonSettingsService>();
                services.AddSingleton<ThemeService>();
                services.AddSingleton<BrushCatalog>();
                services.AddSingleton<FileAssociationService>();
                services.AddSingleton<IProjectFileService, ProjectFileService>();
                services.AddSingleton<DrawingDocument>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _appInstance = AppInstance.FindOrRegisterForKey("MyDraw.Main");
        if (!_appInstance.IsCurrent)
        {
            string? forwardedPath = ProjectActivation.GetProjectPath(
                new[] { args.Arguments }.Concat(Environment.GetCommandLineArgs().Skip(1)));
            if (forwardedPath is not null)
            {
                ProjectForwardingService.TrySend(forwardedPath);
            }

            await _appInstance.RedirectActivationToAsync(
                AppInstance.GetCurrent().GetActivatedEventArgs());
            Exit();
            return;
        }

        _appInstance.Activated += AppInstance_Activated;
        _host.Start();
        _window = _host.Services.GetRequiredService<MainWindow>();
        _window.Activate();
        _projectForwarding = new ProjectForwardingService();
        _projectForwarding.ProjectPathReceived += ProjectForwarding_ProjectPathReceived;
        _projectForwarding.Start();

        string? projectPath = ProjectActivation.GetProjectPath(
            new[] { args.Arguments }.Concat(Environment.GetCommandLineArgs().Skip(1)));
        if (projectPath is not null && _window is MainWindow mainWindow)
        {
            mainWindow.QueueOpenProject(projectPath);
        }
    }

    private void ProjectForwarding_ProjectPathReceived(object? sender, string filePath)
    {
        if (_window is MainWindow mainWindow)
        {
            mainWindow.QueueOpenProject(filePath);
        }
    }

    private void AppInstance_Activated(object? sender, AppActivationArguments args)
    {
        if (_window is null)
        {
            return;
        }

        _window.DispatcherQueue.TryEnqueue(() =>
        {
            if (_window.AppWindow.Presenter is OverlappedPresenter
                {
                    State: OverlappedPresenterState.Minimized
                } presenter)
            {
                presenter.Restore();
            }

            _window.Activate();
        });
    }
}
