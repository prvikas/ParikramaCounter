using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    // Fix #9: DiagnosticsViewModel subscribes/unsubscribes based on page
    // visibility (Activate/Deactivate called from code-behind OnAppearing/OnDisappearing).
    // Previously it processed 50 UI updates/second permanently regardless of tab visibility.
    public class DiagnosticsViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ISensorService     sensorService;
        private readonly TrackingViewModel  trackingViewModel;
        private bool active;
        private bool disposed;

        private string accelX = "0.000", accelY = "0.000", accelZ = "0.000";
        private string gyroX  = "0.000", gyroY  = "0.000", gyroZ  = "0.000";
        private string magX   = "0.000", magY   = "0.000", magZ   = "0.000";
        private string accelMagnitude   = "0.000";
        private string currentThreshold = "dynamic";
        private string timestamp        = DateTime.Now.ToString("HH:mm:ss.fff");

        public string AccelX            { get => accelX;            set { accelX = value;            OnPropertyChanged(); } }
        public string AccelY            { get => accelY;            set { accelY = value;            OnPropertyChanged(); } }
        public string AccelZ            { get => accelZ;            set { accelZ = value;            OnPropertyChanged(); } }
        public string GyroX             { get => gyroX;             set { gyroX = value;             OnPropertyChanged(); } }
        public string GyroY             { get => gyroY;             set { gyroY = value;             OnPropertyChanged(); } }
        public string GyroZ             { get => gyroZ;             set { gyroZ = value;             OnPropertyChanged(); } }
        public string MagX              { get => magX;              set { magX = value;              OnPropertyChanged(); } }
        public string MagY              { get => magY;              set { magY = value;              OnPropertyChanged(); } }
        public string MagZ              { get => magZ;              set { magZ = value;              OnPropertyChanged(); } }
        public string AccelMagnitude    { get => accelMagnitude;    set { accelMagnitude = value;    OnPropertyChanged(); } }
        public string CurrentThreshold  { get => currentThreshold;  set { currentThreshold = value;  OnPropertyChanged(); } }
        public string Timestamp         { get => timestamp;         set { timestamp = value;         OnPropertyChanged(); } }

        public string Heading     => trackingViewModel.Heading;
        public string TrueHeading => trackingViewModel.Heading;
        public string Steps       => trackingViewModel.Steps.ToString();

        public DiagnosticsViewModel(ISensorService sensorService, TrackingViewModel trackingViewModel)
        {
            this.sensorService     = sensorService     ?? throw new ArgumentNullException(nameof(sensorService));
            this.trackingViewModel = trackingViewModel ?? throw new ArgumentNullException(nameof(trackingViewModel));
        }

        // Called from DiagnosticsPage.OnAppearing
        public void Activate()
        {
            if (active) return;
            active = true;
            sensorService.SensorDataReceived += OnSensorDataReceived;
        }

        // Called from DiagnosticsPage.OnDisappearing
        public void Deactivate()
        {
            if (!active) return;
            active = false;
            sensorService.SensorDataReceived -= OnSensorDataReceived;
        }

        private void OnSensorDataReceived(double[] accel, double[] gyro, double[] mag)
        {
            double totalMag = Math.Sqrt(accel[0]*accel[0] + accel[1]*accel[1] + accel[2]*accel[2]);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AccelX = accel[0].ToString("F3"); AccelY = accel[1].ToString("F3"); AccelZ = accel[2].ToString("F3");
                GyroX  = gyro[0].ToString("F3");  GyroY  = gyro[1].ToString("F3");  GyroZ  = gyro[2].ToString("F3");
                MagX   = mag[0].ToString("F3");   MagY   = mag[1].ToString("F3");   MagZ   = mag[2].ToString("F3");
                AccelMagnitude = totalMag.ToString("F3");
                Timestamp      = DateTime.Now.ToString("HH:mm:ss.fff");
                OnPropertyChanged(nameof(Heading));
                OnPropertyChanged(nameof(TrueHeading));
                OnPropertyChanged(nameof(Steps));
            });
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Deactivate();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
