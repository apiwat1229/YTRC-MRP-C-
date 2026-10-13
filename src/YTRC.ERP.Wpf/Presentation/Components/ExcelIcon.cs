using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace YTRC.ERP.Wpf;

/// <summary>
/// Vector Microsoft Excel application icon component.
/// Displays the iconic Office Excel green spreadsheet and white 'X' badge.
/// Scales smoothly to any size (16x16, 20x20, 24x24).
/// </summary>
public sealed class ExcelIcon : Viewbox
{
    public ExcelIcon()
    {
        Stretch = Stretch.Uniform;
        Width = 20;
        Height = 20;
        IsHitTestVisible = false;
        Child = CreateVisual();
    }

    private static UIElement CreateVisual()
    {
        var canvas = new Canvas
        {
            Width = 24,
            Height = 24,
            ClipToBounds = false
        };

        // 1. Spreadsheet base plate (right / back)
        var sheetBack = new Border
        {
            Width = 15,
            Height = 18,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#107C41")), // Office Excel Green
            CornerRadius = new CornerRadius(2.5)
        };
        Canvas.SetLeft(sheetBack, 7);
        Canvas.SetTop(sheetBack, 3);
        canvas.Children.Add(sheetBack);

        // 2. Spreadsheet grid cells
        var cellsGrid = new Grid
        {
            Width = 9.5,
            Height = 14
        };
        Canvas.SetLeft(cellsGrid, 11);
        Canvas.SetTop(cellsGrid, 5);

        cellsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cellsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cellsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.1, GridUnitType.Star) });
        cellsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        cellsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        cellsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var headerBrush = new SolidColorBrush(Color.FromArgb(175, 255, 255, 255));
        var cellBrush1 = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
        var cellBrush2 = new SolidColorBrush(Color.FromArgb(125, 255, 255, 255));

        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 2; c++)
            {
                var cell = new Border
                {
                    Background = r == 0 ? headerBrush : ((r + c) % 2 == 0 ? cellBrush1 : cellBrush2),
                    Margin = new Thickness(0.5),
                    CornerRadius = new CornerRadius(0.5)
                };
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                cellsGrid.Children.Add(cell);
            }
        }
        canvas.Children.Add(cellsGrid);

        // 3. Front Excel "X" Tile with shadow
        var xTile = new Border
        {
            Width = 13,
            Height = 14,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0C5A2F")), // Deep Excel forest green
            CornerRadius = new CornerRadius(2),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 3,
                ShadowDepth = 1,
                Direction = 315,
                Opacity = 0.35,
                Color = Colors.Black
            }
        };
        Canvas.SetLeft(xTile, 2);
        Canvas.SetTop(xTile, 5);

        // White 'X' path inside tile
        var xPath = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 4.2 2 L 7 5.6 L 9.8 2 L 11.2 3.4 L 8.4 7 L 11.2 10.6 L 9.8 12 L 7 8.4 L 4.2 12 L 2.8 10.6 L 5.6 7 L 2.8 3.4 Z"),
            Fill = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform,
            Width = 7.8,
            Height = 8.5
        };
        xTile.Child = xPath;
        canvas.Children.Add(xTile);

        return canvas;
    }
}
