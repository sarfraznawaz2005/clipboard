using System.Collections.ObjectModel;
using ClipboardApp.Models;

namespace ClipboardApp.Services;

public sealed class ClipboardStore
{
    readonly AppSettings _settings;

    public ObservableCollection<ClipboardEntry> Entries { get; } = new();

    // Fires on changes that don't touch the collection itself (pin toggle) so the UI can
    // refresh a row without a full rebuild.
    public event Action? Changed;

    public ClipboardStore(AppSettings settings) => _settings = settings;

    public void Load()
    {
        var file = Storage.LoadEntries();
        Entries.Clear();
        foreach (var e in file.Entries) Entries.Add(e);
    }

    public void AddNew(ClipboardEntry entry)
    {
        var existing = Entries.FirstOrDefault(e => ContentEquals(e, entry));
        if (existing is not null)
        {
            // Re-copying something already in history bumps it to the top instead of
            // cluttering the list with an identical second row.
            Entries.Remove(existing);
            existing.CreatedAt = entry.CreatedAt;
            Entries.Insert(0, existing);
            SaveNow();
            return;
        }

        Entries.Insert(0, entry);
        Trim();
        SaveNow();
    }

    public void Remove(ClipboardEntry entry)
    {
        Entries.Remove(entry);
        DeleteImageFile(entry);
        SaveNow();
    }

    public void TogglePin(ClipboardEntry entry)
    {
        entry.IsPinned = !entry.IsPinned;
        SaveNow();
        Changed?.Invoke();
    }

    public void ClearHistory()
    {
        // Pinned entries survive "clear" - that's the point of pinning something.
        foreach (var e in Entries.Where(e => !e.IsPinned).ToList())
        {
            Entries.Remove(e);
            DeleteImageFile(e);
        }
        SaveNow();
    }

    // Re-applies the current MaxEntries limit - called after the user lowers it in Settings,
    // so the trim doesn't wait for the next captured entry.
    public void EnforceMaxEntries()
    {
        Trim();
        SaveNow();
    }

    void Trim()
    {
        var unpinned = Entries.Where(e => !e.IsPinned).ToList();
        int overflow = unpinned.Count - Math.Max(0, _settings.MaxEntries);
        if (overflow <= 0) return;

        foreach (var e in unpinned.TakeLast(overflow).ToList())
        {
            Entries.Remove(e);
            DeleteImageFile(e);
        }
    }

    static bool ContentEquals(ClipboardEntry a, ClipboardEntry b)
    {
        if (a.Kind != b.Kind) return false;
        return a.Kind switch
        {
            ClipboardEntryKind.Text => a.PlainText == b.PlainText,
            ClipboardEntryKind.Html => a.Html == b.Html,
            ClipboardEntryKind.Rtf => a.Rtf == b.Rtf,
            // Comparing image bytes isn't worth the cost here - re-copying the same image
            // just adds a fresh row.
            ClipboardEntryKind.Image => false,
            _ => false,
        };
    }

    static void DeleteImageFile(ClipboardEntry e)
    {
        if (string.IsNullOrEmpty(e.ImageFile)) return;
        try { File.Delete(Path.Combine(Storage.ImagesDir, e.ImageFile)); } catch { /* best effort */ }
    }

    public void SaveNow() =>
        Storage.SaveEntries(new ClipboardEntryFile { Entries = Entries.ToList() });
}
