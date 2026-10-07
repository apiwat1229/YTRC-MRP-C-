using YTRC.ERP.Application.Contracts;

namespace YTRC.ERP.Application.Features.Authentication;

public sealed class SignInUseCase(IApiClient apiClient) : IAuthenticationUseCase
{
    public Task<LoginResult> SignInAsync(string serverUrl, string username, string password,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return Task.FromResult(new LoginResult(false, null, "", "Enter your username or email."));
        if (string.IsNullOrEmpty(password))
            return Task.FromResult(new LoginResult(false, null, "", "Enter your password."));
        return apiClient.LoginAsync(serverUrl, username.Trim(), password, ct);
    }
}
