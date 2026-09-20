using System.Windows;
using ClipboardApp.Models;
using ClipboardApp.Services;
using ClipboardApp.Views;

namespace ClipboardApp;

public partial class App : System.Windows.Application
{
    public static bool IsExiting { get; private set; }

    SingleInstance? _singleInstance;
    TrayIcon? _tray;
    ClipboardStore? _store;
    ClipboardMonitor? _monitor;
    GlobalHotkey? _hotkey;
    AppSettings? _settings;
    MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Last-resort safety net: this app is meant to sit quietly in the tray at all times,
        // so one unexpected exception (e.g. a transient clipboard/COM error we didn't already
        // catch) shouldn't take the whole thing down. Logged, not silently swallowed.
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipboard", "crash.log");
                File.AppendAllText(logPath, $"{DateTime.Now:O}  {args.Exception}\n\n");
            }
            catch { /* best effort */ }
            args.Handled = true;
        };

        _singleInstance = new SingleInstance();
        _singleInstance.Acquire();
        if (!_singleInstance.IsFirstInstance)
        {
            Shutdown(0);
            return;
        }

        bool startInTray = e.Args.Contains("--tray");

        _settings = Storage.LoadSettings();
        _store = new ClipboardStore(_settings);
        _store.Load();

        _monitor = new ClipboardMonitor(_settings);
        _monitor.EntryCaptured += entry => Dispatcher.Invoke(() => _store.AddNew(entry));
        _monitor.Start();

        _mainWindow = new MainWindow(_store, _settings, _monitor);

        _hotkey = new GlobalHotkey();
        _hotkey.Pressed += () => Dispatcher.Invoke(() => _mainWindow.ShowAndActivate());
        if (!_hotkey.Register())
            MessageBox.Show(
                "Ctrl+Shift+V is already used by another application, so the Clipboard shortcut could not be registered. " +
                "You can still open Clipboard from the tray icon.",
                "Clipboard", MessageBoxButton.OK, MessageBoxImage.Warning);

        _tray = new TrayIcon();
        _tray.OpenRequested += () => _mainWindow.ShowAndActivate();
        _tray.ClearHistoryRequested += () => _mainWindow.ClearHistoryFromTray();
        _tray.SettingsRequested += () =>
        {
            var settingsWindow = new SettingsWindow(_settings) { Owner = _mainWindow };
            settingsWindow.ShowDialog();
        };
        _tray.ExitRequested += () => ExitApplication();
        _mainWindow.ExitRequested += () => ExitApplication();

#if !DEBUG
        if (_settings.StartWithWindows) StartupRegistration.Apply(true);
#endif

        _singleInstance.ListenForActivation(() => Dispatcher.Invoke(() => _mainWindow.ShowAndActivate()));

        if (startInTray || _settings.StartMinimized)
            _mainWindow.WarmUp();
        else
            _mainWindow.Show();
    }

    void ExitApplication()
    {
        IsExiting = true;
        _mainWindow?.SavePlacement();
        _store?.SaveNow();
        _hotkey?.Dispose();
        _monitor?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        Shutdown(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _monitor?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
