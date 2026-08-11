using System.Drawing;
using System.IO;
using RandomWatermarkTool.Infrastructure;
using ImageSource = System.Windows.Media.ImageSource;

namespace RandomWatermarkTool.Models;

public sealed class WatermarkSlotModel : ObservableObject, IDisposable
{
    private string? _filePath;
    private ImageSource? _previewImage;

    public WatermarkSlotModel(int index)
    {
        Index = index;
    }

    public int Index { get; }

    public string Title => $"水印 {Index + 1}";

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
        ? "拖入 PNG 或点击选择"
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

    public void Dispose() => Clear();
}
