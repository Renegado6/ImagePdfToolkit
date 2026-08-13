using ImagePdfToolkit.Infrastructure;
using ImagePdfToolkit.Services;

namespace ImagePdfToolkit.Models;

public sealed class WatermarkAdjustmentModel : ObservableObject
{
    private readonly LocalizationService _localization = LocalizationService.Instance;
    private int _sizePercent = AppConstants.DefaultWatermarkSizePercent;
    private int _colorDepthPercent = AppConstants.DefaultWatermarkColorDepthPercent;
    private int _offsetXPercent;
    private int _offsetYPercent;

    public WatermarkAdjustmentModel(int index)
    {
        Index = index;
        MoveOffsetCommand = new RelayCommand(MoveOffset);
    }

    public event EventHandler? SettingsChanged;

    public int Index { get; }

    public string Title => _localization.Format("WatermarkSettingsTitleFormat", Index + 1);

    public RelayCommand MoveOffsetCommand { get; }

    public int SizePercent
    {
        get => _sizePercent;
        set
        {
            value = Math.Clamp(value, AppConstants.MinWatermarkSizePercent, AppConstants.MaxWatermarkSizePercent);
            if (SetProperty(ref _sizePercent, value))
            {
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public int ColorDepthPercent
    {
        get => _colorDepthPercent;
        set
        {
            value = Math.Clamp(value, AppConstants.MinWatermarkColorDepthPercent, AppConstants.MaxWatermarkColorDepthPercent);
            if (SetProperty(ref _colorDepthPercent, value))
            {
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public int OffsetXPercent => _offsetXPercent;

    public int OffsetYPercent => _offsetYPercent;

    public string OffsetXDisplay => _localization.Format("OffsetHorizontalFormat", FormatSignedPercent(_offsetXPercent));

    public string OffsetYDisplay => _localization.Format("OffsetVerticalFormat", FormatSignedPercent(_offsetYPercent));

    public void Restore(int sizePercent, int colorDepthPercent, int offsetXPercent, int offsetYPercent)
    {
        _sizePercent = Math.Clamp(sizePercent, AppConstants.MinWatermarkSizePercent, AppConstants.MaxWatermarkSizePercent);
        _colorDepthPercent = Math.Clamp(colorDepthPercent, AppConstants.MinWatermarkColorDepthPercent, AppConstants.MaxWatermarkColorDepthPercent);
        _offsetXPercent = Math.Clamp(offsetXPercent, AppConstants.MinWatermarkOffsetPercent, AppConstants.MaxWatermarkOffsetPercent);
        _offsetYPercent = Math.Clamp(offsetYPercent, AppConstants.MinWatermarkOffsetPercent, AppConstants.MaxWatermarkOffsetPercent);
        OnPropertyChanged(nameof(SizePercent));
        OnPropertyChanged(nameof(ColorDepthPercent));
        OnPropertyChanged(nameof(OffsetXPercent));
        OnPropertyChanged(nameof(OffsetYPercent));
        OnPropertyChanged(nameof(OffsetXDisplay));
        OnPropertyChanged(nameof(OffsetYDisplay));
    }

    public void ResetOffsets()
    {
        SetOffsets(0, 0);
    }

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(OffsetXDisplay));
        OnPropertyChanged(nameof(OffsetYDisplay));
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

        SetOffsets(x, y);
    }

    private void SetOffsets(int x, int y)
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
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string FormatSignedPercent(int value)
        => value > 0 ? $"+{value}%" : $"{value}%";
}
