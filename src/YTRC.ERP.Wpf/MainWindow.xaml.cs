using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using FluentIcons.Common;
using FluentIcons.Wpf;
using YTRC.ERP.Application.Contracts;
using YTRC.ERP.Domain.Models;

namespace YTRC.ERP.Wpf;

public partial class MainWindow : Fluent.RibbonWindow
{
    private UserSession _session;
    private readonly IApiClient _apiClient;
    private readonly IHardwareScaleService _scaleService;
    private readonly IUpdateService _updateService;
    private readonly List<ScaleEditor> _scaleEditors = [];
    private IReadOnlyList<ApiRow> _activeRows = [];
    private string _currentPage = "Home";
    private string _purchasingSection = "Cuplump";
    private string? _purchasingAction;
    private List<PurchasingRecord> _purchasingAllRecords = [];
    private List<PurchasingRecord> _purchasingFilteredRecords = [];
    private List<JsonElement> _purchasingSuppliers = [];
    private List<JsonElement> _purchasingRubberTypes = [];
    private DataGrid? _purchasingGrid;
    private TextBox? _purchasingSearchBox;
    private CheckBox? _purchasingShowDeletedCheckBox;
    private PurchasingRecord? _purchasingSelectedRecord;
    private string _purchasingMessage = "";
    private bool _purchasingLoading;
    private int _purchasingLoadVersion;
    private string _settingsSection = "System Config";
    private string _pageMessage = "";
    private bool _apiOnline;
    private bool _isUiInitialized;
    private DataGrid? _remoteWorkspaceGrid;

    private TextBox? _apiBaseInput;
    private TextBox? _updateUrlInput;
    private ComboBox? _environmentInput;
    private CheckBox? _rememberUsernameInput;
    private TextBox? _printerNameInput;
    private TextBox? _printerCopiesInput;
    private CheckBox? _printerSilentInput;

    public MainWindow(IApiClient apiClient, IHardwareScaleService scaleService, IUpdateService updateService)
    {
        _apiClient = apiClient;
        _scaleService = scaleService;
        _updateService = updateService;
        InitializeComponent();
        MainRibbon.Loaded += MainRibbon_Loaded;
        _isUiInitialized = true;
        _session = LoginWindow.Session ?? new UserSession(new UserProfile(), "", true);
        UserNameText.Text = _session.User.FullName;
        RoleText.Text = _session.IsPreview ? "Guest preview · read only" : _session.User.RoleName;
        ConfigureBackstageProfile();
        FooterText.Text = $"YTRC-MRP  |  Client {ApplicationInfo.Version}";
        _scaleService.ReadingChanged += ScaleService_ReadingChanged;
        RenderCurrentPage();
    }

    private void ConfigureBackstageProfile()
    {
        var user = _session.User;
        var displayName = string.IsNullOrWhiteSpace(user.FullName) ? "Guest User" : user.FullName.Trim();
        var username = string.IsNullOrWhiteSpace(user.Username) ? "Not provided" : user.Username.Trim();
        var department = !string.IsNullOrWhiteSpace(user.DepartmentName)
            ? user.DepartmentName.Trim()
            : user.PrimaryDepartment == DepartmentId.Home
                ? "Not assigned"
                : DepartmentCatalog.Get(user.PrimaryDepartment).Name;

        BackstageUserNameText.Text = displayName;
        BackstageAccountIdentifierText.Text = string.IsNullOrWhiteSpace(user.Email) ? username : user.Email.Trim();
        BackstageRoleText.Text = _session.IsPreview ? "Guest preview · read only" : user.RoleName;
        ProfileInitialsText.Text = string.Join("", displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2).Select(part => char.ToUpperInvariant(part[0])));
        if (string.IsNullOrWhiteSpace(ProfileInitialsText.Text)) ProfileInitialsText.Text = "Y";

        ProfileUsernameText.Text = username;
        ProfileEmailText.Text = string.IsNullOrWhiteSpace(user.Email) ? "Not provided by ERP service" : user.Email.Trim();
        ProfileUserIdText.Text = string.IsNullOrWhiteSpace(user.UserId) ? "Not provided by ERP service" : user.UserId.Trim();
        ProfileRoleDetailText.Text = _session.IsPreview ? "Guest preview · read only" : user.RoleName;
        ProfileDepartmentText.Text = department;
        ProfileWorkspaceText.Text = user.PrimaryDepartment == DepartmentId.Home
            ? "Home workspace"
            : DepartmentCatalog.Get(user.PrimaryDepartment).Name;

        var availableWorkspaces = DepartmentCatalog.All.Count(item => user.HasDepartmentAccess(item.Id));
        ProfilePermissionSummaryText.Text = _session.IsPreview
            ? "Preview access · read only"
            : $"{availableWorkspaces} of {DepartmentCatalog.All.Count} department workspaces available";

        var departmentGrants = user.DepartmentPermissions
            .Select(grant => $"{DepartmentCatalog.Get(grant.Department).Name} · {grant.Level}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (_session.IsPreview)
            ProfilePermissionDetailsText.Text = "Sign in to load your assigned ERP access.";
        else if (user.Role == UserRole.SystemAdmin || user.RawPermissions.Contains("*", StringComparer.Ordinal))
            ProfilePermissionDetailsText.Text = "All workspaces · system-wide access";
        else if (departmentGrants.Length > 0)
            ProfilePermissionDetailsText.Text = string.Join("  •  ", departmentGrants);
        else if (user.RawPermissions.Count > 0)
            ProfilePermissionDetailsText.Text = $"{user.RawPermissions.Count} service permissions are assigned to this account.";
        else
            ProfilePermissionDetailsText.Text = "Detailed grants are managed by the ERP service.";

        ProfileSessionModeText.Text = _session.IsPreview ? "Guest preview · read only" : "Signed in · authenticated session";
        ProfileEnvironmentText.Text = string.IsNullOrWhiteSpace(YTRC.ERP.Application.Configuration.AppSettings.Current.EnvironmentMode)
            ? "Not configured"
            : YTRC.ERP.Application.Configuration.AppSettings.Current.EnvironmentMode;
        var appSettings = YTRC.ERP.Application.Configuration.AppSettings.Current;
        ProfileRememberUsernameText.Text = appSettings.RememberUsername
            ? $"Enabled · {appSettings.SavedUsername}"
            : "Disabled · username is not saved";
    }

    public async Task InitializeAsync()
    {
        if (_session.IsPreview)
        {
            SetConnection(false, "Guest preview");
            return;
        }
        await RefreshConnectionAsync();
    }

    private sealed class ScaleEditor(int index)
    {
        public int Index { get; } = index;
        public ComboBox Port { get; set; } = new();
        public ComboBox Baud { get; set; } = new();
        public ComboBox Model { get; set; } = new();
        public ComboBox Decimals { get; set; } = new();
        public CheckBox Simulation { get; set; } = new();
        public TextBlock Status { get; set; } = new();
        public TextBlock Reading { get; set; } = new();
        public Button Toggle { get; set; } = new();
    }
}
