using System.Text.Json;
using YTRC.ERP.Application.Features.SIExport;

namespace YTRC.ERP.Wpf;

internal static class SIExportSourceWindow
{
    private sealed record SourceCell(string Column, string Header, string Value);

    internal static void Show(Window owner, SIExportLine line)
    {
        if (string.IsNullOrWhiteSpace(line.SourceData))
        {
            MessageBox.Show(owner, "This schedule was created in the app and has no source Excel row.", "Source Data");
            return;
        }
        try
        {
            using var json = JsonDocument.Parse(line.SourceData);
            var raw = json.RootElement;
            var headers = raw.GetProperty("headers").EnumerateArray().ToArray();
            var values = raw.GetProperty("values").EnumerateArray().ToArray();
            var cells = new List<SourceCell>();
            for (var i = 0; i < Math.Max(headers.Length, values.Length); i++)
            {
                var column = ""; var number = i + 1;
                while (number > 0) { number--; column = (char)('A' + number % 26) + column; number /= 26; }
                cells.Add(new SourceCell(column, i < headers.Length ? headers[i].ToString() : "",
                    i < values.Length ? values[i].ToString() : ""));
            }
            if (raw.TryGetProperty("inheritedFields", out var inherited))
                foreach (var item in inherited.EnumerateArray())
                    cells.Add(new SourceCell("Inherited field", item.GetProperty("field").GetString() ?? "",
                        item.GetProperty("value").ToString() + " | " + item.GetProperty("source").GetString()));
            var panel = new DockPanel { Margin = new Thickness(18) };
            var source = new TextBlock { Text = line.SourceReference + "\nThe values below are from the original source row before normalization.", TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12), FontSize = 14 };
            DockPanel.SetDock(source, Dock.Top); panel.Children.Add(source);
            var grid = new DataGrid { ItemsSource = cells, AutoGenerateColumns = false,
                IsReadOnly = true, CanUserAddRows = false };
            SIExportUi.Column(grid, "Column", "Column", 75);
            SIExportUi.Column(grid, "Source Column Header", "Header", 320);
            SIExportUi.Column(grid, "Source Value", "Value", 520);
            panel.Children.Add(grid);
            new Window { Owner = owner, Title = "Source Excel · " + (line.Contract.Length > 0 ? line.Contract : line.CustomerPo),
                Width = 980, Height = 700, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel }.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show(owner, ex.Message, "Source Data", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}
