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
    private List<PurchasingChoice> _purchasingProvinces = [];
    private List<PurchasingChoice> _purchasingDistricts = [];
    private List<PurchasingChoice> _purchasingSubdistricts = [];
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
        MainRibbon.SizeChanged += MainWindow_PurchasingGridSizeChanged;
        WorkspaceScrollViewer.SizeChanged += MainWindow_PurchasingGridSizeChanged;
        SizeChanged += MainWindow_PurchasingGridSizeChanged;
        _isUiInitialized = true;
        _session = LoginWindow.Session ?? new UserSession(new UserProfile(), "", true);
        UserNameText.Text = _session.User.FullName;
        RoleText.Text = _session.IsPreview ? "Guest preview · read only" : _session.User.RoleName;
        FooterText.Text = $"YTRC-MRP  |  Client {ApplicationInfo.Version}";
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
