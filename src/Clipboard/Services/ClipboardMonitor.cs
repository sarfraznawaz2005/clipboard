using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ClipboardApp.Models;
using WpfClipboard = System.Windows.Clipboard;
using WpfDataFormats = System.Windows.DataFormats;
using WpfIDataObject = System.Windows.IDataObject;

namespace ClipboardApp.Services;

// Watches the system clipboard via WM_CLIPBOARDUPDATE (delivered to a hidden message-only
// window) and turns each new clipboard write into a ClipboardEntry.
public sealed class ClipboardMonitor : IDisposable
{
    const int WM_CLIPBOARDUPDATE = 0x031D;
    static readonly IntPtr HWND_MESSAGE = new(-3);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    readonly AppSettings _settings;
    HwndSource? _source;
    bool _suppressNext;

    public event Action<ClipboardEntry>? EntryCaptured;

    public ClipboardMonitor(AppSettings settings) => _settings = settings;

    public void Start()
    {
        var parameters = new HwndSourceParameters("ClipboardMonitorWindow")
        {
            WindowStyle = 0,
            Width = 0,
            Height = 0,
            ParentWindow = HWND_MESSAGE,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        AddClipboardFormatListener(_source.Handle);
    }

    // Call right before the app itself writes to the clipboard (e.g. copying a history row
    // back), so that write isn't re-captured as a brand-new entry.
    public void SuppressNextChange() => _suppressNext = true;

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            if (_suppressNext) _suppressNext = false;
            else TryCapture();
        }
        return IntPtr.Zero;
    }

    void TryCapture()
    {
        WpfIDataObject? data;
        try { data = WpfClipboard.GetDataObject(); }
        catch { return; } // Another app can hold the clipboard open briefly right after writing to it.

        if (data is null) return;

        // The convention 1Password, Bitwarden, KeePass and Windows' own credential UI use to
        // keep copied secrets out of clipboard history tools.
        if (_settings.IgnorePasswordManagerCopies &&
            data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing"))
            return;

        var entry = BuildEntry(data);
        if (entry is not null) EntryCaptured?.Invoke(entry);
    }

    static ClipboardEntry? BuildEntry(WpfIDataObject data)
    {
        if (data.GetDataPresent(WpfDataFormats.Bitmap) &&
            data.GetData(WpfDataFormats.Bitmap) is BitmapSource bmp)
            return BuildImageEntry(bmp);

        if (data.GetDataPresent(WpfDataFormats.Html) &&
            data.GetData(WpfDataFormats.Html) is string html && !string.IsNullOrWhiteSpace(html))
        {
            var text = GetPlainText(data) ?? StripHtml(html);
            return new ClipboardEntry { Kind = ClipboardEntryKind.Html, Html = html, PlainText = text };
        }

        if (data.GetDataPresent(WpfDataFormats.Rtf) &&
            data.GetData(WpfDataFormats.Rtf) is string rtf && !string.IsNullOrWhiteSpace(rtf))
        {
            return new ClipboardEntry { Kind = ClipboardEntryKind.Rtf, Rtf = rtf, PlainText = GetPlainText(data) ?? "" };
        }

        var plain = GetPlainText(data);
        if (!string.IsNullOrEmpty(plain))
            return new ClipboardEntry { Kind = ClipboardEntryKind.Text, PlainText = plain };

        return null;
    }

    static string? GetPlainText(WpfIDataObject data)
    {
        if (data.GetDataPresent(WpfDataFormats.UnicodeText) && data.GetData(WpfDataFormats.UnicodeText) is string u)
            return u;
        if (data.GetDataPresent(WpfDataFormats.Text) && data.GetData(WpfDataFormats.Text) is string t)
            return t;
        return null;
    }

    static ClipboardEntry BuildImageEntry(BitmapSource bmp)
    {
        Directory.CreateDirectory(Storage.ImagesDir);
        var fileName = $"{Guid.NewGuid():N}.png";
        var path = Path.Combine(Storage.ImagesDir, fileName);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using (var fs = new FileStream(path, FileMode.Create))
            encoder.Save(fs);

        return new ClipboardEntry
        {
            Kind = ClipboardEntryKind.Image,
            ImageFile = fileName,
            ImageWidth = bmp.PixelWidth,
            ImageHeight = bmp.PixelHeight,
        };
    }

    // Small tag stripper used only to derive search/preview text when a source provides
    // HTML but no plain-text fallback - not meant to be an exact HTML-to-text conversion.
    static string StripHtml(string html)
    {
        var chars = new List<char>(html.Length);
        bool inTag = false;
        foreach (var c in html)
        {
            if (c == '<') inTag = true;
            else if (c == '>') inTag = false;
            else if (!inTag) chars.Add(c);
        }
        return new string(chars.ToArray()).Trim();
    }

    public void Dispose()
    {
        if (_source is null) return;
        RemoveClipboardFormatListener(_source.Handle);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
