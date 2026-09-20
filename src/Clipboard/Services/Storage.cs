using System.Text.Json;
using System.Text.Json.Serialization;
using ClipboardApp.Models;

namespace ClipboardApp.Services;

public static class Storage
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    static readonly string RootDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipboard");

    static readonly string EntriesPath = Path.Combine(RootDir, "history.json");
    static readonly string SettingsPath = Path.Combine(RootDir, "settings.json");

    public static readonly string ImagesDir = Path.Combine(RootDir, "images");

    public static ClipboardEntryFile LoadEntries() =>
        LoadWithFallback(EntriesPath, () => new ClipboardEntryFile());

    public static void SaveEntries(ClipboardEntryFile file) => SaveAtomic(EntriesPath, file);

    public static AppSettings LoadSettings() =>
        LoadWithFallback(SettingsPath, () => new AppSettings());

    public static void SaveSettings(AppSettings settings) => SaveAtomic(SettingsPath, settings);

    static T LoadWithFallback<T>(string path, Func<T> makeDefault) where T : class
    {
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(ImagesDir);

        if (TryLoad<T>(path, out var value)) return value!;

        var backup = path + ".bak";
        if (TryLoad<T>(backup, out var fromBackup)) return fromBackup!;

        if (File.Exists(path))
        {
            var quarantine = path.Replace(".json", $".corrupt-{DateTime.Now:yyyyMMddHHmmss}.json");
            try { File.Move(path, quarantine, overwrite: true); } catch { /* best effort */ }
        }

        return makeDefault();
    }

    static bool TryLoad<T>(string path, out T? value) where T : class
    {
        value = null;
        if (!File.Exists(path)) return false;

        try
        {
            var text = File.ReadAllText(path);
            value = JsonSerializer.Deserialize<T>(text, Json);
            return value is not null;
        }
        catch
        {
            return false;
        }
    }

    static void SaveAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(RootDir);

        var tmp = path + ".tmp";
        var backup = path + ".bak";

        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));

        if (File.Exists(path))
            File.Replace(tmp, path, backup);
        else
            File.Move(tmp, path);
    }
}
