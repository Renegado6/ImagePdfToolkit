using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using ImagePdfToolkit.Infrastructure;
using ImagePdfToolkit.Models;
using ImagePdfToolkit.Services;
using ImageSource = System.Windows.Media.ImageSource;

namespace ImagePdfToolkit.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly ImageProcessingService _imageService;
    private readonly PdfExportService _pdfService;
    private readonly PdfImageExtractionService _pdfImageExtractionService;
    private readonly SettingsService _settingsService;
    private readonly UserDialogService _dialogs;
    private readonly LocalizationService _localization;
    private readonly Random _random = new();
    private Bitmap? _sourceImage;
    private Bitmap? _previewBitmap;
    private ImageSource? _previewImage;
    private string? _sourceImagePath;
    private string? _outputDirectory;
    private WatermarkRenderState? _lastRenderState;
    private bool _hasWatermarkedResult;
    private bool _isRestoringSettings;
    private bool _isExtractingPdf;
    private int _watermarkSizePercent = AppConstants.DefaultWatermarkSizePercent;
    private int _watermarkColorDepthPercent = AppConstants.DefaultWatermarkColorDepthPercent;
    private int _offsetXPercent;
    private int _offsetYPercent;
    private string _statusText = string.Empty;
    private string _statusResourceKey = "StatusReady";
    private object?[] _statusArguments = [];
    private LanguageOption? _selectedLanguage;

    public MainViewModel()
    {
        _imageService = new ImageProcessingService();
        _pdfService = new PdfExportService(_imageService);
        _pdfImageExtractionService = new PdfImageExtractionService();
        _settingsService = new SettingsService();
        _localization = LocalizationService.Instance;
        _dialogs = new UserDialogService(_localization);

        Languages =
        [
            new LanguageOption(LocalizationService.SystemLanguageCode, "System default / 跟随系统"),
            new LanguageOption(LocalizationService.EnglishLanguageCode, "English"),
            new LanguageOption(LocalizationService.SimplifiedChineseLanguageCode, "简体中文")
        ];
        _selectedLanguage = Languages.First(option => option.Code == _localization.SelectedLanguageCode);
        _statusText = _localization.Get(_statusResourceKey);
        _localization.LanguageChanged += OnLanguageChanged;

        WatermarkSlots = new ObservableCollection<WatermarkSlotModel>(
            Enumerable.Range(0, AppConstants.SlotCount).Select(index => new WatermarkSlotModel(index)));

        PickSourceCommand = new RelayCommand(PickSourceFile);
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

    public IReadOnlyList<LanguageOption> Languages { get; }

    public RelayCommand PickSourceCommand { get; }

    public RelayCommand PickWatermarkCommand { get; }

    public RelayCommand ApplyWatermarkCommand { get; }

    public RelayCommand SaveResultCommand { get; }

    public RelayCommand ChooseOutputDirectoryCommand { get; }

    public RelayCommand CreatePdfCommand { get; }

    public RelayCommand ResetPreviewCommand { get; }

    public RelayCommand ClearWatermarksCommand { get; }

    public RelayCommand MoveOffsetCommand { get; }

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null || !SetProperty(ref _selectedLanguage, value))
            {
                return;
            }

            _localization.SetLanguage(value.Code);
            SaveSettings();
            SetStatus("StatusLanguageChangedFormat", value.DisplayName);
        }
    }

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

    public string OffsetXDisplay => _localization.Format("OffsetHorizontalFormat", FormatSignedPercent(_offsetXPercent));

    public string OffsetYDisplay => _localization.Format("OffsetVerticalFormat", FormatSignedPercent(_offsetYPercent));

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
                return _localization.Get("OutputDirectoryNotSelected");
            }

            if (!Directory.Exists(_outputDirectory))
            {
                return _localization.Format("OutputDirectoryMissingFormat", _outputDirectory);
            }

            if (IsSourceImageDirectory(_outputDirectory))
            {
                return _localization.Get("OutputDirectoryUnsafe");
            }

            return _localization.Format("OutputDirectoryFormat", _outputDirectory);
        }
    }

    public bool IsOutputDirectoryError => !string.IsNullOrWhiteSpace(_outputDirectory) && !IsOutputDirectoryValid();

    public Task LoadDroppedFileAsync(string path)
    {
        return HandleInputFileAsync(path);
    }

    public void LoadDroppedWatermark(WatermarkSlotModel slot, string path)
    {
        LoadWatermark(slot, path, remember: true, showError: true);
    }

    private async void PickSourceFile()
    {
        var path = _dialogs.PickSourceImage(_sourceImagePath);
        if (!string.IsNullOrWhiteSpace(path))
        {
            await HandleInputFileAsync(path);
        }
    }

    private async Task HandleInputFileAsync(string path)
    {
        if (PdfImageExtractionService.IsPdfFile(path))
        {
            await ExtractPdfPagesAsync(path);
            return;
        }

        LoadSourceImage(path, remember: true, showError: true);
    }

    private async Task ExtractPdfPagesAsync(string pdfPath)
    {
        if (_isExtractingPdf)
        {
            _dialogs.ShowInfo(
                _localization.Get("MessagePdfExtractionBusy"),
                _localization.Get("TitlePdfExtractionBusy"));
            return;
        }

        var selectedOutputDirectory = _dialogs.PickPdfImageOutputDirectory(pdfPath);
        if (string.IsNullOrWhiteSpace(selectedOutputDirectory))
        {
            return;
        }

        _isExtractingPdf = true;
        SetStatus("StatusPdfExtractionStartingFormat", Path.GetFileName(pdfPath));
        try
        {
            var progress = new Progress<PdfExtractionProgress>(value =>
            {
                if (value.CompletedPages == 0)
                {
                    SetStatus("StatusPdfExtractionPreparingFormat", value.TotalPages);
                    return;
                }

                SetStatus("StatusPdfExtractionProgressFormat", value.CompletedPages, value.TotalPages);
            });
            var result = await _pdfImageExtractionService.ExtractAllPagesAsync(
                pdfPath,
                selectedOutputDirectory,
                progress);
            SetStatus("StatusPdfExtractionCompletedFormat", result.PageCount, result.OutputDirectory);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(
                _localization.Format("MessagePdfExtractionFailedFormat", Environment.NewLine, ex.Message),
                _localization.Get("TitlePdfExtractionFailed"));
            SetStatus("StatusPdfExtractionFailed");
        }
        finally
        {
            _isExtractingPdf = false;
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
                _dialogs.ShowWarning(
                    _localization.Get("MessageUnsupportedSource"),
                    _localization.Get("TitleUnsupportedFormat"));
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
            SetStatus("StatusSourceLoadedFormat", Path.GetFileName(path));
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
                _dialogs.ShowError(
                    _localization.Format("MessageLoadSourceFailedFormat", Environment.NewLine, ex.Message),
                    _localization.Get("TitleLoadFailed"));
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
                _dialogs.ShowWarning(
                    _localization.Get("MessageWatermarkPngOnly"),
                    _localization.Get("TitleUnsupportedFormat"));
            }

            return false;
        }

        try
        {
            var loaded = _imageService.LoadBitmapCopy(path);
            slot.Replace(loaded, _imageService.CreatePreviewSource(loaded), path);
            SetStatus("StatusWatermarkLoadedFormat", slot.Index + 1, Path.GetFileName(path));
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
                _dialogs.ShowError(
                    _localization.Format("MessageLoadWatermarkFailedFormat", Environment.NewLine, ex.Message),
                    _localization.Get("TitleLoadFailed"));
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
            _dialogs.ShowInfo(
                _localization.Get("MessageMissingSource"),
                _localization.Get("TitleMissingSource"));
            return;
        }

        var availableSlots = WatermarkSlots.Where(slot => slot.Image is not null).ToArray();
        if (availableSlots.Length == 0)
        {
            _dialogs.ShowInfo(
                _localization.Get("MessageMissingWatermark"),
                _localization.Get("TitleMissingWatermark"));
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
                _dialogs.ShowInfo(
                    _localization.Get("MessageSelectedWatermarkMissing"),
                    _localization.Get("TitleMissingWatermark"));
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
        var direction = _localization.Get(state.Angle >= 0 ? "DirectionClockwise" : "DirectionCounterclockwise");
        SetStatus(
            "StatusWatermarkAppliedFormat",
            slot.Index + 1,
            WatermarkSizePercent,
            WatermarkColorDepthPercent,
            FormatSignedPercent(_offsetXPercent),
            FormatSignedPercent(_offsetYPercent),
            render.EffectiveOpacity,
            direction,
            Math.Abs(state.Angle));
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

        SetStatus("StatusSettingsRemembered");
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
            _dialogs.ShowInfo(
                _localization.Get("MessageNoResult"),
                _localization.Get("TitleNoResult"));
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
            SetStatus("StatusSavedFormat", fileName);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(
                _localization.Format("MessageSaveFailedFormat", Environment.NewLine, ex.Message),
                _localization.Get("TitleSaveFailed"));
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
                _localization.Get("MessageUnsafeDirectoryChooseAnother"),
                _localization.Get("TitleUnsafeDirectory"));
            return;
        }

        _outputDirectory = selected;
        SaveSettings();
        SetStatus("StatusOutputSelectedFormat", selected);
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
            _dialogs.ShowInfo(
                _localization.Get("MessageNoImages"),
                _localization.Get("TitleNoImages"));
            return;
        }

        var pdfPath = Path.Combine(_outputDirectory!, _localization.Get("MergedPdfFileName"));
        try
        {
            _pdfService.WriteImagesToPdf(imagePaths, pdfPath);
            SetStatus("StatusPdfCreatedFormat", Path.GetFileName(pdfPath), imagePaths.Count);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(
                _localization.Format("MessagePdfFailedFormat", Environment.NewLine, ex.Message),
                _localization.Get("TitleGenerationFailed"));
        }
    }

    private bool EnsureOutputDirectorySelected()
    {
        if (string.IsNullOrWhiteSpace(_outputDirectory))
        {
            _dialogs.ShowInfo(
                _localization.Get("MessageMissingOutputDirectory"),
                _localization.Get("TitleMissingOutputDirectory"));
            return false;
        }

        if (!Directory.Exists(_outputDirectory))
        {
            _dialogs.ShowWarning(
                _localization.Get("MessageOutputDirectoryMissing"),
                _localization.Get("TitleInvalidOutputDirectory"));
            return false;
        }

        if (IsSourceImageDirectory(_outputDirectory))
        {
            _dialogs.ShowWarning(
                _localization.Get("MessageUnsafeDirectoryReselect"),
                _localization.Get("TitleUnsafeDirectory"));
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
        SetStatus("StatusSourceRestored");
        RefreshDerivedState();
    }

    private void ClearWatermarks()
    {
        foreach (var slot in WatermarkSlots)
        {
            slot.Clear();
        }

        _lastRenderState = null;
        SetStatus("StatusWatermarksCleared");
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
                SetStatus(
                    "StatusAssetsRestoredFormat",
                    _localization.Get(restoredSource ? "RestoredSourceImage" : "RestoredNoSourceImage"),
                    restoredWatermarks);
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
            LanguageCode = _localization.SelectedLanguageCode,
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

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var slot in WatermarkSlots)
        {
            slot.RefreshLocalizedText();
        }

        OnPropertyChanged(nameof(OffsetXDisplay));
        OnPropertyChanged(nameof(OffsetYDisplay));
        OnPropertyChanged(nameof(OutputDirectoryDisplay));
        StatusText = _localization.Format(_statusResourceKey, _statusArguments);
    }

    private void SetStatus(string resourceKey, params object?[] arguments)
    {
        _statusResourceKey = resourceKey;
        _statusArguments = arguments;
        StatusText = _localization.Format(resourceKey, arguments);
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
        _localization.LanguageChanged -= OnLanguageChanged;
        _sourceImage?.Dispose();
        _previewBitmap?.Dispose();
        foreach (var slot in WatermarkSlots)
        {
            slot.Dispose();
        }
    }
}
