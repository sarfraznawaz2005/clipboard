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
            // Re-copying something already in history - even as a different clipboard format
            // (plain text immediately followed by HTML for the same copy) - bumps it to the
            // top instead of cluttering the list with a second row for the same copy. Keep
            // whichever format is richer rather than always keeping the older one.
            Entries.Remove(existing);
            var kept = Rank(entry.Kind) >= Rank(existing.Kind) ? entry : existing;
            kept.CreatedAt = entry.CreatedAt;
            kept.IsPinned = existing.IsPinned;
            Entries.Insert(0, kept);
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

    // Deletes unpinned entries older than AutoClearDays, when that setting is on. Called at
    // startup, after Settings is saved, and on a recurring timer so a long-running instance
    // still clears entries without needing a restart.
    public void EnforceMaxAge()
    {
        if (!_settings.AutoClearEnabled) return;

        var cutoff = DateTime.Now - TimeSpan.FromDays(Math.Max(0, _settings.AutoClearDays));
        var expired = Entries.Where(e => !e.IsPinned && e.CreatedAt < cutoff).ToList();
        if (expired.Count == 0) return;

        foreach (var e in expired)
        {
            Entries.Remove(e);
            DeleteImageFile(e);
        }
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

    // Text, HTML and RTF entries are treated as the same copy when their plain-text fallback
    // matches - that's what lets a Text+HTML pair from one copy collapse into a single row.
    // Comparing image bytes isn't worth the cost here - re-copying the same image just adds
    // a fresh row.
    static bool ContentEquals(ClipboardEntry a, ClipboardEntry b)
    {
        if (a.Kind == ClipboardEntryKind.Image || b.Kind == ClipboardEntryKind.Image) return false;
        return !string.IsNullOrEmpty(a.PlainText) && a.PlainText == b.PlainText;
    }

    static int Rank(ClipboardEntryKind kind) => kind switch
    {
        ClipboardEntryKind.Html => 2,
        ClipboardEntryKind.Rtf => 2,
        ClipboardEntryKind.Text => 1,
        _ => 0,
    };

    static void DeleteImageFile(ClipboardEntry e)
    {
        if (string.IsNullOrEmpty(e.ImageFile)) return;
        try { File.Delete(Path.Combine(Storage.ImagesDir, e.ImageFile)); } catch { /* best effort */ }
    }

    public void SaveNow() =>
        Storage.SaveEntries(new ClipboardEntryFile { Entries = Entries.ToList() });
}
