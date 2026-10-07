namespace YTRC.ERP.Application.Configuration;

public static class ApplicationInfo
{
    public const string Version = "0.1.0";
}

public sealed class AppSettings
{
    public static AppSettings Current { get; set; } = new();
    public string ApiBaseUrl { get; set; } = "https://api.ytrc.co.th";
    public string EnvironmentMode { get; set; } = "production";
    public string UpdateServerUrl { get; set; } = "https://api.ytrc.co.th/api/app-updates/ytrc-erp/latest";
    public string SavedUsername { get; set; } = "admin";
    public bool RememberUsername { get; set; } = true;
    public ScaleSettings[] Scales { get; set; } = [
        new() { PortName = "COM1", DecimalPlaces = 2, Simulation = true, Model = "Tiger TI-01" },
        new() { PortName = "COM2", DecimalPlaces = 3, Model = "Generic" }
    ];
    public string PrinterName { get; set; } = "Default Windows Printer";
    public int PrintCopies { get; set; } = 1;
    public bool SilentPrint { get; set; }
}

public sealed class ScaleSettings
{
    public bool Enabled { get; set; }
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public int Parity { get; set; }
    public int StopBits { get; set; } = 1;
    public int DecimalPlaces { get; set; } = 2;
    public bool Simulation { get; set; }
    public string Model { get; set; } = "Generic";
}
