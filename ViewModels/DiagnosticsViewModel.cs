using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    // Fix #6: observes ISensorPipeline.SensorProcessed — receives already-processed
    // SensorData instead of subscribing to raw ISensorService.SensorDataReceived and
    // calling fusionEngine.ProcessSensorData a second time per tick.
    public class DiagnosticsViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ISensorPipeline     pipeline;
        private readonly ISensorFusionEngine fusionEngine;
        private bool active;
        private bool disposed;

        private string accelX = "0.000", accelY = "0.000", accelZ = "0.000";
        private string gyroX  = "0.000", gyroY  = "0.000", gyroZ  = "0.000";
        private string magX   = "0.000", magY   = "0.000", magZ   = "0.000";
        private string accelMagnitude   = "0.000";
        private string currentThreshold = "dynamic";
        private string timestamp        = DateTime.Now.ToString("HH:mm:ss.fff");
        private string heading          = "0.0°";
        private string steps            = "0";
        private string isMoving         = "Stationary";

        public string AccelX           { get => accelX;           set { accelX = value;           OnPropertyChanged(); } }
        public string AccelY           { get => accelY;           set { accelY = value;           OnPropertyChanged(); } }
        public string AccelZ           { get => accelZ;           set { accelZ = value;           OnPropertyChanged(); } }
        public string GyroX            { get => gyroX;            set { gyroX = value;            OnPropertyChanged(); } }
        public string GyroY            { get => gyroY;            set { gyroY = value;            OnPropertyChanged(); } }
        public string GyroZ            { get => gyroZ;            set { gyroZ = value;            OnPropertyChanged(); } }
        public string MagX             { get => magX;             set { magX = value;             OnPropertyChanged(); } }
        public string MagY             { get => magY;             set { magY = value;             OnPropertyChanged(); } }
        public string MagZ             { get => magZ;             set { magZ = value;             OnPropertyChanged(); } }
        public string AccelMagnitude   { get => accelMagnitude;   set { accelMagnitude = value;   OnPropertyChanged(); } }
        public string CurrentThreshold { get => currentThreshold; set { currentThreshold = value; OnPropertyChanged(); } }
        public string Timestamp        { get => timestamp;        set { timestamp = value;        OnPropertyChanged(); } }
        public string Heading          { get => heading;          set { heading = value;          OnPropertyChanged(); } }
        public string TrueHeading      => Heading;
        public string Steps            { get => steps;            set { steps = value;            OnPropertyChanged(); } }
        public string IsMovingStatus   { get => isMoving;         set { isMoving = value;         OnPropertyChanged(); } }

        public DiagnosticsViewModel(ISensorPipeline pipeline, ISensorFusionEngine fusionEngine)
        {
            this.pipeline    = pipeline    ?? throw new ArgumentNullException(nameof(pipeline));
            this.fusionEngine = fusionEngine ?? throw new ArgumentNullException(nameof(fusionEngine));
        }

        // Called from DiagnosticsPage.OnAppearing
        public void Activate()
        {
            if (active) return;
            active = true;
            pipeline.SensorProcessed += OnSensorProcessed;
        }

        // Called from DiagnosticsPage.OnDisappearing
        public void Deactivate()
        {
            if (!active) return;
            active = false;
            pipeline.SensorProcessed -= OnSensorProcessed;
        }

        private void OnSensorProcessed(Models.SensorData data)
        {
            try
            {
                double totalAccelMag = Math.Sqrt(
                    data.AccelX * data.AccelX +
                    data.AccelY * data.AccelY +
                    data.AccelZ * data.AccelZ);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AccelX = data.AccelX.ToString("F3");
                    AccelY = data.AccelY.ToString("F3");
                    AccelZ = data.AccelZ.ToString("F3");
                    GyroX  = data.GyroX.ToString("F3");
                    GyroY  = data.GyroY.ToString("F3");
                    GyroZ  = data.GyroZ.ToString("F3");
                    MagX   = data.MagX.ToString("F3");
                    MagY   = data.MagY.ToString("F3");
                    MagZ   = data.MagZ.ToString("F3");
                    AccelMagnitude = totalAccelMag.ToString("F3");
                    Heading        = $"{data.Heading:F1}°";
                    Steps          = data.Steps.ToString();
                    IsMovingStatus = fusionEngine.IsMoving ? "🚶 Walking" : "🛑 Stationary";
                    Timestamp      = DateTime.Now.ToString("HH:mm:ss.fff");
                    OnPropertyChanged(nameof(TrueHeading));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DiagnosticsViewModel] Display error: {ex.Message}");
            }
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
