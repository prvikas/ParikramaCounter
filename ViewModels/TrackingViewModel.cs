using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using ParikramaCounter.Services;
using ParikramaCounter.Models;

namespace ParikramaCounter.ViewModels
{
    public class TrackingViewModel : INotifyPropertyChanged
    {
        private readonly ISensorService sensorService;
        private readonly SensorFusionEngine fusionEngine = new SensorFusionEngine();
        private readonly ParikramaTracker parikramaTracker = new ParikramaTracker();

        private DateTime lastUIUpdate = DateTime.MinValue;
        private const int UI_UPDATE_INTERVAL_MS = 500;
        private bool hasStartedTracking = false; // ADDED

        private bool isTracking;
        private string heading = "0°";
        private string direction = "N";
        private int parikramaCount;
        private int targetParikrama = 7;
        private double circleProgress;
        private string circleDirection = "Determining...";
        private double distanceInCircle;
        private string startStopButtonText = "Start";
        private bool targetReached;
        private string movementStatus = "Stationary";
        private string calibrationStatus = "Initializing..."; // ADDED

        public bool IsTracking
        {
            get => isTracking;
            set
            {
                isTracking = value;
                StartStopButtonText = value ? "Stop" : "Start";
                OnPropertyChanged();
            }
        }

        public string StartStopButtonText
        {
            get => startStopButtonText;
            set { startStopButtonText = value; OnPropertyChanged(); }
        }

        public string Heading
        {
            get => heading;
            set { heading = value; OnPropertyChanged(); }
        }

        public string Direction
        {
            get => direction;
            set { direction = value; OnPropertyChanged(); }
        }

        public int ParikramaCount
        {
            get => parikramaCount;
            set
            {
                parikramaCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingParikramas));
            }
        }

        public int TargetParikrama
        {
            get => targetParikrama;
            set
            {
                targetParikrama = value;
                parikramaTracker.SetTarget(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingParikramas));
            }
        }

        public int RemainingParikramas => Math.Max(0, TargetParikrama - ParikramaCount);

        public double CircleProgress
        {
            get => circleProgress;
            set { circleProgress = value; OnPropertyChanged(); }
        }

        public string CircleDirection
        {
            get => circleDirection;
            set { circleDirection = value; OnPropertyChanged(); }
        }

        public double DistanceInCircle
        {
            get => distanceInCircle;
            set { distanceInCircle = value; OnPropertyChanged(); }
        }

        public bool TargetReached
        {
            get => targetReached;
            set { targetReached = value; OnPropertyChanged(); }
        }

        public string MovementStatus
        {
            get => movementStatus;
            set { movementStatus = value; OnPropertyChanged(); }
        }

        // ADDED: Calibration status
        public string CalibrationStatus
        {
            get => calibrationStatus;
            set { calibrationStatus = value; OnPropertyChanged(); }
        }

        public ICommand StartStopCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand IncrementTargetCommand { get; }
        public ICommand DecrementTargetCommand { get; }

        public TrackingViewModel(ISensorService sensorService)
        {
            this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));
            this.sensorService.SensorDataReceived += OnSensorDataReceived;

            StartStopCommand = new Command(StartStop);
            ResetCommand = new Command(Reset);
            IncrementTargetCommand = new Command(() => TargetParikrama++);
            DecrementTargetCommand = new Command(() => { if (TargetParikrama > 1) TargetParikrama--; });

            parikramaTracker.SetTarget(targetParikrama);

            // ADDED: Start sensors immediately for background calibration
            sensorService.Start();
        }

        private void OnSensorDataReceived(double[] accel, double[] gyro, double[] mag)
        {
            var data = fusionEngine.ProcessSensorData(accel, gyro, mag);

            // ADDED: Update calibration status
            if (!fusionEngine.IsCalibrated)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CalibrationStatus = "⏳ Calibrating compass...";
                });
                return; // Don't track yet
            }
            else if (CalibrationStatus != "")
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CalibrationStatus = ""; // Hide message
                });
            }

            // ADDED: Initialize tracker with calibrated heading on first start
            if (isTracking && !hasStartedTracking)
            {
                parikramaTracker.StartTracking(fusionEngine.CalibratedStartHeading);
                hasStartedTracking = true;
            }

            // Update tracking logic
            bool completed = false;
            if (isTracking)
            {
                completed = parikramaTracker.Update(data.Heading, fusionEngine.IsMoving);
            }

            // Throttle UI updates
            bool shouldUpdate = (DateTime.Now - lastUIUpdate).TotalMilliseconds >= UI_UPDATE_INTERVAL_MS || completed;

            if (!shouldUpdate)
            {
                return;
            }

            lastUIUpdate = DateTime.Now;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Heading = $"{data.Heading:F1}°";
                Direction = data.Direction;
                MovementStatus = fusionEngine.IsMoving ? "🚶 Walking" : "🛑 Stationary";
                CircleProgress = parikramaTracker.CircleProgress / 100.0;
                CircleDirection = parikramaTracker.GetDirection();
                DistanceInCircle = parikramaTracker.CurrentDistanceInCircle;

                if (completed)
                {
                    System.Diagnostics.Debug.WriteLine($"🎉 UI UPDATE: Parikrama #{parikramaTracker.ParikramaCount}");

                    ParikramaCount = parikramaTracker.ParikramaCount;
                    OnPropertyChanged(nameof(ParikramaCount));
                    OnPropertyChanged(nameof(RemainingParikramas));

                    CircleProgress = 0;
                    DistanceInCircle = 0;

                    // Re-initialize for next circle
                    hasStartedTracking = false;

                    if (parikramaTracker.IsTargetReached && !TargetReached)
                    {
                        TargetReached = true;
                        VibrateForTargetCompletion();
                    }
                    else
                    {
                        VibrateForParikramaCompletion();
                    }
                }
            });
        }

        private void VibrateForParikramaCompletion()
        {
            try
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(500));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
            }
        }

        private async void VibrateForTargetCompletion()
        {
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(300));
                    await System.Threading.Tasks.Task.Delay(200);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
            }
        }

        private void StartStop()
        {
            if (IsTracking)
            {
                // Stop tracking (but keep sensors running for calibration)
                IsTracking = false;
                hasStartedTracking = false;
            }
            else
            {
                // Check if calibrated
                if (!fusionEngine.IsCalibrated)
                {
                    CalibrationStatus = "⚠️ Please wait, calibrating...";
                    return;
                }

                // Start tracking
                IsTracking = true;
            }
        }

        private void Reset()
        {
            fusionEngine.Reset();
            parikramaTracker.Reset();
            ParikramaCount = 0;
            TargetReached = false;
            MovementStatus = "Stationary";
            CircleProgress = 0;
            DistanceInCircle = 0;
            hasStartedTracking = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
