using YTRC.ERP.Domain.Models;

namespace YTRC.ERP.Application.Contracts;

public sealed record UserSession(UserProfile User, string Token, bool IsPreview = false);
public sealed record ApiHealth(bool IsOnline, string Version, string Message);
public sealed record LoginResult(bool Success, UserProfile? User, string Token, string ErrorMessage);
public sealed record UpdateManifest(string LatestVersion, string MinimumVersion, string DownloadUrl,
    string ReleaseNotes, string Sha256, bool IsMandatory);
public sealed record UpdateCheckResult(bool Success, bool HasUpdate, string CurrentVersion,
    UpdateManifest? Manifest, string Message);
public sealed record ApiRow(string Title, string Subtitle, string Detail, string Status);
