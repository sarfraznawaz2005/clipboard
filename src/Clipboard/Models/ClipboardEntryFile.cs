namespace ClipboardApp.Models;

public sealed class ClipboardEntryFile
{
    public int Version { get; set; } = 1;
    public List<ClipboardEntry> Entries { get; set; } = new();
}
