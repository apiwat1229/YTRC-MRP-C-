using System.Text.Json;
using YTRC.ERP.Application.Configuration;
using YTRC.ERP.Domain.Models;

namespace YTRC.ERP.Application.Contracts;

public sealed record ApiResponse<T>(bool Success, T? Data, int StatusCode, string Error);

public interface IApiClient
{
    Task<ApiHealth> TestConnectionAsync(string baseUrl, CancellationToken ct = default);
    Task<LoginResult> LoginAsync(string baseUrl, string username, string password, CancellationToken ct = default);
    Task<ApiResponse<JsonElement>> GetAsync(string endpoint, string token, CancellationToken ct = default);
    Task<ApiResponse<JsonElement>> PostJsonAsync(string endpoint, object payload, string token, CancellationToken ct = default);
    Task<ApiResponse<JsonElement>> PutJsonAsync(string endpoint, object payload, string token, CancellationToken ct = default);
    Task<ApiResponse<JsonElement>> PatchJsonAsync(string endpoint, object payload, string token, CancellationToken ct = default);
    Task<ApiResponse<JsonElement>> DeleteAsync(string endpoint, string token, CancellationToken ct = default);
}

public interface IAuthenticationUseCase
{
    Task<LoginResult> SignInAsync(string serverUrl, string username, string password, CancellationToken ct = default);
}

public interface IAppSettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public interface IHardwareScaleService : IDisposable
{
    event EventHandler<ScaleChangedEventArgs>? ReadingChanged;
    bool IsConnected(int index);
    ScaleReading? LatestReading(int index);
    Task ConnectAsync(int index, ScaleSettings config);
    Task DisconnectAsync(int index);
    IReadOnlyList<string> EnumeratePorts();
}

public sealed class ScaleChangedEventArgs(int index, ScaleReading? reading, bool connected) : EventArgs
{
    public int Index { get; } = index;
    public ScaleReading? Reading { get; } = reading;
    public bool Connected { get; } = connected;
}

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default);
    Task<string> DownloadAsync(UpdateManifest manifest, IProgress<double>? progress = null, CancellationToken ct = default);
    void LaunchInstaller(string installerPath);
}
