namespace ClipboardApp.Models;

public enum ClipboardEntryKind
{
    Text,
    Rtf,
    Html,
    Image,
}

public sealed class ClipboardEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ClipboardEntryKind Kind { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsPinned { get; set; }

    // Plain text: the copied text itself.
    // Rtf/Html: a plain-text fallback used for the row preview and for search.
    // Image: empty.
    public string PlainText { get; set; } = "";

    public string? Html { get; set; }
    public string? Rtf { get; set; }

    // File name (not full path) of the cached PNG under the images folder. Images live on
    // disk rather than as base64 in the JSON so the history file stays small to read/write.
    public string? ImageFile { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
}
