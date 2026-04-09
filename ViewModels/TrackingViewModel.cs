using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using ParikramaCounter.Models;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    public class TrackingViewModel : INotifyPropertyChanged, IDisposable
    {
        private bool disposed = false;

        private readonly ISensorService sensorService;

        // Fix #7: SensorFusionEngine is now injected as a singleton from DI so
        // TrackingViewModel, DiagnosticsViewModel, and SettingsViewModel all share
        // the same engine instance. Live setting changes in SettingsViewModel
        // immediately affect the engine used for tracking and diagnostics.
        private readonly SensorFusionEngine fusionEngine;
        private readonly ParikramaTracker parikramaTracker = new ParikramaTracker();

        private const int TotalSides = 4;

        private bool isTracking;
        private string heading = "0°";
        private string direction = "N";
        private int steps;
        private int parikramaCount;
        private int targetParikrama = 7;
        private double progressPercentage;
        private string startStopButtonText = "Start";
        private bool targetReached;
        private string movementStatus = "Stationary";
        private string sidesInfo = $"0/{TotalSides} sides";
        private double circleProgress;
        private string circleDirection = "Determining...";
        private int stepsInCircle;

        // ── Properties ────────────────────────────────────────────────────────────

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

        public int Steps
        {
            get => steps;
            set { steps = value; OnPropertyChanged(); }
        }

        public int ParikramaCount
        {
            get => parikramaCount;
            set
            {
                parikramaCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingParikramas));
                UpdateProgress();
            }
        }

        public int TargetParikrama
        {
            get => targetParikrama;
            set
            {
                targetParikrama = value;
                parikramaTracker.TargetParikramaCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingParikramas));
                UpdateProgress();
            }
        }

        public int RemainingParikramas => Math.Max(0, TargetParikrama - ParikramaCount);

        public double ProgressPercentage
        {
            get => progressPercentage;
            set { progressPercentage = value; OnPropertyChanged(); }
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

        public string SidesInfo
        {
            get => sidesInfo;
            set { sidesInfo = value; OnPropertyChanged(); }
        }

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

        public int StepsInCircle
        {
            get => stepsInCircle;
            set { stepsInCircle = value; OnPropertyChanged(); }
        }

        public ICommand StartStopCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand IncrementTargetCommand { get; }
        public ICommand DecrementTargetCommand { get; }

        // ── Constructor ───────────────────────────────────────────────────────────

        public TrackingViewModel(ISensorService sensorService, SensorFusionEngine fusionEngine)
        {
            this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));
            this.fusionEngine  = fusionEngine  ?? throw new ArgumentNullException(nameof(fusionEngine));

            this.sensorService.SensorDataReceived += OnSensorDataReceived;
            parikramaTracker.OnThirdSideCompleted += OnThirdSideCompleted;
            parikramaTracker.OnApproachingStart   += OnApproachingStart;

            StartStopCommand       = new Command(StartStop);
            ResetCommand           = new Command(Reset);
            IncrementTargetCommand = new Command(() => { if (TargetParikrama < 108) TargetParikrama++; });
            DecrementTargetCommand = new Command(() => { if (TargetParikrama > 1)   TargetParikrama--; });

            parikramaTracker.TargetParikramaCount = targetParikrama;
        }

        // ── Sensor data handler ───────────────────────────────────────────────────

        private void OnSensorDataReceived(double[] accel, double[] gyro, double[] mag)
        {
            var data = fusionEngine.ProcessSensorData(accel, gyro, mag);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Heading       = $"{data.Heading:F1}°";
                Direction     = data.Direction;
                Steps         = data.Steps;
                MovementStatus = fusionEngine.IsMoving ? "🚶 Walking" : "🛑 Stationary";

                CircleProgress = parikramaTracker.CurrentProgress;
                CircleDirection = parikramaTracker.GetDirection();
                StepsInCircle  = parikramaTracker.CurrentStepsInCircle;
                SidesInfo      = $"{parikramaTracker.SidesCompleted}/{TotalSides} sides";

                // Only update tracking state when actively tracking
                if (!isTracking) return;

                bool completed = parikramaTracker.CheckAndUpdateParikrama(
                    data.Heading,
                    data.Steps,
                    fusionEngine.IsMoving,
                    data.Timestamp
                );

                if (completed)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"🎉 Parikrama #{parikramaTracker.ParikramaCount}");
#endif
                    ParikramaCount = parikramaTracker.ParikramaCount;

                    if (parikramaTracker.IsTargetReached && !TargetReached)
                    {
                        TargetReached = true;
                        _ = VibrateForTargetCompletionAsync();
                    }
                    else
                    {
                        VibrateForParikramaCompletion();
                    }
                }
            });
        }

        // ── Vibration event callbacks ─────────────────────────────────────────────

        private void OnThirdSideCompleted()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("🔔 3rd side completed (250°–290°)");
#endif
                VibrateForThirdSide();
            });
        }

        private void OnApproachingStart()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("⚠️ Approaching start (320°–350°)");
#endif
                VibrateForApproachingStart();
            });
        }

        // ── Vibration methods ─────────────────────────────────────────────────────

        private void VibrateForThirdSide()
        {
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(400)); }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
#endif
            }
        }

        private void VibrateForApproachingStart()
        {
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(200)); }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
#endif
            }
        }

        private void VibrateForParikramaCompletion()
        {
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(500)); }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
#endif
            }
        }

        private async Task VibrateForTargetCompletionAsync()
        {
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(300));
                    await Task.Delay(500);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
#endif
            }
        }

        // ── Commands ──────────────────────────────────────────────────────────────

        private void StartStop()
        {
            if (IsTracking)
            {
                sensorService.Stop();
                IsTracking = false;
            }
            else
            {
                sensorService.Start();
                IsTracking = true;
            }
        }

        private void Reset()
        {
            if (IsTracking)
            {
                sensorService.Stop();
                IsTracking = false;
            }

            fusionEngine.Reset();
            parikramaTracker.Reset();

            Steps          = 0;
            ParikramaCount = 0;
            TargetReached  = false;
            MovementStatus = "Stationary";
            SidesInfo      = $"0/{TotalSides} sides";
            CircleDirection = "Determining...";
            CircleProgress  = 0;
            StepsInCircle   = 0;

            UpdateProgress();
        }

        private void UpdateProgress()
        {
            ProgressPercentage = TargetParikrama > 0
                ? (double)ParikramaCount / TargetParikrama
                : 0;
        }

        // ── IDisposable ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if (isTracking)
                sensorService.Stop();

            sensorService.SensorDataReceived      -= OnSensorDataReceived;
            parikramaTracker.OnThirdSideCompleted -= OnThirdSideCompleted;
            parikramaTracker.OnApproachingStart   -= OnApproachingStart;
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────────

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
