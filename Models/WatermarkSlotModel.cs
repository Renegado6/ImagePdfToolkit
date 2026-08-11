using System.Drawing;
using System.IO;
using ImagePdfToolkit.Infrastructure;
using ImagePdfToolkit.Services;
using ImageSource = System.Windows.Media.ImageSource;

namespace ImagePdfToolkit.Models;

public sealed class WatermarkSlotModel : ObservableObject, IDisposable
{
    private readonly LocalizationService _localization = LocalizationService.Instance;
    private string? _filePath;
    private ImageSource? _previewImage;

    public WatermarkSlotModel(int index)
    {
        Index = index;
    }

    public int Index { get; }

    public string Title => _localization.Format("WatermarkSlotTitleFormat", Index + 1);

    public string? FilePath
    {
        get => _filePath;
        private set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(HasImage));
            }
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(FilePath)
        ? _localization.Get("WatermarkDropHint")
        : Path.GetFileName(FilePath);

    public ImageSource? PreviewImage
    {
        get => _previewImage;
        private set => SetProperty(ref _previewImage, value);
    }

    public bool HasImage => Image is not null;

    public Bitmap? Image { get; private set; }

    public void Replace(Bitmap image, ImageSource previewImage, string path)
    {
        Image?.Dispose();
        Image = image;
        PreviewImage = previewImage;
        FilePath = path;
        OnPropertyChanged(nameof(HasImage));
    }

    public void Clear()
    {
        Image?.Dispose();
        Image = null;
        PreviewImage = null;
        FilePath = null;
        OnPropertyChanged(nameof(HasImage));
    }

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(DisplayName));
    }

    public void Dispose() => Clear();
}
