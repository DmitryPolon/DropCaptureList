using System.IO;
using System.Windows;
using System.Windows.Threading;
using DropCaptureList.Windows.Models;
using DropCaptureList.Windows.Services;
using DropCaptureList.Windows.ViewModels;
using DropCaptureList.Windows.Views;

namespace DropCaptureList.Windows;

public partial class App : Application
{
    private readonly IIdentityService _identity;
    private readonly ICaptureService _captures;
    private readonly ProtectedSessionStore _sessions;
    private readonly StorageModeClient _storageMode;
    private readonly ApiBackend _api;
    private readonly ExcelSelectionCapture _excel = new();

    public App()
    {
        var dataDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DropCaptureList");
        Directory.CreateDirectory(dataDir);
        _sessions = new ProtectedSessionStore(System.IO.Path.Combine(dataDir, "session.bin"));

        var apiBase = AppConfiguration.LoadApiBase();
        if (string.IsNullOrWhiteSpace(apiBase))
        {
            throw new InvalidOperationException("Set ApiBase in appsettings.Local.json (the hosted API URL).");
        }

        _storageMode = new StorageModeClient(apiBase);
        _api = new ApiBackend(apiBase);
        _identity = _api;
        _captures = _api;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var session = _sessions.Load();
        if (session is not null && session.UserId != Guid.Empty && !string.IsNullOrWhiteSpace(session.Email))
        {
            ShowMain(session);
        }
        else
        {
            ShowLogin();
        }
    }

    private void ShowLogin()
    {
        var loginVm = new LoginViewModel(_identity, remembered: _sessions.Load());
        var login = new LoginWindow(loginVm);
        loginVm.SignedIn += (_, _) =>
        {
            if (loginVm.SignedInSession is null)
            {
                return;
            }

            _sessions.Save(loginVm.SignedInSession);
            ShowMain(loginVm.SignedInSession);
            login.Close();
        };

        MainWindow = login;
        login.Closed += OnShellClosed;
        login.Show();
    }

    private void ShowMain(UserSession session)
    {
        _api.LastEmail = session.Email;
        _api.LastHousehold = session.TenantName;

        var mainVm = new MainViewModel(session, _captures, _excel, _sessions, _identity, _storageMode, _api);
        var main = new MainWindow(mainVm);
        mainVm.SignedOut += (_, _) =>
        {
            ShowLogin();
            main.Close();
        };

        MainWindow = main;
        main.Closed += OnShellClosed;
        main.Show();
    }

    private void OnShellClosed(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.Closed -= OnShellClosed;
        }

        if (Windows.Cast<Window>().All(w => !w.IsVisible))
        {
            Shutdown();
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "DropCaptureList", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
