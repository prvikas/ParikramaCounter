using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    public class DiagnosticsViewModel : INotifyPropertyChanged
    {
        private readonly ISensorService sensorService;
        private readonly SensorFusionEngine fusionEngine = new SensorFusionEngine();

        private string accelX = "0.00";
        private string accelY = "0.00";
        private string accelZ = "0.00";
        private string gyroX = "0.00";
        private string gyroY = "0.00";
        private string gyroZ = "0.00";
        private string magX = "0.00";
        private string magY = "0.00";
        private string magZ = "0.00";
        private string heading = "0.00";
        private string trueHeading = "0.00";
        private string accelMagnitude = "0.00";
        private string currentThreshold = "0.00";
        private string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        private bool isMonitoring;

        public string AccelX { get => accelX; set { accelX = value; OnPropertyChanged(); } }
        public string AccelY { get => accelY; set { accelY = value; OnPropertyChanged(); } }
        public string AccelZ { get => accelZ; set { accelZ = value; OnPropertyChanged(); } }
        public string GyroX { get => gyroX; set { gyroX = value; OnPropertyChanged(); } }
        public string GyroY { get => gyroY; set { gyroY = value; OnPropertyChanged(); } }
        public string GyroZ { get => gyroZ; set { gyroZ = value; OnPropertyChanged(); } }
        public string MagX { get => magX; set { magX = value; OnPropertyChanged(); } }
        public string MagY { get => magY; set { magY = value; OnPropertyChanged(); } }
        public string MagZ { get => magZ; set { magZ = value; OnPropertyChanged(); } }
        public string Heading { get => heading; set { heading = value; OnPropertyChanged(); } }
        public string TrueHeading { get => trueHeading; set { trueHeading = value; OnPropertyChanged(); } }
        public string AccelMagnitude { get => accelMagnitude; set { accelMagnitude = value; OnPropertyChanged(); } }
        public string CurrentThreshold { get => currentThreshold; set { currentThreshold = value; OnPropertyChanged(); } }
        public string Timestamp { get => timestamp; set { timestamp = value; OnPropertyChanged(); } }
        public bool IsMonitoring { get => isMonitoring; set { isMonitoring = value; OnPropertyChanged(); } }

        public DiagnosticsViewModel(ISensorService sensorService)
        {
            this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));
            this.sensorService.SensorDataReceived += OnSensorDataReceived;
            this.sensorService.Start();
            IsMonitoring = true;
        }

        private void OnSensorDataReceived(double[] accel, double[] gyro, double[] mag)
        {
            var data = fusionEngine.ProcessSensorData(accel, gyro, mag);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                AccelX = data.AccelX.ToString("F3");
                AccelY = data.AccelY.ToString("F3");
                AccelZ = data.AccelZ.ToString("F3");
                GyroX = data.GyroX.ToString("F3");
                GyroY = data.GyroY.ToString("F3");
                GyroZ = data.GyroZ.ToString("F3");
                MagX = data.MagX.ToString("F3");
                MagY = data.MagY.ToString("F3");
                MagZ = data.MagZ.ToString("F3");
                Heading = data.Heading.ToString("F2");
                TrueHeading = data.TrueHeading.ToString("F2");
                AccelMagnitude = data.AccelerationMagnitude.ToString("F3");
                Timestamp = data.Timestamp.ToString("HH:mm:ss.fff");
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
