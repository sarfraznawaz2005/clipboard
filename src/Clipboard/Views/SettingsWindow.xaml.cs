using System.Text.RegularExpressions;
using System.Windows.Input;
using ClipboardApp.Models;
using ClipboardApp.Services;

namespace ClipboardApp.Views;

public partial class SettingsWindow : Window
{
    const int MinEntries = 10;
    const int MaxEntriesLimit = 5000;
    const int MinAutoClearDays = 1;
    const int MaxAutoClearDays = 3650;

    readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
        _settings = settings;

        LoadFrom(settings);
    }

    void LoadFrom(AppSettings s)
    {
        MaxEntriesBox.Text = s.MaxEntries.ToString();
        AutoPasteBox.IsChecked = s.AutoPasteEnabled;
        IgnorePasswordManagerBox.IsChecked = s.IgnorePasswordManagerCopies;

        AutoClearBox.IsChecked = s.AutoClearEnabled;
        AutoClearDaysBox.Text = s.AutoClearDays.ToString();
        AutoClearDaysBox.IsEnabled = s.AutoClearEnabled;

        StartWithWindowsBox.IsChecked = s.StartWithWindows;
        StartMinimizedBox.IsChecked = s.StartMinimized;
        MinimizeToTrayBox.IsChecked = s.MinimizeToTray;
        CloseToTrayBox.IsChecked = s.CloseToTray;
    }

    void MaxEntriesBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]$");

    void AutoClearDaysBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]$");

    void AutoClearBox_Toggled(object sender, RoutedEventArgs e) =>
        AutoClearDaysBox.IsEnabled = AutoClearBox.IsChecked == true;

    void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    void Save_Click(object sender, RoutedEventArgs e)
    {
        var maxEntries = int.TryParse(MaxEntriesBox.Text, out var n) ? n : _settings.MaxEntries;
        _settings.MaxEntries = Math.Clamp(maxEntries, MinEntries, MaxEntriesLimit);

        _settings.AutoPasteEnabled = AutoPasteBox.IsChecked == true;
        _settings.IgnorePasswordManagerCopies = IgnorePasswordManagerBox.IsChecked == true;

        var autoClearDays = int.TryParse(AutoClearDaysBox.Text, out var d) ? d : _settings.AutoClearDays;
        _settings.AutoClearEnabled = AutoClearBox.IsChecked == true;
        _settings.AutoClearDays = Math.Clamp(autoClearDays, MinAutoClearDays, MaxAutoClearDays);

        _settings.StartWithWindows = StartWithWindowsBox.IsChecked == true;
        _settings.StartMinimized = StartMinimizedBox.IsChecked == true;
        _settings.MinimizeToTray = MinimizeToTrayBox.IsChecked == true;
        _settings.CloseToTray = CloseToTrayBox.IsChecked == true;

        // Debug builds must never touch the registry entry - Environment.ProcessPath in a
        // `dotnet run` session points at the bin\Debug apphost, not the real published exe,
        // so applying it here would silently overwrite (or delete) a real Start-with-Windows
        // entry set up by the published build. The checkbox state still saves to settings.json
        // either way; only the actual registry write is skipped.
#if !DEBUG
        StartupRegistration.Apply(_settings.StartWithWindows);
#endif
        Storage.SaveSettings(_settings);

        DialogResult = true;
    }
}
