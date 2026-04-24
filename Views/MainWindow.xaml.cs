using MaterialDesignThemes.Wpf;
using ScottPlot.WPF;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SystemEye.ViewModels;
using ScottPlot;
using System.Collections.Generic;
using System.Linq;
using System;

namespace SystemEye
{
    public partial class MainWindow : Window
    {
        private MainViewModel? _mainViewModel;

        private class SensorUI
        {
            public Card MainCard { get; set; } = null!;
            public TextBlock ValueText { get; set; } = null!;
            public WpfPlot Plot { get; set; } = null!;
            public ScottPlot.Plottables.Signal Signal { get; set; } = null!;
            public double[] DataBuffer { get; set; } = null!;
            public int NextIndex { get; set; } = 0;
            public ScottPlot.Plottables.VerticalLine CurrentLine { get; set; } = null!;
        }

        private readonly Dictionary<string, SensorUI> _sensorUIs = new();

        public MainWindow()
        {
            InitializeComponent();

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

                var activeKeys = new HashSet<string>();

                foreach (var sensor in _mainViewModel.CurrentSensors)
                {
                    string key = $"{sensor.HardwareType}_{sensor.Name}";
                    activeKeys.Add(key);

                    if (!_sensorUIs.ContainsKey(key))
                    {
                        var sensorUI = CreateSensorCard(sensor);
                        _sensorUIs[key] = sensorUI;
                        ChartsWrapPanel.Children.Add(sensorUI.MainCard);
                    }

                    var ui = _sensorUIs[key];
                    ui.ValueText.Text = $"{sensor.Value:F1} {sensor.Format}";

                    ui.DataBuffer[ui.NextIndex] = sensor.Value;
                    ui.CurrentLine.X = ui.NextIndex;

                    ui.NextIndex++;
                    if (ui.NextIndex >= ui.DataBuffer.Length)
                    {
                        ui.NextIndex = 0;
                    }

                    if (sensor.Format == "%")
                        ui.Plot.Plot.Axes.SetLimitsY(0, 100);
                    else
                        ui.Plot.Plot.Axes.AutoScaleY();

                    ui.Plot.Refresh();
                }

                var keysToRemove = _sensorUIs.Keys.Where(k => !activeKeys.Contains(k)).ToList();
                foreach (var key in keysToRemove)
                {
                    ChartsWrapPanel.Children.Remove(_sensorUIs[key].MainCard);
                    _sensorUIs.Remove(key);
                }
            });
        }

        private SensorUI CreateSensorCard(Models.SensorDataModel sensor)
        {
            var ui = new SensorUI();
            int bufferSize = 60;
            ui.DataBuffer = new double[bufferSize];

            ui.MainCard = new Card
            {
                Width = 320,
                Height = 140,
                Margin = new Thickness(8),
                UniformCornerRadius = 16
            };

            ElevationAssist.SetElevation(ui.MainCard, Elevation.Dp1);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

            var infoStack = new StackPanel
            {
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(16, 16, 8, 16)
            };

            var nameText = new TextBlock
            {
                Text = sensor.Name,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };

            var hwText = new TextBlock
            {
                Text = sensor.HardwareType,
                FontSize = 11,
                Opacity = 0.6,
                Margin = new Thickness(0, 2, 0, 8),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            ui.ValueText = new TextBlock { FontSize = 28, FontWeight = FontWeights.Black };
            ui.ValueText.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryHueMidBrush");

            infoStack.Children.Add(nameText);
            infoStack.Children.Add(hwText);
            infoStack.Children.Add(ui.ValueText);

            Grid.SetColumn(infoStack, 0);
            grid.Children.Add(infoStack);

            var border = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(12, 0, 0, 0)),
                CornerRadius = new CornerRadius(0, 16, 16, 0)
            };

            ui.Plot = new WpfPlot { Margin = new Thickness(5) };
            ui.Plot.UserInputProcessor.IsEnabled = false;

            ui.Plot.Plot.Axes.Bottom.IsVisible = false;
            ui.Plot.Plot.Axes.Left.IsVisible = false;
            ui.Plot.Plot.Axes.Right.IsVisible = false;
            ui.Plot.Plot.Axes.Top.IsVisible = false;
            ui.Plot.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#00000000");
            ui.Plot.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#00000000");

            ui.Signal = ui.Plot.Plot.Add.Signal(ui.DataBuffer);
            ui.Signal.Color = ScottPlot.Color.FromHex("#673AB7");
            ui.Signal.LineWidth = 2.5f;

            ui.CurrentLine = ui.Plot.Plot.Add.VerticalLine(0);
            ui.CurrentLine.Color = ScottPlot.Color.FromHex("#FF5252");
            ui.CurrentLine.LineWidth = 2;

            ui.Plot.Plot.Axes.SetLimitsX(0, bufferSize - 1);

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
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

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

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}