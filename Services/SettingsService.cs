using System.IO;
using System.Text.Json;
using ImagePdfToolkit.Models;

namespace ImagePdfToolkit.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings? Load()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return LoadLegacySettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // Settings are a convenience. Image processing must remain available if persistence fails.
        }
    }

    public void SaveWindowPlacement(
        double left,
        double top,
        double width,
        double height,
        bool isMaximized)
    {
        var settings = Load() ?? new AppSettings();
        settings.WindowLeft = left;
        settings.WindowTop = top;
        settings.WindowWidth = width;
        settings.WindowHeight = height;
        settings.IsWindowMaximized = isMaximized;
        Save(settings);
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ImagePdfToolkit",
            "settings.json");
    }

    private AppSettings? LoadLegacySettings()
    {
        var legacyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RandomWatermarkTool",
            "settings.json");

        if (!File.Exists(legacyPath))
        {
            return null;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(legacyPath), JsonOptions);
            if (settings is not null)
            {
                Save(settings);
            }

            return settings;
        }
        catch
        {
            return null;
        }
    }
}
