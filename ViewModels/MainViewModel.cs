using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using RandomWatermarkTool.Infrastructure;
using RandomWatermarkTool.Models;
using RandomWatermarkTool.Services;
using ImageSource = System.Windows.Media.ImageSource;

namespace RandomWatermarkTool.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly ImageProcessingService _imageService;
    private readonly PdfExportService _pdfService;
    private readonly SettingsService _settingsService;
    private readonly UserDialogService _dialogs;
    private readonly Random _random = new();
    private Bitmap? _sourceImage;
    private Bitmap? _previewBitmap;
    private ImageSource? _previewImage;
    private string? _sourceImagePath;
    private string? _outputDirectory;
    private WatermarkRenderState? _lastRenderState;
    private bool _hasWatermarkedResult;
    private bool _isRestoringSettings;
    private int _watermarkSizePercent = AppConstants.DefaultWatermarkSizePercent;
    private int _watermarkColorDepthPercent = AppConstants.DefaultWatermarkColorDepthPercent;
    private int _offsetXPercent;
    private int _offsetYPercent;
    private string _statusText = "准备就绪";

    public MainViewModel()
    {
        _imageService = new ImageProcessingService();
        _pdfService = new PdfExportService(_imageService);
        _settingsService = new SettingsService();
        _dialogs = new UserDialogService();

        WatermarkSlots = new ObservableCollection<WatermarkSlotModel>(
            Enumerable.Range(0, AppConstants.SlotCount).Select(index => new WatermarkSlotModel(index)));

        PickSourceCommand = new RelayCommand(PickSourceImage);
        PickWatermarkCommand = new RelayCommand(PickWatermark);
        ApplyWatermarkCommand = new RelayCommand(ApplyRandomWatermark, CanApplyWatermark);
        SaveResultCommand = new RelayCommand(SaveResult, CanSaveResult);
        ChooseOutputDirectoryCommand = new RelayCommand(ChooseOutputDirectory);
        CreatePdfCommand = new RelayCommand(CreatePdf, IsOutputDirectoryValid);
        ResetPreviewCommand = new RelayCommand(ResetPreviewToSource, () => _sourceImage is not null);
        ClearWatermarksCommand = new RelayCommand(ClearWatermarks, () => WatermarkSlots.Any(slot => slot.HasImage));
        MoveOffsetCommand = new RelayCommand(MoveOffset);

        LoadRememberedAssets();
        RefreshDerivedState();
    }

    public ObservableCollection<WatermarkSlotModel> WatermarkSlots { get; }

    public RelayCommand PickSourceCommand { get; }

    public RelayCommand PickWatermarkCommand { get; }

    public RelayCommand ApplyWatermarkCommand { get; }

    public RelayCommand SaveResultCommand { get; }

    public RelayCommand ChooseOutputDirectoryCommand { get; }

    public RelayCommand CreatePdfCommand { get; }

    public RelayCommand ResetPreviewCommand { get; }

    public RelayCommand ClearWatermarksCommand { get; }

    public RelayCommand MoveOffsetCommand { get; }

    public ImageSource? PreviewImage
    {
        get => _previewImage;
        private set
        {
            if (SetProperty(ref _previewImage, value))
            {
                OnPropertyChanged(nameof(HasPreview));
            }
        }
    }

    public bool HasPreview => PreviewImage is not null;

    public int WatermarkSizePercent
    {
        get => _watermarkSizePercent;
        set
        {
            value = Math.Clamp(value, AppConstants.MinWatermarkSizePercent, AppConstants.MaxWatermarkSizePercent);
            if (SetProperty(ref _watermarkSizePercent, value))
            {
                HandleSettingsChanged();
            }
        }
    }

    public int WatermarkColorDepthPercent
    {
        get => _watermarkColorDepthPercent;
        set
        {
            value = Math.Clamp(value, AppConstants.MinWatermarkColorDepthPercent, AppConstants.MaxWatermarkColorDepthPercent);
            if (SetProperty(ref _watermarkColorDepthPercent, value))
            {
                HandleSettingsChanged();
            }
        }
    }

    public int OffsetXPercent => _offsetXPercent;

    public int OffsetYPercent => _offsetYPercent;

    public string OffsetXDisplay => $"左右 {FormatSignedPercent(_offsetXPercent)}";

    public string OffsetYDisplay => $"上下 {FormatSignedPercent(_offsetYPercent)}";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string OutputDirectoryDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_outputDirectory))
            {
                return "输出目录：未选择";
            }

            if (!Directory.Exists(_outputDirectory))
            {
                return $"输出目录不存在：{_outputDirectory}";
            }

            if (IsSourceImageDirectory(_outputDirectory))
            {
                return "输出目录不能是原图所在文件夹";
            }

            return $"输出目录：{_outputDirectory}";
        }
    }

    public bool IsOutputDirectoryError => !string.IsNullOrWhiteSpace(_outputDirectory) && !IsOutputDirectoryValid();

    public void LoadDroppedSource(string path)
    {
        LoadSourceImage(path, remember: true, showError: true);
    }

    public void LoadDroppedWatermark(WatermarkSlotModel slot, string path)
    {
        LoadWatermark(slot, path, remember: true, showError: true);
    }

    private void PickSourceImage()
    {
        var path = _dialogs.PickSourceImage(_sourceImagePath);
        if (!string.IsNullOrWhiteSpace(path))
        {
            LoadSourceImage(path, remember: true, showError: true);
        }
    }

    private void PickWatermark(object? parameter)
    {
        if (parameter is not WatermarkSlotModel slot)
        {
            return;
        }

        var path = _dialogs.PickWatermark(slot.Index, slot.FilePath);
        if (!string.IsNullOrWhiteSpace(path))
        {
            LoadWatermark(slot, path, remember: true, showError: true);
        }
    }

    private bool LoadSourceImage(string path, bool remember, bool showError)
    {
        if (!ImageProcessingService.IsSupportedSourceImage(path))
        {
            if (showError)
            {
                _dialogs.ShowWarning("请选择 PNG、JPG、BMP、GIF 或 TIFF 图片。", "格式不支持");
            }

            return false;
        }

        try
        {
            var loaded = _imageService.LoadBitmapCopy(path);
            _sourceImage?.Dispose();
            _sourceImage = loaded;
            _sourceImagePath = path;
            _lastRenderState = null;
            SetPreviewBitmap((Bitmap)loaded.Clone(), hasWatermark: false);
            StatusText = $"已载入底图：{Path.GetFileName(path)}";
            if (remember)
            {
                SaveSettings();
            }

            RefreshDerivedState();
            return true;
        }
        catch (Exception ex)
        {
            if (showError)
            {
                _dialogs.ShowError($"底图载入失败：\n{ex.Message}", "载入失败");
            }

            return false;
        }
    }

    private bool LoadWatermark(WatermarkSlotModel slot, string path, bool remember, bool showError)
    {
        if (!ImageProcessingService.IsPngFile(path))
        {
            if (showError)
            {
                _dialogs.ShowWarning("水印槽只接受 PNG 图片。", "格式不支持");
            }

            return false;
        }

        try
        {
            var loaded = _imageService.LoadBitmapCopy(path);
            slot.Replace(loaded, _imageService.CreatePreviewSource(loaded), path);
            StatusText = $"已载入水印 {slot.Index + 1}：{Path.GetFileName(path)}";
            if (remember)
            {
                SaveSettings();
            }

            RefreshDerivedState();
            return true;
        }
        catch (Exception ex)
        {
            if (showError)
            {
                _dialogs.ShowError($"水印载入失败：\n{ex.Message}", "载入失败");
            }

            return false;
        }
    }

    private bool CanApplyWatermark()
        => _sourceImage is not null && WatermarkSlots.Any(slot => slot.HasImage);

    private void ApplyRandomWatermark()
    {
        if (_sourceImage is null)
        {
            _dialogs.ShowInfo("请先拖入底图。", "缺少底图");
            return;
        }

        var availableSlots = WatermarkSlots.Where(slot => slot.Image is not null).ToArray();
        if (availableSlots.Length == 0)
        {
            _dialogs.ShowInfo("请至少放入 1 个 PNG 水印。", "缺少水印");
            return;
        }

        var selectedSlot = availableSlots[_random.Next(availableSlots.Length)];
        var opacity = _random.Next(88, 99) / 100F;
        var angle = (float)(_random.NextDouble() * 30D) * (_random.Next(2) == 0 ? -1F : 1F);
        var state = new WatermarkRenderState(
            selectedSlot.Index,
            opacity,
            angle,
            _random.Next(),
            _random.NextDouble(),
            _random.NextDouble());
        RenderWatermark(state, showMissingWatermarkError: true);
    }

    private bool RenderWatermark(WatermarkRenderState state, bool showMissingWatermarkError)
    {
        if (_sourceImage is null)
        {
            return false;
        }

        var slot = WatermarkSlots.FirstOrDefault(item => item.Index == state.SlotIndex);
        if (slot?.Image is null)
        {
            if (showMissingWatermarkError)
            {
                _dialogs.ShowInfo("这次生成使用的水印已经不存在，请重新生成。", "缺少水印");
            }

            return false;
        }

        var render = _imageService.Render(
            _sourceImage,
            slot.Image,
            state,
            WatermarkSizePercent,
            WatermarkColorDepthPercent,
            _offsetXPercent,
            _offsetYPercent);
        _lastRenderState = state;
        SetPreviewBitmap(render.Image, hasWatermark: true);
        var direction = state.Angle >= 0 ? "顺时针" : "逆时针";
        StatusText =
            $"已使用水印 {slot.Index + 1} · 大小 {WatermarkSizePercent}% · 深浅 {WatermarkColorDepthPercent}% · " +
            $"左右 {FormatSignedPercent(_offsetXPercent)} · 上下 {FormatSignedPercent(_offsetYPercent)} · " +
            $"实际强度 {render.EffectiveOpacity:P0} · {direction} {Math.Abs(state.Angle):0.#}°";
        RefreshDerivedState();
        return true;
    }

    private void HandleSettingsChanged()
    {
        if (_isRestoringSettings)
        {
            return;
        }

        SaveSettings();
        if (_lastRenderState is not null
            && _hasWatermarkedResult
            && RenderWatermark(_lastRenderState, showMissingWatermarkError: false))
        {
            return;
        }

        StatusText = "已记住水印大小、深浅和位置，下一次生成生效";
    }

    private void MoveOffset(object? parameter)
    {
        var x = _offsetXPercent;
        var y = _offsetYPercent;
        switch (parameter as string)
        {
            case "Left":
                x -= AppConstants.WatermarkOffsetStepPercent;
                break;
            case "Right":
                x += AppConstants.WatermarkOffsetStepPercent;
                break;
            case "Up":
                y -= AppConstants.WatermarkOffsetStepPercent;
                break;
            case "Down":
                y += AppConstants.WatermarkOffsetStepPercent;
                break;
            case "Reset":
                x = 0;
                y = 0;
                break;
            default:
                return;
        }

        SetOffsets(x, y, applyChange: true);
    }

    private void SetOffsets(int x, int y, bool applyChange)
    {
        x = Math.Clamp(x, AppConstants.MinWatermarkOffsetPercent, AppConstants.MaxWatermarkOffsetPercent);
        y = Math.Clamp(y, AppConstants.MinWatermarkOffsetPercent, AppConstants.MaxWatermarkOffsetPercent);
        var changed = SetProperty(ref _offsetXPercent, x, nameof(OffsetXPercent));
        changed |= SetProperty(ref _offsetYPercent, y, nameof(OffsetYPercent));
        if (!changed)
        {
            return;
        }

        OnPropertyChanged(nameof(OffsetXDisplay));
        OnPropertyChanged(nameof(OffsetYDisplay));
        if (applyChange)
        {
            HandleSettingsChanged();
        }
    }

    private bool CanSaveResult()
        => _previewBitmap is not null && _hasWatermarkedResult && IsOutputDirectoryValid();

    private void SaveResult()
    {
        if (_previewBitmap is null || !_hasWatermarkedResult)
        {
            _dialogs.ShowInfo("请先生成水印结果。", "没有结果");
            return;
        }

        if (!EnsureOutputDirectorySelected())
        {
            return;
        }

        try
        {
            var fileName = GetOutputFileName();
            _imageService.SaveByExtension(_previewBitmap, Path.Combine(_outputDirectory!, fileName));
            ResetOffsetsAfterSave();
            StatusText = $"已保存到输出目录：{fileName}，左右/上下已重置为 0";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"保存失败：\n{ex.Message}", "保存失败");
        }
    }

    private void ResetOffsetsAfterSave()
    {
        var hadOffset = _offsetXPercent != 0 || _offsetYPercent != 0;
        _isRestoringSettings = true;
        try
        {
            SetOffsets(0, 0, applyChange: false);
        }
        finally
        {
            _isRestoringSettings = false;
        }

        SaveSettings();
        if (hadOffset && _lastRenderState is not null && _hasWatermarkedResult)
        {
            RenderWatermark(_lastRenderState, showMissingWatermarkError: false);
        }
    }

    private void ChooseOutputDirectory()
    {
        var selected = _dialogs.PickOutputDirectory(_outputDirectory, _sourceImagePath);
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        if (IsSourceImageDirectory(selected))
        {
            _dialogs.ShowWarning(
                "输出目录不能选择原图所在文件夹，否则同名保存会覆盖原图。请新建或选择另一个目录。",
                "目录不安全");
            return;
        }

        _outputDirectory = selected;
        SaveSettings();
        StatusText = $"已选择输出目录：{selected}";
        RefreshDerivedState();
    }

    private void CreatePdf()
    {
        if (!EnsureOutputDirectorySelected())
        {
            return;
        }

        var imagePaths = _pdfService.GetOrderedSourceImages(_outputDirectory!);
        if (imagePaths.Count == 0)
        {
            _dialogs.ShowInfo("输出目录里没有可生成 PDF 的图片。", "没有图片");
            return;
        }

        var pdfPath = Path.Combine(_outputDirectory!, "合并结果.pdf");
        try
        {
            _pdfService.WriteImagesToPdf(imagePaths, pdfPath);
            StatusText = $"已生成 PDF：{Path.GetFileName(pdfPath)}，共 {imagePaths.Count} 张图片";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"生成 PDF 失败：\n{ex.Message}", "生成失败");
        }
    }

    private bool EnsureOutputDirectorySelected()
    {
        if (string.IsNullOrWhiteSpace(_outputDirectory))
        {
            _dialogs.ShowInfo("请先点击“选择输出目录”。", "缺少输出目录");
            return false;
        }

        if (!Directory.Exists(_outputDirectory))
        {
            _dialogs.ShowWarning("输出目录不存在，请重新选择。", "输出目录无效");
            return false;
        }

        if (IsSourceImageDirectory(_outputDirectory))
        {
            _dialogs.ShowWarning(
                "输出目录不能是原图所在文件夹，否则同名保存会覆盖原图。请重新选择输出目录。",
                "目录不安全");
            return false;
        }

        return true;
    }

    private bool IsOutputDirectoryValid()
    {
        return !string.IsNullOrWhiteSpace(_outputDirectory)
            && Directory.Exists(_outputDirectory)
            && !IsSourceImageDirectory(_outputDirectory);
    }

    private bool IsSourceImageDirectory(string directory)
    {
        var sourceDirectory = string.IsNullOrWhiteSpace(_sourceImagePath)
            ? null
            : Path.GetDirectoryName(_sourceImagePath);
        return !string.IsNullOrWhiteSpace(sourceDirectory) && IsSameDirectory(directory, sourceDirectory);
    }

    private static bool IsSameDirectory(string left, string right)
    {
        try
        {
            return string.Equals(
                NormalizeDirectory(left),
                NormalizeDirectory(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeDirectory(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private string GetOutputFileName()
        => string.IsNullOrWhiteSpace(_sourceImagePath)
            ? $"watermarked_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            : Path.GetFileName(_sourceImagePath);

    private void ResetPreviewToSource()
    {
        if (_sourceImage is null)
        {
            return;
        }

        SetPreviewBitmap((Bitmap)_sourceImage.Clone(), hasWatermark: false);
        _lastRenderState = null;
        StatusText = "已恢复原图";
        RefreshDerivedState();
    }

    private void ClearWatermarks()
    {
        foreach (var slot in WatermarkSlots)
        {
            slot.Clear();
        }

        _lastRenderState = null;
        StatusText = "已清空水印";
        SaveSettings();
        RefreshDerivedState();
    }

    private void SetPreviewBitmap(Bitmap bitmap, bool hasWatermark)
    {
        _previewBitmap?.Dispose();
        _previewBitmap = bitmap;
        PreviewImage = _imageService.CreatePreviewSource(bitmap);
        _hasWatermarkedResult = hasWatermark;
    }

    private void LoadRememberedAssets()
    {
        var settings = _settingsService.Load();
        if (settings is null)
        {
            return;
        }

        _isRestoringSettings = true;
        try
        {
            WatermarkSizePercent = settings.WatermarkSizePercent;
            WatermarkColorDepthPercent = settings.WatermarkColorDepthPercent;
            SetOffsets(settings.WatermarkOffsetXPercent, settings.WatermarkOffsetYPercent, applyChange: false);
            _outputDirectory = !string.IsNullOrWhiteSpace(settings.OutputDirectory)
                ? settings.OutputDirectory
                : settings.LastSaveDirectory;

            var restoredSource = !string.IsNullOrWhiteSpace(settings.SourceImagePath)
                && File.Exists(settings.SourceImagePath)
                && LoadSourceImage(settings.SourceImagePath, remember: false, showError: false);
            var restoredWatermarks = 0;
            var paths = settings.WatermarkPaths ?? [];
            for (var index = 0; index < Math.Min(WatermarkSlots.Count, paths.Length); index++)
            {
                var path = paths[index];
                if (!string.IsNullOrWhiteSpace(path)
                    && File.Exists(path)
                    && LoadWatermark(WatermarkSlots[index], path, remember: false, showError: false))
                {
                    restoredWatermarks++;
                }
            }

            if (restoredSource || restoredWatermarks > 0)
            {
                StatusText = $"已恢复上次素材：{(restoredSource ? "底图" : "无底图")}，{restoredWatermarks} 个水印";
            }
        }
        finally
        {
            _isRestoringSettings = false;
        }
    }

    private void SaveSettings()
    {
        if (_isRestoringSettings)
        {
            return;
        }

        _settingsService.Save(new AppSettings
        {
            SourceImagePath = _sourceImagePath,
            WatermarkPaths = WatermarkSlots.Select(slot => slot.FilePath).ToArray(),
            OutputDirectory = _outputDirectory,
            LastSaveDirectory = _outputDirectory,
            WatermarkSizePercent = WatermarkSizePercent,
            WatermarkColorDepthPercent = WatermarkColorDepthPercent,
            WatermarkOffsetXPercent = _offsetXPercent,
            WatermarkOffsetYPercent = _offsetYPercent
        });
    }

    private void RefreshDerivedState()
    {
        OnPropertyChanged(nameof(OutputDirectoryDisplay));
        OnPropertyChanged(nameof(IsOutputDirectoryError));
        ApplyWatermarkCommand.NotifyCanExecuteChanged();
        SaveResultCommand.NotifyCanExecuteChanged();
        CreatePdfCommand.NotifyCanExecuteChanged();
        ResetPreviewCommand.NotifyCanExecuteChanged();
        ClearWatermarksCommand.NotifyCanExecuteChanged();
    }

    private static string FormatSignedPercent(int value)
        => value > 0 ? $"+{value}%" : $"{value}%";

    public void Dispose()
    {
        _sourceImage?.Dispose();
        _previewBitmap?.Dispose();
        foreach (var slot in WatermarkSlots)
        {
            slot.Dispose();
        }
    }
}
