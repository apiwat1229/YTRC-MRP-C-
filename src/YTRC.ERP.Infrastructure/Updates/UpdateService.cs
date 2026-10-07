using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using YTRC.ERP.Application.Configuration;
using YTRC.ERP.Application.Contracts;
using YTRC.ERP.Domain.Configuration;
using YTRC.ERP.Domain.Models;

namespace YTRC.ERP.Infrastructure.Updates;

public sealed class UpdateService : IUpdateService
{
    public const string CurrentVersion = ApplicationInfo.Version;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await Http.GetAsync(AppSettings.Current.UpdateServerUrl, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new(true, false, CurrentVersion, null, "No newer update is available.");
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            var version = Value(root, "versionString") ?? Value(root, "version") ?? Value(root, "latestVersion") ?? "";
            var url = Value(root, "downloadUrl") ?? Value(root, "url") ?? "";
            if (url.StartsWith('/')) url = ServerUrl.NormalizeBaseUrl(AppSettings.Current.ApiBaseUrl) + url;
            var manifest = new UpdateManifest(version, Value(root, "min_version") ?? Value(root, "minVersion") ?? "0.0.0",
                url, Value(root, "changelog") ?? Value(root, "notes") ?? Value(root, "releaseNotes") ?? "",
                Value(root, "sha256") ?? "", Boolean(root, "mandatory") || Boolean(root, "isMandatory"));
            var newer = CompareVersions(CurrentVersion, version) > 0;
            return new(true, newer, CurrentVersion, manifest, newer ? $"Version {version} is available." : "You are using the latest version.");
        }
        catch (Exception ex) { return new(false, false, CurrentVersion, null, ex.Message); }
    }

    public async Task<string> DownloadAsync(UpdateManifest manifest, IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(manifest.DownloadUrl)) throw new InvalidOperationException("The update manifest has no download URL.");
        var folder = Path.Combine(Path.GetTempPath(), "YTRC-ERP-Updates");
        Directory.CreateDirectory(folder);
        var name = Path.GetFileName(new Uri(manifest.DownloadUrl).AbsolutePath);
        if (string.IsNullOrWhiteSpace(name)) name = $"YTRC-ERP-Setup-{manifest.LatestVersion}.exe";
        var target = Path.Combine(folder, name);
        using var response = await Http.GetAsync(manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? -1;
        await using (var input = await response.Content.ReadAsStreamAsync(ct))
        await using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920]; long read = 0; int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, count), ct);
                read += count;
                if (total > 0) progress?.Report((double)read / total);
            }
        }
        if (!string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            await using var file = File.OpenRead(target);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
            if (!actual.Equals(manifest.Sha256.Replace(" ", "").Trim(), StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(target);
                throw new InvalidDataException("The downloaded update did not match its SHA-256 checksum.");
            }
        }
        return target;
    }

    public void LaunchInstaller(string installerPath)
        => Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });

    private static string? Value(JsonElement root, string name)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String
            ? item.GetString() : null;
    private static bool Boolean(JsonElement root, string name)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.True;
    private static int CompareVersions(string a, string b)
    {
        static int[] Parts(string s) => s.TrimStart('v', 'V').Split('-', '+')[0].Split('.').Select(x => int.TryParse(x, out var n) ? n : 0).ToArray();
        var x = Parts(a); var y = Parts(b);
        for (var i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            var cmp = (i < y.Length ? y[i] : 0).CompareTo(i < x.Length ? x[i] : 0);
            if (cmp != 0) return cmp;
        }
        return 0;
    }
}
