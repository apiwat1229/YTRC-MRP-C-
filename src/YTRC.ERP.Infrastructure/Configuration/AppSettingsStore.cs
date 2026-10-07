using System.IO;
using System.Text.Json;
using YTRC.ERP.Application.Configuration;
using YTRC.ERP.Application.Contracts;
using YTRC.ERP.Domain.Configuration;

namespace YTRC.ERP.Infrastructure.Configuration;

public sealed class AppSettingsStore : IAppSettingsStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YTRC-ERP-CSharp");
    private static readonly string FilePath = Path.Combine(Folder, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (loaded is not null)
                {
                    loaded.ApiBaseUrl = ServerUrl.NormalizeBaseUrl(loaded.ApiBaseUrl);
                    var scales = loaded.Scales?.Take(2).ToArray() ?? [];
                    loaded.Scales = scales.Length == 2 ? scales : new AppSettings().Scales;
                    return loaded;
                }
            }
        }
        catch { /* A malformed local preference file should not prevent startup. */ }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
