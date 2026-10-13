using System.Text.Json;
using YTRC.ERP.Application.Features.SIExport;

namespace YTRC.ERP.Wpf;

internal sealed class SIExportSourceArchiveWindow : Window
{
    private sealed record SourceRow(int Row, IReadOnlyList<string> Cells);

    public SIExportSourceArchiveWindow(ISIExportService service, string token, List<SIExportSourceFile> files)
    {
        Title = "คลังไฟล์ต้นทาง SI Export"; Width = 1050; Height = 730;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(18) };
        var tools = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(tools, Dock.Top); root.Children.Add(tools);
        tools.Children.Add(new TextBlock { Text = "ค้นหาชื่อไฟล์ / ปี / เดือน ", VerticalAlignment = VerticalAlignment.Center });
        var search = SIExportEditorWindow.Input(""); search.Width = 330; search.Margin = new Thickness(0, 0, 10, 0); tools.Children.Add(search);
        var grid = new DataGrid { ItemsSource = files, AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single };
        SIExportUi.Column(grid, "ไฟล์ต้นทาง", "Path", 750);
        SIExportUi.Column(grid, "การนำเข้า", "StatusLabel", 180);
        root.Children.Add(grid);
        search.TextChanged += (_, _) => grid.ItemsSource = files.Where(file => file.Path.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        var view = SIExportUi.Button("ดูทุกชีตและแถวต้นฉบับ", () => { }); tools.Children.Add(view);
        var download = SIExportUi.Button("บันทึก Excel ต้นฉบับ", () => { }); tools.Children.Add(download);
        download.Click += async (_, _) =>
        {
            if (grid.SelectedItem is not SIExportSourceFile source) return;
            var suffix = System.IO.Path.GetExtension(source.Path);
            var dialog = new SaveFileDialog { FileName = System.IO.Path.GetFileName(source.Path), Filter = $"Excel original (*{suffix})|*{suffix}" };
            if (dialog.ShowDialog(this) != true) return;
            try { download.IsEnabled = false; await service.SaveOriginalAsync(token, source, dialog.FileName); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "บันทึกต้นฉบับ", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { download.IsEnabled = true; }
        };
        async Task OpenSource()
        {
            if (grid.SelectedItem is not SIExportSourceFile selected) return;
            try
            {
                view.IsEnabled = false;
                var source = await service.SourceFileAsync(token, selected.Id);
                var panel = new DockPanel { Margin = new Thickness(18) };
                var label = new TextBlock { Text = selected.Path + "\nตารางนี้แสดงค่าต้นฉบับ รวมแถวที่ยังไม่เป็น shipment และแผนเดือนถัดไป", TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 12) };
                DockPanel.SetDock(label, Dock.Top); panel.Children.Add(label);
                var tabs = new TabControl(); panel.Children.Add(tabs);
                foreach (var sheet in source.GetProperty("sheets").EnumerateArray())
                {
                    var rows = new List<SourceRow>(); var columns = 0;
                    foreach (var row in sheet.GetProperty("rows").EnumerateArray())
                    {
                        var values = row.GetProperty("values").EnumerateArray().Select(value => value.ToString()).ToArray();
                        if (row.TryGetProperty("struckColumns", out var struck))
                            foreach (var column in struck.EnumerateArray())
                            {
                                var index = column.GetInt32() - 1;
                                if (index < values.Length) values[index] = "[ขีดฆ่า] " + values[index];
                            }
                        columns = Math.Max(columns, values.Length);
                        rows.Add(new SourceRow(row.GetProperty("row").GetInt32(), values));
                    }
                    rows = rows.Select(row => row with { Cells = row.Cells.Concat(Enumerable.Repeat("", columns - row.Cells.Count)).ToArray() }).ToList();
                    var table = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false };
                    SIExportUi.Column(table, "Row", "Row", 65);
                    for (var i = 0; i < columns; i++)
                    {
                        var name = ""; var number = i + 1;
                        while (number > 0) { number--; name = (char)('A' + number % 26) + name; number /= 26; }
                        SIExportUi.Column(table, name, $"Cells[{i}]", 170);
                    }
                    tabs.Items.Add(new TabItem { Header = sheet.GetProperty("name").GetString(), Content = table });
                }
                new Window { Owner = this, Title = selected.Path, Width = 1250, Height = 780,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel }.ShowDialog();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "ต้นฉบับ SI Export", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { view.IsEnabled = true; }
        }
        view.Click += async (_, _) => await OpenSource();
        grid.MouseDoubleClick += async (_, _) => await OpenSource();
        Content = root;
    }
}
