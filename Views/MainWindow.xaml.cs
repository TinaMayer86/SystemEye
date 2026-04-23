using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using ScottPlot.WPF;
using System.ComponentModel;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SystemEye.Services;
using SystemEye.ViewModels;

namespace SystemEye
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel? _mainViewModel;

        private class SensorUI
        {
            public Card MainCard { get; set; } = null!;
            public TextBlock ValueText { get; set; } = null!;
            public ScottPlot.Plottables.DataLogger Logger { get; set; } = null!;
            public WpfPlot Plot { get; set; } = null!;
            public int TickCount { get; set; } = 0;
            public ScottPlot.Plottables.VerticalLine CurrentLine { get; set; } = null!;
        }

        private readonly Dictionary<string, SensorUI> _sensorUIs = new();
        public MainWindow()
        {
            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this)) return;

            if (Application.Current is App myApp)
            {
                _mainViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<MainViewModel>(myApp.Services);
                if (_mainViewModel != null)
                {
                    DataContext = _mainViewModel;
                    _mainViewModel.DataUpdated += OnViewModelDataUpdated;
                }
            }
        }


        private void OnViewModelDataUpdated()
        {
            Dispatcher.Invoke(() =>
            {
                if (_mainViewModel == null) return;

                foreach (var sensor in _mainViewModel.CurrentSensors)
                {
                    string key = $"{sensor.HardwareType}_{sensor.Name}";

                    if (!_sensorUIs.ContainsKey(key))
                    {
                        var sensorUI = CreateSensorCard(sensor);
                        _sensorUIs[key] = sensorUI;
                        ChartsWrapPanel.Children.Add(sensorUI.MainCard);
                    }

                    var ui = _sensorUIs[key];

                    ui.ValueText.Text = $"{sensor.Value:F1} {sensor.Format}";

                    ui.Logger.Add(sensor.Value);
                    ui.TickCount++;
                    //TODO: Linie nochmal anpassen da es noch nicht richtig funktioniert!!!
                    //Graph nochmal anpasssen der er sich falsch verhält
                    double currentTick = ui.TickCount;

                    ui.CurrentLine.X = currentTick;

                    if (sensor.Format == "%")
                    {
                        ui.Plot.Plot.Axes.SetLimitsY(0, 100);
                    }
                    else
                    {
                        ui.Plot.Plot.Axes.AutoScaleY();
                    }

                    double windowSize = 60;

                    if (currentTick > windowSize)
                    {
                        ui.Plot.Plot.Axes.SetLimitsX(currentTick - windowSize, currentTick);
                    }
                    else
                    {
                        ui.Plot.Plot.Axes.SetLimitsX(0, windowSize);
                    }

                    ui.Plot.Refresh();
                }
                var activeKeys = _mainViewModel.CurrentSensors.Select(s => $"{s.HardwareType}_{s.Name}").ToList();
                var keysToRemove = _sensorUIs.Keys.Except(activeKeys).ToList();

                foreach (var k in keysToRemove)
                {
                    ChartsWrapPanel.Children.Remove(_sensorUIs[k].MainCard);
                    _sensorUIs.Remove(k);
                }
            });
        }

        private SensorUI CreateSensorCard(Models.SensorDataModel sensor)
        {
            var ui = new SensorUI();

            ui.MainCard = new Card
            {
                Width = 320,
                Height = 140,
                Margin = new Thickness(8),
                UniformCornerRadius = 16    // Corner anpassung
            };

            ElevationAssist.SetElevation(ui.MainCard, Elevation.Dp1);   // schatten

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

            // Linke Seite(Text)
            var stackPanel = new StackPanel { Margin = new Thickness(16, 16, 8, 16) };

            var nameText = new TextBlock { Text = sensor.Name, FontWeight = FontWeights.SemiBold, FontSize = 14, TextWrapping = TextWrapping.Wrap };
            var hwText = new TextBlock { Text = sensor.HardwareType, FontSize = 11, Opacity = 0.6, Margin = new Thickness(0, 2, 0, 8), TextTrimming = TextTrimming.CharacterEllipsis };

            ui.ValueText = new TextBlock { FontSize = 28, FontWeight = FontWeights.Black };
            ui.ValueText.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryHueMidBrush");

            stackPanel.Children.Add(nameText);
            stackPanel.Children.Add(hwText);
            stackPanel.Children.Add(ui.ValueText);

            Grid.SetColumn(stackPanel, 0);
            grid.Children.Add(stackPanel);

            // Rechte Seite(graphen)
            var border = new Border { Background = new SolidColorBrush(Color.FromArgb(5, 0, 0, 0)), CornerRadius = new CornerRadius(0, 16, 16, 0) };

            ui.Plot = new WpfPlot { Margin = new Thickness(5) };
            ui.Plot.UserInputProcessor.IsEnabled = false;

            ui.Plot.Plot.Axes.Bottom.IsVisible = false;
            ui.Plot.Plot.Axes.Left.IsVisible = false;
            ui.Plot.Plot.Axes.Right.IsVisible = false;
            ui.Plot.Plot.Axes.Top.IsVisible = false;
            ui.Plot.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#00000000");
            ui.Plot.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#00000000");

            ui.Logger = ui.Plot.Plot.Add.DataLogger();
            ui.Logger.Color = ScottPlot.Color.FromHex("#673AB7");
            ui.Logger.LineWidth = 2.5f;

            ui.CurrentLine = ui.Plot.Plot.Add.VerticalLine(0);
            ui.CurrentLine.Color = ScottPlot.Color.FromHex("#FF5252");
            ui.CurrentLine.LineWidth = 2;
            ui.CurrentLine.LinePattern = ScottPlot.LinePattern.Solid;

            border.Child = ui.Plot;
            Grid.SetColumn(border, 1);
            grid.Children.Add(border);

            ui.MainCard.Content = grid;

            return ui;
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(ThemeToggleButton.IsChecked == true ? BaseTheme.Dark : BaseTheme.Light);
            paletteHelper.SetTheme(theme);
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                MaximizeIcon.Kind = PackIconKind.WindowMaximize;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                MaximizeIcon.Kind = PackIconKind.WindowRestore;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}