using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using ScottPlot.WPF;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SystemEye.Messages;
using SystemEye.ViewModels;

namespace SystemEye
{
    /// <summary>
    /// Hauptfenster der Anwendung, das die Live‑Sensoransicht darstellt und dynamisch
    /// UI‑Elemente für alle aktiven Sensoren erzeugt. Reagiert auf eingehende
    /// Live‑Datenmeldungen des ViewModels, aktualisiert Werte und Diagramme in
    /// Echtzeit und verwaltet Layout, Theme‑Umschaltung sowie grundlegende
    /// Fensterinteraktionen.
    /// </summary>
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
                }
            }

            // Beim Messenger anmelden, um den Graphen zu zeichnen
            WeakReferenceMessenger.Default.Register<LiveDataUpdatedMessage>(this, (recipient, message) =>
            {
                OnViewModelDataUpdated();
            });
        }

        /// <summary>
        /// Aktualisiert die UI‑Darstellung aller aktiven Sensoren, sobald neue
        /// Live‑Sensordaten vorliegen. Erstellt fehlende Sensor‑Karten dynamisch,
        /// aktualisiert Werte und Diagramme und entfernt nicht mehr aktive Sensoren
        /// aus der Ansicht.
        /// </summary>
        private void OnViewModelDataUpdated()
        {
            Dispatcher.Invoke(() =>
            {
                if (_mainViewModel == null) return;

                var activeKeys = new HashSet<string>();

                // NEU: Greift nun auf LiveVM zu
                foreach (var sensor in _mainViewModel.LiveVM.CurrentSensors)
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
                    float finalValue = sensor.Value;
                    string unit = sensor.Format;

                    if (sensor.Name.Contains("Memory") && sensor.HardwareType.Contains("Gpu"))
                    {
                        if (finalValue > 1000)
                        {
                            finalValue /= 1024f;
                            unit = "GB";
                        }
                        else if (string.IsNullOrEmpty(unit))
                        {
                            unit = "%";
                        }
                    }

                    string lowerName = sensor.Name.ToLower();

                    if (lowerName.Contains("fan") || lowerName.Contains("rpm"))
                    {
                        unit = finalValue > 100 ? "RPM" : "%";
                    }

                    if (string.IsNullOrEmpty(unit))
                    {
                        // Formatkennzeichnungen
                        if (lowerName.Contains("temp")) unit = "°C";
                        else if (lowerName.Contains("load") || lowerName.Contains("utilization") || lowerName.Contains("controller")) unit = "%";
                        else if (lowerName.Contains("clock") || lowerName.Contains("freq")) unit = "MHz";
                        else if (lowerName.Contains("power") || lowerName.Contains("watt")) unit = "W";
                        else if (lowerName.Contains("voltage") || lowerName.Contains("volt")) unit = "V";
                        else if (lowerName.Contains("data") || lowerName.Contains("used")) unit = "GB";
                    }

                    ui.ValueText.Text = $"{finalValue:F1} {unit}".Trim();
                    ui.DataBuffer[ui.NextIndex] = finalValue;
                    ui.CurrentLine.X = ui.NextIndex;

                    ui.NextIndex++;
                    if (ui.NextIndex >= ui.DataBuffer.Length)
                    {
                        ui.NextIndex = 0;
                    }

                    if (unit == "%")
                    {
                        ui.Plot.Plot.Axes.SetLimitsY(0, 100);
                    }
                    else
                    {
                        ui.Plot.Plot.Axes.AutoScaleY();
                    }
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

        /// <summary>
        /// Erstellt eine neue UI‑Karte für einen Sensor, einschließlich Textinformationen
        /// und eines Live‑Diagramms. Initialisiert alle grafischen Elemente und legt den
        /// Datenpuffer für die Verlaufskurve an.
        /// </summary>
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
            ui.Signal.Color = ScottPlot.Color.FromHex("#009688");
            ui.Signal.LineWidth = 2.5f;

            ui.CurrentLine = ui.Plot.Plot.Add.VerticalLine(0);
            ui.CurrentLine.Color = ScottPlot.Color.FromHex("#FFB300");
            ui.CurrentLine.LineWidth = 3;

            ui.Plot.Plot.Axes.SetLimitsX(0, bufferSize - 1);

            border.Child = ui.Plot;
            Grid.SetColumn(border, 1);
            grid.Children.Add(border);

            ui.MainCard.Content = grid;
            return ui;
        }

        /// <summary>
        /// Schaltet das Anwendungsdesign zwischen hellem und dunklem Modus um.
        /// Liest den aktuellen Zustand des Theme‑Toggles aus, setzt das entsprechende
        /// Basis‑Theme und aktualisiert anschließend das globale MaterialDesign‑Theme.
        /// </summary>
        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(ThemeToggleButton.IsChecked == true ? BaseTheme.Dark : BaseTheme.Light);
            paletteHelper.SetTheme(theme);
        }

        /// <summary>
        /// Leitet das Mausrad‑Scrollen einer verschachtelten ListBox an das übergeordnete
        /// UI‑Element weiter, um ein natürliches Scrollverhalten zu ermöglichen.
        /// Unterdrückt das Standardereignis und erzeugt ein neues MouseWheel‑Event
        /// für das Parent‑Element.
        /// </summary>
        private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                var parent = ((Control)sender).Parent as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }

        /// <summary>
        /// Ermöglicht das Verschieben des Fensters, indem ein Linksklick auf die
        /// benutzerdefinierte Titelleiste als Drag‑Bewegung interpretiert wird.
        /// </summary>
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        /// <summary>
        /// Wechselt den Fensterzustand zwischen normaler und maximierter Ansicht.
        /// Aktualisiert zusätzlich das Symbol des Maximize‑Buttons, um den aktuellen
        /// Zustand visuell widerzuspiegeln.
        /// </summary>
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

        /// <summary>
        /// Minimiert das Hauptfenster, wenn der entsprechende Button ausgelöst wird.
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        /// <summary>
        /// Schließt das Hauptfenster und beendet damit die aktuelle Anwendungssitzung.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}