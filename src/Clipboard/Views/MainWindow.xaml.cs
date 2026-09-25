using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClipboardApp.Models;
using ClipboardApp.Services;

namespace ClipboardApp.Views;

public partial class MainWindow : Window
{
    readonly ClipboardStore _store;
    readonly AppSettings _settings;
    readonly ClipboardMonitor _monitor;

    readonly ObservableCollection<ClipboardEntryRow> _rows = new();
    readonly Dictionary<Guid, ClipboardEntryRow> _rowsById = new();
    readonly DispatcherTimer _relativeTimeTimer;
    string _search = "";
    IntPtr _previousForeground;
    bool _childDialogOpen;

    public event Action? ExitRequested;

    public MainWindow(ClipboardStore store, AppSettings settings, ClipboardMonitor monitor)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
        WindowStyleHelper.DisableMaximize(this);

        _store = store;
        _settings = settings;
        _monitor = monitor;

        List.ItemsSource = _rows;

        _store.Entries.CollectionChanged += OnStoreChanged;
        // TogglePin (fired on pin/unpin) doesn't touch the collection itself, so
        // CollectionChanged never fires for it - rebuild explicitly to re-sort pinned-first.
        _store.Changed += RebuildVisibleRows;
        foreach (var e in _store.Entries) TrackRow(e);
        RebuildVisibleRows();

        RestorePlacement();
        StateChanged += (_, _) => OnStateChangedHandler();
        Deactivated += (_, _) => OnDeactivatedHandler();
        Closing += OnClosingHandler;

        _relativeTimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _relativeTimeTimer.Tick += (_, _) => { foreach (var row in _rows) row.Refresh(); };
        _relativeTimeTimer.Start();
    }

    void OnStoreChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _rowsById.Clear();
            foreach (var entry in _store.Entries) TrackRow(entry);
        }
        else
        {
            if (e.OldItems is not null)
                foreach (ClipboardEntry entry in e.OldItems) _rowsById.Remove(entry.Id);
            if (e.NewItems is not null)
                foreach (ClipboardEntry entry in e.NewItems) TrackRow(entry);
        }
        RebuildVisibleRows();
    }

    void TrackRow(ClipboardEntry entry) => _rowsById[entry.Id] = new ClipboardEntryRow(entry);

    void RebuildVisibleRows()
    {
        _rows.Clear();

        IEnumerable<ClipboardEntry> visible = _store.Entries;
        if (!string.IsNullOrWhiteSpace(_search))
        {
            var q = _search.Trim();
            visible = visible.Where(e => e.PlainText.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        // Entries is already newest-first (AddNew inserts at index 0). OrderByDescending is
        // stable, so this only needs to float pinned entries to the top without disturbing
        // the newest-first order within each group.
        foreach (var entry in visible.OrderByDescending(e => e.IsPinned))
        {
            var row = _rowsById[entry.Id];
            row.RaiseAll();
            _rows.Add(row);
        }

        EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateSearchPlaceholder();

        // The top entry is always the most relevant one (newest, or the top search match) -
        // keep it pre-selected so Enter/arrow keys work immediately without a mouse click.
        List.SelectedIndex = _rows.Count > 0 ? 0 : -1;

        var total = _store.Entries.Count;
        CountText.Text = total == 1 ? "1 item" : $"{total} items";
    }

    void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text;
        RebuildVisibleRows();
    }

    void SearchBox_FocusChanged(object sender, RoutedEventArgs e) => UpdateSearchPlaceholder();

    // The placeholder is drawn on top of the text box so it can sit over an empty caret
    // position - which also means it hides the caret itself unless we duck out on focus.
    void UpdateSearchPlaceholder() =>
        SearchPlaceholder.Visibility = SearchBox.Text.Length == 0 && !SearchBox.IsFocused
            ? Visibility.Visible
            : Visibility.Collapsed;

    void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;

        var current = source;
        while (current is not null)
        {
            // Let the pin/delete buttons handle their own click.
            if (current is System.Windows.Controls.Primitives.ButtonBase) return;
            if (current is ListBoxItem { DataContext: ClipboardEntryRow row })
            {
                CopyAndClose(row.Model);
                return;
            }
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
    }

    void CopyAndClose(ClipboardEntry entry)
    {
        CopyToClipboard(entry);
        var target = _previousForeground;
        Hide();

        if (_settings.AutoPasteEnabled)
        {
            // Let Hide() finish restoring the previous window before we simulate the
            // keystroke, otherwise Ctrl+V can land on our own (closing) window instead.
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => AutoPaste.RestoreAndPaste(target));
        }
    }

    void CopyToClipboard(ClipboardEntry entry)
    {
        _monitor.SuppressNextChange();

        var data = new System.Windows.DataObject();
        switch (entry.Kind)
        {
            case ClipboardEntryKind.Text:
                data.SetText(entry.PlainText);
                break;

            case ClipboardEntryKind.Html:
                data.SetData(DataFormats.Html, entry.Html);
                if (!string.IsNullOrEmpty(entry.PlainText)) data.SetText(entry.PlainText);
                break;

            case ClipboardEntryKind.Rtf:
                data.SetData(DataFormats.Rtf, entry.Rtf);
                if (!string.IsNullOrEmpty(entry.PlainText)) data.SetText(entry.PlainText);
                break;

            case ClipboardEntryKind.Image:
                var image = LoadFullImage(entry);
                if (image is not null) data.SetImage(image);
                break;
        }

        SetClipboardWithRetry(data);
    }

    // Windows briefly locks the clipboard right after any process (including our own
    // monitor) touches it, which makes Clipboard.SetDataObject throw a COMException at
    // random - this was crashing the app on click. Back off and retry instead of throwing.
    static void SetClipboardWithRetry(System.Windows.DataObject data)
    {
        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetDataObject(data, copy: true);
                return;
            }
            catch (System.Runtime.InteropServices.COMException) when (attempt < maxAttempts)
            {
                System.Threading.Thread.Sleep(30);
            }
        }
    }

    // Loads the full-resolution PNG from disk - row.ImageSource is downsampled for the list
    // thumbnail and must never be what gets pasted back onto the clipboard.
    static BitmapImage? LoadFullImage(ClipboardEntry entry)
    {
        if (string.IsNullOrEmpty(entry.ImageFile)) return null;
        var path = Path.Combine(Storage.ImagesDir, entry.ImageFile);
        if (!File.Exists(path)) return null;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: ClipboardEntryRow row }) return;
        _store.TogglePin(row.Model);
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: ClipboardEntryRow row }) return;
        _store.Remove(row.Model);
    }

    void ClearHistory_Click(object sender, RoutedEventArgs e) => ClearHistoryFromTray();

    public void ClearHistoryFromTray()
    {
        const string message = "Clear all clipboard history? Pinned items will be kept.";

        _childDialogOpen = true;
        var result = IsVisible
            ? MessageBox.Show(this, message, "Clear history", MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(message, "Clear history", MessageBoxButton.YesNo, MessageBoxImage.Question);
        _childDialogOpen = false;

        if (result == MessageBoxResult.Yes) _store.ClearHistory();
    }

    void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settings) { Owner = this };

        _childDialogOpen = true;
        var saved = settingsWindow.ShowDialog() == true;
        _childDialogOpen = false;

        if (saved)
        {
            _store.EnforceMaxEntries();
            _store.EnforceMaxAge();
        }
    }

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handled at the window level (tunneling) so Up/Down/Enter work no matter which
        // control has focus - normally the search box, which would otherwise eat them.
        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;

            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;

            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;

            case Key.Enter:
                if (List.SelectedItem is ClipboardEntryRow row) CopyAndClose(row.Model);
                e.Handled = true;
                break;
        }
    }

    void MoveSelection(int delta)
    {
        if (_rows.Count == 0) return;
        var current = List.SelectedIndex < 0 ? 0 : List.SelectedIndex;
        List.SelectedIndex = Math.Clamp(current + delta, 0, _rows.Count - 1);
        List.ScrollIntoView(List.SelectedItem);
    }

    // Forces the window's handle, layout, and first render to happen once, right away,
    // instead of on the user's first Ctrl+Shift+V - that first real Show() would otherwise
    // be noticeably slower than every one after it. Moved off-screen (rather than just
    // Opacity=0) so there's no risk of a one-frame flash at the real position before
    // DWM picks up the opacity change.
    public void WarmUp()
    {
        var realLeft = Left;
        var realTop = Top;

        Left = -32000;
        Top = -32000;
        Show();
        Hide();

        Left = realLeft;
        Top = realTop;
    }

    public void ShowAndActivate()
    {
        // Skip re-capturing our own window as the "previous" foreground window if the
        // hotkey is pressed again while we're already showing.
        var foreground = AutoPaste.CaptureForegroundWindow();
        var self = new WindowInteropHelper(this).Handle;
        if (foreground != self) _previousForeground = foreground;

        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;

        SearchBox.Focus();
        SearchBox.SelectAll();

        // If the cursor is already resting over the auto-selected first row when the popup
        // appears, WPF never sees a real mouse-move into that row, so it never starts hover
        // tracking (and its tooltip) for it - only for rows the cursor moves onto afterward.
        // Synchronize forces WPF to re-check hover state against the cursor's current
        // position without needing an actual move.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => System.Windows.Input.Mouse.Synchronize());
    }

    void OnStateChangedHandler()
    {
        if (WindowState == WindowState.Minimized && _settings.MinimizeToTray)
            Hide();
    }

    // Clicking outside the popup closes it to the tray, like Windows' own clipboard history
    // (Win+V). Guarded by _childDialogOpen so opening Settings, or the clear-history confirm,
    // doesn't hide this window out from under its own owned dialog.
    void OnDeactivatedHandler()
    {
        if (_childDialogOpen || !IsVisible) return;
        Hide();
    }

    void OnClosingHandler(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (App.IsExiting) { SavePlacement(); return; }

        if (_settings.CloseToTray)
        {
            e.Cancel = true;
            SavePlacement();
            Hide();
        }
        else
        {
            // With "close to tray" off, the X should fully quit - not just close this
            // window while ShutdownMode=OnExplicitShutdown leaves the app (and tray icon)
            // running invisibly in the background. Route through the real exit path instead.
            e.Cancel = true;
            SavePlacement();
            ExitRequested?.Invoke();
        }
    }

    void RestorePlacement()
    {
        var w = _settings.Window;
        Width = w.Width;
        Height = w.Height;

        if (double.IsNaN(w.Left) || double.IsNaN(w.Top))
        {
            // Computed explicitly (rather than WindowStartupLocation=CenterScreen) because
            // that property only ever applies on a window's very first Show() call - and
            // WarmUp() below needs to be able to make that first Show() happen off-screen
            // without permanently losing the centered position.
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Top + (workArea.Height - Height) / 2;
        }
        else
        {
            Left = w.Left;
            Top = w.Top;
        }

        if (w.Maximized) WindowState = WindowState.Maximized;
    }

    public void SavePlacement()
    {
        var w = _settings.Window;
        w.Maximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            w.Left = Left;
            w.Top = Top;
            w.Width = Width;
            w.Height = Height;
        }
        Storage.SaveSettings(_settings);
    }
}
