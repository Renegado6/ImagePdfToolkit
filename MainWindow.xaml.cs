using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ImagePdfToolkit.Models;
using ImagePdfToolkit.Services;
using ImagePdfToolkit.ViewModels;

namespace ImagePdfToolkit;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        RestoreWindowPlacement();
    }

    private void PreviewDropZone_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            ViewModel.PickSourceCommand.Execute(null);
        }
    }

    private void PreviewDropZone_OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedFile(e, out var path)
                    && (ImageProcessingService.IsSupportedSourceImage(path) || PdfImageExtractionService.IsPdfFile(path))
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void PreviewDropZone_OnDrop(object sender, DragEventArgs e)
    {
        if (TryGetDroppedFile(e, out var path)
            && (ImageProcessingService.IsSupportedSourceImage(path) || PdfImageExtractionService.IsPdfFile(path)))
        {
            await ViewModel.LoadDroppedFileAsync(path);
        }

        e.Handled = true;
    }

    private void WatermarkSlot_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left
            && sender is FrameworkElement { DataContext: WatermarkSlotModel slot })
        {
            ViewModel.PickWatermarkCommand.Execute(slot);
        }
    }

    private void WatermarkSlot_OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedFile(e, out var path) && ImageProcessingService.IsPngFile(path)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void WatermarkSlot_OnDrop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WatermarkSlotModel slot }
            && TryGetDroppedFile(e, out var path)
            && ImageProcessingService.IsPngFile(path))
        {
            ViewModel.LoadDroppedWatermark(slot, path);
        }

        e.Handled = true;
    }

    private static bool TryGetDroppedFile(DragEventArgs e, out string path)
    {
        path = string.Empty;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)
            || e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths)
        {
            return false;
        }

        path = paths[0];
        return File.Exists(path);
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        SaveWindowPlacement();
        ViewModel.Dispose();
    }

    private void RestoreWindowPlacement()
    {
        var settings = _settingsService.Load();
        if (settings?.WindowWidth is not { } width
            || settings.WindowHeight is not { } height
            || !double.IsFinite(width)
            || !double.IsFinite(height)
            || width < MinWidth
            || height < MinHeight)
        {
            return;
        }

        Width = width;
        Height = height;
        if (settings.WindowLeft is { } left
            && settings.WindowTop is { } top
            && IsPlacementVisible(left, top, width, height))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }

        if (settings.IsWindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SaveWindowPlacement()
    {
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;
        if (bounds.Width < MinWidth || bounds.Height < MinHeight)
        {
            return;
        }

        _settingsService.SaveWindowPlacement(
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            WindowState == WindowState.Maximized);
    }

    private static bool IsPlacementVisible(double left, double top, double width, double height)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top))
        {
            return false;
        }

        var placement = new Rect(left, top, width, height);
        var desktop = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        placement.Intersect(desktop);
        return placement.Width >= 120 && placement.Height >= 80;
    }
}
