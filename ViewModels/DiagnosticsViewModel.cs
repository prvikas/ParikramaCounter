using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    // Fix #16: implement IDisposable so the sensor subscription is released when
    // the page is torn down, preventing a dangling event handler and double-start.
    public class DiagnosticsViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ISensorService sensorService;
        private readonly SensorFusionEngine fusionEngine = new SensorFusionEngine();
        private bool disposed;

        private string accelX = "0.000";
        private string accelY = "0.000";
        private string accelZ = "0.000";
        private string gyroX = "0.000";
        private string gyroY = "0.000";
        private string gyroZ = "0.000";
        private string magX = "0.000";
        private string magY = "0.000";
        private string magZ = "0.000";
        private string heading = "0.00";
        private string trueHeading = "0.00";
        private string accelMagnitude = "0.000";
        private string currentThreshold = "0.000";
        private string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

        public string AccelX        { get => accelX;        set { accelX = value;        OnPropertyChanged(); } }
        public string AccelY        { get => accelY;        set { accelY = value;        OnPropertyChanged(); } }
        public string AccelZ        { get => accelZ;        set { accelZ = value;        OnPropertyChanged(); } }
        public string GyroX         { get => gyroX;         set { gyroX = value;         OnPropertyChanged(); } }
        public string GyroY         { get => gyroY;         set { gyroY = value;         OnPropertyChanged(); } }
        public string GyroZ         { get => gyroZ;         set { gyroZ = value;         OnPropertyChanged(); } }
        public string MagX          { get => magX;          set { magX = value;          OnPropertyChanged(); } }
        public string MagY          { get => magY;          set { magY = value;          OnPropertyChanged(); } }
        public string MagZ          { get => magZ;          set { magZ = value;          OnPropertyChanged(); } }
        public string Heading       { get => heading;       set { heading = value;       OnPropertyChanged(); } }
        public string TrueHeading   { get => trueHeading;   set { trueHeading = value;   OnPropertyChanged(); } }
        public string AccelMagnitude{ get => accelMagnitude;set { accelMagnitude = value;OnPropertyChanged(); } }
        public string CurrentThreshold{get=>currentThreshold;set{currentThreshold=value;OnPropertyChanged();}}
        public string Timestamp     { get => timestamp;     set { timestamp = value;     OnPropertyChanged(); } }

        public DiagnosticsViewModel(ISensorService sensorService)
        {
            this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));

            // Fix #16: subscribe only — do NOT call sensorService.Start() here.
            // TrackingViewModel owns the sensor lifecycle. DiagnosticsViewModel
            // is a read-only observer; calling Start() again on Android registers
            // the listener twice, doubling callback rate.
            this.sensorService.SensorDataReceived += OnSensorDataReceived;
        }

        private void OnSensorDataReceived(double[] accel, double[] gyro, double[] mag)
        {
            var data = fusionEngine.ProcessSensorData(accel, gyro, mag);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                AccelX          = data.AccelX.ToString("F3");
                AccelY          = data.AccelY.ToString("F3");
                AccelZ          = data.AccelZ.ToString("F3");
                GyroX           = data.GyroX.ToString("F3");
                GyroY           = data.GyroY.ToString("F3");
                GyroZ           = data.GyroZ.ToString("F3");
                MagX            = data.MagX.ToString("F3");
                MagY            = data.MagY.ToString("F3");
                MagZ            = data.MagZ.ToString("F3");
                Heading         = data.Heading.ToString("F2");
                TrueHeading     = data.TrueHeading.ToString("F2");
                AccelMagnitude  = data.AccelerationMagnitude.ToString("F3");
                Timestamp       = data.Timestamp.ToString("HH:mm:ss.fff");
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
