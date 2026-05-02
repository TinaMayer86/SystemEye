using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Windows;
using SystemEye.Messages;
using SystemEye.Models;

namespace SystemEye.ViewModels
{
    /// <summary>
    /// ViewModel für den Live-Tab.
    /// Hält die aktuellen Sensordaten und benachrichtigt die UI (ScottPlot) über neue Werte.
    /// </summary>
    public partial class LiveViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<SensorDataModel> _currentSensors = new();

        /// <summary>
        /// Wird vom Background-Loop des MainViewModels aufgerufen, um neue Daten zu übergeben.
        /// </summary>
        public void UpdateSensorData(IEnumerable<SensorDataModel> newSensors)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentSensors.Clear();
                foreach (var s in newSensors)
                {
                    CurrentSensors.Add(s);
                }
            });
            WeakReferenceMessenger.Default.Send(new LiveDataUpdatedMessage());
        }
    }
}