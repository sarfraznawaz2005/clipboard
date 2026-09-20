using System.Text.RegularExpressions;
using System.Windows.Input;
using ClipboardApp.Models;
using ClipboardApp.Services;

namespace ClipboardApp.Views;

public partial class SettingsWindow : Window
{
    const int MinEntries = 10;
    const int MaxEntriesLimit = 5000;

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

        StartWithWindowsBox.IsChecked = s.StartWithWindows;
        StartMinimizedBox.IsChecked = s.StartMinimized;
        MinimizeToTrayBox.IsChecked = s.MinimizeToTray;
        CloseToTrayBox.IsChecked = s.CloseToTray;
    }

    void MaxEntriesBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]$");

    void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    void Save_Click(object sender, RoutedEventArgs e)
    {
        var maxEntries = int.TryParse(MaxEntriesBox.Text, out var n) ? n : _settings.MaxEntries;
        _settings.MaxEntries = Math.Clamp(maxEntries, MinEntries, MaxEntriesLimit);

        _settings.AutoPasteEnabled = AutoPasteBox.IsChecked == true;
        _settings.IgnorePasswordManagerCopies = IgnorePasswordManagerBox.IsChecked == true;

        _settings.StartWithWindows = StartWithWindowsBox.IsChecked == true;
        _settings.StartMinimized = StartMinimizedBox.IsChecked == true;
        _settings.MinimizeToTray = MinimizeToTrayBox.IsChecked == true;
        _settings.CloseToTray = CloseToTrayBox.IsChecked == true;

        StartupRegistration.Apply(_settings.StartWithWindows);
        Storage.SaveSettings(_settings);

        DialogResult = true;
    }
}
