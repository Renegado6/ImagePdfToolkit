using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace RandomWatermarkTool.Services;

public sealed class UserDialogService
{
    public string? PickSourceImage(string? currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择底图",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|所有文件|*.*",
            InitialDirectory = GetInitialDirectory(currentPath)
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickWatermark(int slotIndex, string? currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = $"选择水印 {slotIndex + 1}",
            Filter = "PNG 图片|*.png|所有文件|*.*",
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
            Title = "选择水印结果输出目录",
            InitialDirectory = initialDirectory ?? string.Empty,
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
