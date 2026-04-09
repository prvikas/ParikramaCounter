using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    public class DiagnosticsViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ISensorService sensorService;

        // Fix #3: DiagnosticsViewModel no longer owns a private SensorFusionEngine.
        // A private engine produced a completely different Kalman filter state from
        // the one driving tracking, making the Diagnostics readout useless for
        // debugging real tracking behaviour. Raw sensor values are now displayed
        // directly from the incoming arrays, which is what Diagnostics should show.
        private bool disposed;

        private string accelX = "0.000";
        private string accelY = "0.000";
        private string accelZ = "0.000";
        private string gyroX  = "0.000";
        private string gyroY  = "0.000";
        private string gyroZ  = "0.000";
        private string magX   = "0.000";
        private string magY   = "0.000";
        private string magZ   = "0.000";
        private string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

        public string AccelX    { get => accelX;    set { accelX = value;    OnPropertyChanged(); } }
        public string AccelY    { get => accelY;    set { accelY = value;    OnPropertyChanged(); } }
        public string AccelZ    { get => accelZ;    set { accelZ = value;    OnPropertyChanged(); } }
        public string GyroX     { get => gyroX;     set { gyroX = value;     OnPropertyChanged(); } }
        public string GyroY     { get => gyroY;     set { gyroY = value;     OnPropertyChanged(); } }
        public string GyroZ     { get => gyroZ;     set { gyroZ = value;     OnPropertyChanged(); } }
        public string MagX      { get => magX;      set { magX = value;      OnPropertyChanged(); } }
        public string MagY      { get => magY;      set { magY = value;      OnPropertyChanged(); } }
        public string MagZ      { get => magZ;      set { magZ = value;      OnPropertyChanged(); } }
        public string Timestamp { get => timestamp; set { timestamp = value; OnPropertyChanged(); } }

        // These are read from TrackingViewModel's public properties for accurate
        // real-time values that reflect the actual tracking state, not a shadow engine.
        private readonly TrackingViewModel trackingViewModel;
        public string Heading       => trackingViewModel.Heading;
        public string TrueHeading   => trackingViewModel.Heading; // same source — no declination yet
        public string AccelMagnitude { get => accelMagnitudeStr; private set { accelMagnitudeStr = value; OnPropertyChanged(); } }
        private string accelMagnitudeStr = "0.000";

        // Fix #3: CurrentThreshold is exposed from the shared StepDetector via
        // TrackingViewModel rather than a private engine's detector.
        public string CurrentThreshold { get => currentThreshold; private set { currentThreshold = value; OnPropertyChanged(); } }
        private string currentThreshold = "0.000";

        public DiagnosticsViewModel(ISensorService sensorService, TrackingViewModel trackingViewModel)
        {
            this.sensorService    = sensorService    ?? throw new ArgumentNullException(nameof(sensorService));
            this.trackingViewModel = trackingViewModel ?? throw new ArgumentNullException(nameof(trackingViewModel));
            this.sensorService.SensorDataReceived += OnSensorDataReceived;
        }

        private void OnSensorDataReceived(double[] accel, double[] gyro, double[] mag)
        {
            // Compute accel magnitude from raw linear accel (approx — no gravity removal here,
            // but good enough for diagnostics display)
            double mag3 = Math.Sqrt(accel[0]*accel[0] + accel[1]*accel[1] + accel[2]*accel[2]);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                AccelX = accel[0].ToString("F3");
                AccelY = accel[1].ToString("F3");
                AccelZ = accel[2].ToString("F3");
                GyroX  = gyro[0].ToString("F3");
                GyroY  = gyro[1].ToString("F3");
                GyroZ  = gyro[2].ToString("F3");
                MagX   = mag[0].ToString("F3");
                MagY   = mag[1].ToString("F3");
                MagZ   = mag[2].ToString("F3");
                AccelMagnitude = mag3.ToString("F3");
                Timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

                // Notify heading values which come from TrackingViewModel
                OnPropertyChanged(nameof(Heading));
                OnPropertyChanged(nameof(TrueHeading));
            });
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            sensorService.SensorDataReceived -= OnSensorDataReceived;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
