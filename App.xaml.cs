using System.Windows;
using RandomWatermarkTool.Services;

namespace RandomWatermarkTool;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var languageCode = e.Args
            .FirstOrDefault(argument => argument.StartsWith("--language=", StringComparison.OrdinalIgnoreCase))
            ?.Split('=', 2)[1]
            ?? new SettingsService().Load()?.LanguageCode;
        LocalizationService.Instance.SetLanguage(languageCode);
        base.OnStartup(e);
    }
}
