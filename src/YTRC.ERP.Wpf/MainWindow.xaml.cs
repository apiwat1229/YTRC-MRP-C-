using System.Windows;
using System.Windows.Controls;
using YTRC.ERP.Application.Contracts;
using YTRC.ERP.Domain.Models;

namespace YTRC.ERP.Wpf;

public partial class MainWindow : Window
{
    private UserSession _session;
    private readonly IApiClient _apiClient;
    private readonly IHardwareScaleService _scaleService;
    private readonly IUpdateService _updateService;
    private readonly List<ScaleEditor> _scaleEditors = [];
    private IReadOnlyList<ApiRow> _activeRows = [];
    private string _currentPage = "Home";
    private string _settingsSection = "System Config";
    private string _pageMessage = "";
    private bool _apiOnline;
    private bool _isUiInitialized;

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
        _isUiInitialized = true;
        _session = LoginWindow.Session ?? new UserSession(new UserProfile(), "", true);
        UserNameText.Text = _session.User.FullName;
        RoleText.Text = _session.IsPreview ? "Guest preview · read only" : _session.User.RoleName;
        FooterText.Text = $"YTRC-ERP  |  Client {ApplicationInfo.Version}  |  {AppSettings.Current.EnvironmentMode}";
        _scaleService.ReadingChanged += ScaleService_ReadingChanged;
        RenderCurrentPage();
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
