using System.IO;
using System.Text.Json;
using RandomWatermarkTool.Models;

namespace RandomWatermarkTool.Services;

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
            return null;
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

    private static string GetSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RandomWatermarkTool",
            "settings.json");
    }
}
