using System.Configuration;
using System.Data;
using System.Windows;
using YTRC.ERP.Application.Contracts;
using YTRC.ERP.Application.Features.Authentication;
using YTRC.ERP.Domain.Models;
using YTRC.ERP.Infrastructure.Api;
using YTRC.ERP.Infrastructure.Configuration;
using YTRC.ERP.Infrastructure.Devices;
using YTRC.ERP.Infrastructure.Updates;

namespace YTRC.ERP.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static IAppSettingsStore SettingsStore { get; private set; } = null!;
    public static IApiClient ApiClient { get; private set; } = null!;
    public static IAuthenticationUseCase SignInUseCase { get; private set; } = null!;
    public static IHardwareScaleService ScaleService { get; private set; } = null!;
    public static IUpdateService UpdateService { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SettingsStore = new AppSettingsStore();
        ApiClient = new ApiClient();
        SignInUseCase = new SignInUseCase(ApiClient);
        ScaleService = new HardwareScaleService();
        UpdateService = new YTRC.ERP.Infrastructure.Updates.UpdateService();
        AppSettings.Current = SettingsStore.Load();

        var login = new LoginWindow(SignInUseCase);
        if (login.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        var main = new MainWindow(ApiClient, ScaleService, UpdateService);
        MainWindow = main;
        ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
        main.Show();
        await main.InitializeAsync();
    }
}
