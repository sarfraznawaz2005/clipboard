using System.Windows.Media.Imaging;
using ClipboardApp.Models;
using ClipboardApp.Services;

namespace ClipboardApp.Views;

// Per-row view wrapper: exposes the bindable preview/label/time text a ListBox can bind to.
public sealed class ClipboardEntryRow : ObservableObject
{
    public ClipboardEntry Model { get; }

    public ClipboardEntryRow(ClipboardEntry model) => Model = model;

    public bool IsImage => Model.Kind == ClipboardEntryKind.Image;
    public bool IsText => !IsImage;

    public string KindLabel => Model.Kind switch
    {
        ClipboardEntryKind.Text => "Text",
        ClipboardEntryKind.Html => "HTML",
        ClipboardEntryKind.Rtf => "Rich text",
        ClipboardEntryKind.Image => "Image",
        _ => "",
    };

    public string Preview => Model.Kind == ClipboardEntryKind.Image
        ? $"{Model.ImageWidth} × {Model.ImageHeight}"
        : TextFormat.Truncate(TextFormat.CollapseWhitespace(Model.PlainText), 300);

    // Full, untruncated text for the hover tooltip - Preview is capped and single-line.
    public string FullText => Model.PlainText;

    BitmapImage? _imageSource;
    public BitmapImage? ImageSource => _imageSource ??= LoadImage();

    string _timeText = "";
    public string TimeText { get => _timeText; private set => Set(ref _timeText, value); }

    bool _isPinned;
    public bool IsPinned { get => _isPinned; private set => Set(ref _isPinned, value); }

    // Pure UI state for bulk actions - never persisted.
    bool _isSelected;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    public void Refresh()
    {
        TimeText = TextFormat.RelativeTime(Model.CreatedAt);
        IsPinned = Model.IsPinned;
    }

    public void RaiseAll()
    {
        Raise(nameof(Preview));
        Raise(nameof(KindLabel));
        Raise(nameof(IsImage));
        Refresh();
    }

    BitmapImage? LoadImage()
    {
        if (string.IsNullOrEmpty(Model.ImageFile)) return null;

        var path = Path.Combine(Storage.ImagesDir, Model.ImageFile);
        if (!File.Exists(path)) return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.DecodePixelWidth = 96;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
