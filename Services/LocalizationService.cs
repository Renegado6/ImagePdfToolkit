using System.Globalization;
using System.Windows;

namespace RandomWatermarkTool.Services;

public sealed class LocalizationService
{
    public const string SystemLanguageCode = "auto";
    public const string EnglishLanguageCode = "en-US";
    public const string SimplifiedChineseLanguageCode = "zh-CN";

    private const string ResourcePrefix = "Resources/Strings.";

    private LocalizationService()
    {
    }

    public static LocalizationService Instance { get; } = new();

    public event EventHandler? LanguageChanged;

    public string SelectedLanguageCode { get; private set; } = SystemLanguageCode;

    public string CurrentLanguageCode { get; private set; } = EnglishLanguageCode;

    public void SetLanguage(string? languageCode)
    {
        var selectedLanguageCode = NormalizeLanguagePreference(languageCode);
        var normalizedCode = ResolveLanguageCode(selectedLanguageCode);
        var culture = CultureInfo.GetCultureInfo(normalizedCode);
        var source = new Uri($"{ResourcePrefix}{normalizedCode}.xaml", UriKind.Relative);
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var replacement = new ResourceDictionary { Source = source };
        var currentIndex = dictionaries
            .Select((dictionary, index) => new { dictionary, index })
            .FirstOrDefault(item => item.dictionary.Source?.OriginalString.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase) == true)
            ?.index;

        if (currentIndex is int index)
        {
            dictionaries[index] = replacement;
        }
        else
        {
            dictionaries.Insert(0, replacement);
        }

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        var changed = !string.Equals(CurrentLanguageCode, normalizedCode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(SelectedLanguageCode, selectedLanguageCode, StringComparison.OrdinalIgnoreCase);
        SelectedLanguageCode = selectedLanguageCode;
        CurrentLanguageCode = normalizedCode;
        if (changed)
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string Get(string key)
        => Application.Current.TryFindResource(key) as string ?? key;

    public string Format(string key, params object?[] arguments)
        => string.Format(CultureInfo.CurrentCulture, Get(key), arguments);

    public static string NormalizeLanguagePreference(string? languageCode)
    {
        if (string.Equals(languageCode, EnglishLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return EnglishLanguageCode;
        }

        if (string.Equals(languageCode, SimplifiedChineseLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return SimplifiedChineseLanguageCode;
        }

        return SystemLanguageCode;
    }

    private static string ResolveLanguageCode(string selectedLanguageCode)
    {
        if (!string.Equals(selectedLanguageCode, SystemLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return selectedLanguageCode;
        }

        return string.Equals(CultureInfo.InstalledUICulture.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase)
            ? SimplifiedChineseLanguageCode
            : EnglishLanguageCode;
    }
}
