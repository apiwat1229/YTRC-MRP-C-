using YTRC.ERP.Application.Features.SIExport;

namespace YTRC.ERP.Wpf;

internal sealed class SIExportHistoryWindow : Window
{
    public SIExportHistoryWindow(ISIExportService service, string token, SIExportSchedule schedule, List<SIExportRevision> revisions)
    {
        Title = $"Revision history · {schedule.ScheduleNo}";
        Width = 1100; Height = 760; MinWidth = 800; MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new Grid { Margin = new Thickness(18) };
        foreach (var height in new[] { new GridLength(1, GridUnitType.Star), GridLength.Auto,
            GridLength.Auto, new GridLength(2, GridUnitType.Star) }) root.RowDefinitions.Add(new RowDefinition { Height = height });
        var grid = new DataGrid { ItemsSource = revisions, AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single };
        foreach (var (label, key, width) in new[] { ("Revision", "Revision", 85d), ("Action", "Action", 100d),
            ("Status", "Status", 110d), ("Changed by", "CreatedBy", 150d), ("Date", "CreatedAt", 210d), ("Reason", "Reason", 380d) })
            SIExportUi.Column(grid, label, key, width);
        root.Children.Add(grid);
        var tools = new WrapPanel { Margin = new Thickness(0, 12, 0, 12) }; Grid.SetRow(tools, 1); root.Children.Add(tools);
        var numbers = revisions.Select(x => x.Revision.ToString()).ToList();
        var from = SIExportEditorWindow.Choice(numbers, revisions.Skip(1).FirstOrDefault()?.Revision.ToString() ?? numbers.FirstOrDefault() ?? "0");
        var to = SIExportEditorWindow.Choice(numbers, numbers.FirstOrDefault() ?? "0");
        from.Width = to.Width = 100; from.Margin = to.Margin = new Thickness(0, 0, 12, 0);
        tools.Children.Add(new TextBlock { Text = "From revision ", VerticalAlignment = VerticalAlignment.Center }); tools.Children.Add(from);
        tools.Children.Add(new TextBlock { Text = "To revision ", VerticalAlignment = VerticalAlignment.Center }); tools.Children.Add(to);
        var message = new TextBlock { Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
        var progress = new ProgressBar { Height = 4, Width = 160, IsIndeterminate = true,
            Visibility = Visibility.Collapsed, Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(message, 2); root.Children.Add(message);
        var host = new ContentControl(); Grid.SetRow(host, 3); root.Children.Add(host);
        var compare = SIExportUi.Button("Compare Revisions", () => { }); tools.Children.Add(compare);
        tools.Children.Add(progress);
        compare.Click += async (_, _) =>
        {
            try
            {
                compare.IsEnabled = false;
                progress.Visibility = Visibility.Visible;
                message.Text = "Comparing revisions…";
                var result = await service.CompareAsync(token, schedule.Id,
                    int.Parse(from.SelectedItem!.ToString()!), int.Parse(to.SelectedItem!.ToString()!));
                var changes = new DataGrid { ItemsSource = result.Changes, AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false };
                SIExportUi.Column(changes, "Contract / Line", "Contract", 150);
                SIExportUi.Column(changes, "Field", "Field", 180);
                SIExportUi.Column(changes, "Before", "BeforeText", 330);
                SIExportUi.Column(changes, "After", "AfterText", 330);
                host.Content = changes;
                message.Text = $"R{result.FromRevision:00} ({result.FromStatus}) → R{result.ToRevision:00} ({result.ToStatus}) · {result.Changes.Count} field changes";
            }
            catch (Exception ex) { message.Text = "Unable to compare revisions: " + ex.Message; }
            finally { progress.Visibility = Visibility.Collapsed; compare.IsEnabled = true; }
        };
        tools.Children.Add(SIExportUi.Button("View Selected Revision", () =>
        {
            if (grid.SelectedItem is not SIExportRevision version) return;
            for (var i = 0; i < version.Document.Lines.Count; i++) version.Document.Lines[i].No = i + 1;
            host.Content = SIExportUi.LineGrid(version.Document.Lines);
            message.Text = $"R{version.Revision:00} · {version.Document.Title} · {version.Document.Recipient} · {version.Status}";
        }));
        tools.Children.Add(SIExportUi.Button("Export Selected Revision", () =>
        {
            if (grid.SelectedItem is not SIExportRevision version) return;
            var dialog = new SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", FileName = $"{schedule.ScheduleNo}_R{version.Revision:00}.xlsx" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                SIExportExcelWriter.Write(new SIExportSchedule { ScheduleNo = schedule.ScheduleNo, Revision = version.Revision,
                    Status = version.Status, Document = version.Document }, dialog.FileName, true);
                message.Text = "Revision exported successfully";
            }
            catch (Exception ex) { message.Text = ex.Message; }
        }));
        grid.SelectedItem = revisions.FirstOrDefault();
        Content = root;
    }
}
