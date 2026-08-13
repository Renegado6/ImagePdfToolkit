using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace ImagePdfToolkit.Services;

public sealed class UserDialogService
{
    private readonly LocalizationService _localization;

    public UserDialogService(LocalizationService localization)
    {
        _localization = localization;
    }

    public string? PickSourceImage(string? currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = _localization.Get("DialogSelectSourceTitle"),
            Filter = $"{_localization.Get("DialogSupportedInputFiles")}|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.pdf|{_localization.Get("DialogImageFiles")}|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|{_localization.Get("DialogPdfFiles")}|*.pdf|{_localization.Get("DialogAllFiles")}|*.*",
            InitialDirectory = GetInitialDirectory(currentPath)
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickWatermark(int groupIndex, int optionIndex, string? currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = _localization.Format("DialogSelectWatermarkTitleFormat", groupIndex + 1, optionIndex + 1),
            Filter = $"{_localization.Get("DialogPngImages")}|*.png|{_localization.Get("DialogAllFiles")}|*.*",
            InitialDirectory = GetInitialDirectory(currentPath)
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickOutputDirectory(string? currentDirectory, string? sourcePath)
    {
        var initialDirectory = Directory.Exists(currentDirectory)
            ? currentDirectory
            : GetInitialDirectory(sourcePath);
        var dialog = new OpenFolderDialog
        {
            Title = _localization.Get("DialogSelectOutputTitle"),
            InitialDirectory = initialDirectory ?? string.Empty,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? PickPdfImageOutputDirectory(string pdfPath)
    {
        var dialog = new OpenFolderDialog
        {
            Title = _localization.Get("DialogSelectPdfOutputTitle"),
            InitialDirectory = GetInitialDirectory(pdfPath) ?? string.Empty,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public void ShowInfo(string message, string title)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string message, string title)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string message, string title)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    private static string? GetInitialDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Directory.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path);
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
            ? directory
            : null;
    }
}
