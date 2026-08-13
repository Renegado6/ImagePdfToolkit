using System.Collections.ObjectModel;
using ImagePdfToolkit.Infrastructure;
using ImagePdfToolkit.Services;

namespace ImagePdfToolkit.Models;

public sealed class WatermarkGroupModel : ObservableObject
{
    private readonly LocalizationService _localization = LocalizationService.Instance;

    public WatermarkGroupModel(int index)
    {
        Index = index;
        Adjustment = new WatermarkAdjustmentModel(index);
        Slots = new ObservableCollection<WatermarkSlotModel>(
            Enumerable.Range(0, AppConstants.SlotsPerWatermark)
                .Select(slotIndex => new WatermarkSlotModel(index, slotIndex)));
    }

    public int Index { get; }

    public string Title => _localization.Format("WatermarkLayerTitleFormat", Index + 1);

    public string ImagesTitle => _localization.Get("WatermarkImagesTitle");

    public ObservableCollection<WatermarkSlotModel> Slots { get; }

    public WatermarkAdjustmentModel Adjustment { get; }

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ImagesTitle));
        Adjustment.RefreshLocalizedText();
        foreach (var slot in Slots)
        {
            slot.RefreshLocalizedText();
        }
    }
}
