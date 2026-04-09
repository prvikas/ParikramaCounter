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

        private readonly ISensorService    sensorService;
        private readonly SensorFusionEngine fusionEngine;
        private readonly ParikramaTracker  parikramaTracker = new ParikramaTracker();
        private readonly SettingsViewModel settings;

        private const int TotalSides = 4;

        // ── Backing fields ────────────────────────────────────────────────────────
        private bool   isTracking;
        private string heading          = "0°";
        private string direction        = "N";
        private int    steps;
        private int    parikramaCount;
        private int    targetParikrama  = 7;
        private double progressPercentage;
        private string startStopButtonText = "Start";
        private bool   targetReached;
        private string movementStatus   = "Stationary";
        private string sidesInfo        = $"0/{TotalSides} sides";
        private double circleProgress;
        private string circleDirection  = "Determining...";
        private int    stepsInCircle;
        private string countModeLabel   = "Ascending";

        // ── Properties ────────────────────────────────────────────────────────────

        public bool IsTracking
        {
            get => isTracking;
            set { isTracking = value; StartStopButtonText = value ? "Stop" : "Start"; OnPropertyChanged(); }
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
                OnPropertyChanged(nameof(DisplayCount));
                UpdateProgress();
                SaveCount();
            }
        }

        // DisplayCount shows the count differently for ascending vs descending mode
        public string DisplayCount => settings.IsDescendingMode
            ? $"{Math.Max(0, TargetParikrama - parikramaCount)}"
            : $"{parikramaCount}";

        public int TargetParikrama
        {
            get => targetParikrama;
            set
            {
                targetParikrama = value;
                parikramaTracker.TargetParikramaCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingParikramas));
                OnPropertyChanged(nameof(DisplayCount));
                UpdateProgress();
                Preferences.Set("TargetParikrama", value);
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

        public string CountModeLabel
        {
            get => countModeLabel;
            set { countModeLabel = value; OnPropertyChanged(); }
        }

        // ── Commands ──────────────────────────────────────────────────────────────

        public ICommand StartStopCommand          { get; }
        public ICommand ResetCommand              { get; }
        public ICommand IncrementTargetCommand    { get; }
        public ICommand DecrementTargetCommand    { get; }
        public ICommand ManualIncrementCommand    { get; }   // manual +1 pradhakshina
        public ICommand ManualDecrementCommand    { get; }   // manual -1 pradhakshina
        public ICommand ToggleCountModeCommand    { get; }   // ascending ↔ descending

        // ── Constructor ───────────────────────────────────────────────────────────

        public TrackingViewModel(ISensorService sensorService, SensorFusionEngine fusionEngine, SettingsViewModel settings)
        {
            this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));
            this.fusionEngine  = fusionEngine  ?? throw new ArgumentNullException(nameof(fusionEngine));
            this.settings      = settings      ?? throw new ArgumentNullException(nameof(settings));

            this.sensorService.SensorDataReceived += OnSensorDataReceived;
            parikramaTracker.OnThirdSideCompleted += OnThirdSideCompleted;
            parikramaTracker.OnApproachingStart   += OnApproachingStart;

            // Settings changes that affect display need re-notification
            settings.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.IsDescendingMode))
                {
                    CountModeLabel = settings.IsDescendingMode ? "Descending" : "Ascending";
                    OnPropertyChanged(nameof(DisplayCount));
                }
            };

            StartStopCommand       = new Command(StartStop);
            ResetCommand           = new Command(Reset);
            IncrementTargetCommand = new Command(() => { if (TargetParikrama < 108) TargetParikrama++; });
            DecrementTargetCommand = new Command(() => { if (TargetParikrama > 1)   TargetParikrama--; });
            ManualIncrementCommand = new Command(ManualIncrement);
            ManualDecrementCommand = new Command(ManualDecrement);
            ToggleCountModeCommand = new Command(() => settings.IsDescendingMode = !settings.IsDescendingMode);

            // Restore persisted state
            targetParikrama = Preferences.Get("TargetParikrama", 7);
            parikramaCount  = Preferences.Get("ParikramaCount",  0);
            parikramaTracker.TargetParikramaCount = targetParikrama;
            CountModeLabel  = settings.IsDescendingMode ? "Descending" : "Ascending";
            UpdateProgress();
        }

        // ── Sensor data handler ───────────────────────────────────────────────────

        private void OnSensorDataReceived(double[] accel, double[] gyro, double[] mag)
        {
            var data = fusionEngine.ProcessSensorData(accel, gyro, mag);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Heading        = $"{data.Heading:F1}°";
                Direction      = data.Direction;
                Steps          = data.Steps;
                MovementStatus = fusionEngine.IsMoving ? "🚶 Walking" : "🛑 Stationary";

                CircleProgress  = parikramaTracker.CurrentProgress;
                CircleDirection = parikramaTracker.GetDirection();
                StepsInCircle   = parikramaTracker.CurrentStepsInCircle;
                SidesInfo       = $"{parikramaTracker.SidesCompleted}/{TotalSides} sides";

                // Only auto-count when tracking is active AND auto-counting is enabled
                if (!isTracking || !settings.AutoCountingEnabled) return;

                bool completed = parikramaTracker.CheckAndUpdateParikrama(
                    data.Heading, data.Steps, fusionEngine.IsMoving, data.Timestamp);

                if (completed)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"🎉 Pradhakshina #{parikramaTracker.ParikramaCount}");
#endif
                    ParikramaCount = parikramaTracker.ParikramaCount;
                    HandleCompletion();
                }
            });
        }

        // ── Manual counting ───────────────────────────────────────────────────────

        private void ManualIncrement()
        {
            if (ParikramaCount < TargetParikrama)
            {
                ParikramaCount++;
                parikramaTracker.ManualSetCount(parikramaCount);
                HandleCompletion();
            }
        }

        private void ManualDecrement()
        {
            if (ParikramaCount > 0)
            {
                ParikramaCount--;
                TargetReached = false;
                parikramaTracker.ManualSetCount(parikramaCount);
            }
        }

        private void HandleCompletion()
        {
            if (parikramaTracker.IsTargetReached && !TargetReached)
            {
                TargetReached = true;
                _ = VibrateForTargetCompletionAsync();
            }
            else if (!TargetReached)
            {
                VibrateForParikramaCompletion();
            }
        }

        // ── Vibration event callbacks ─────────────────────────────────────────────

        private void OnThirdSideCompleted()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("🔔 3rd side completed");
#endif
                if (settings.EnableVibrations)
                    Vibrate(settings.ThirdSideVibrationMs);
            });
        }

        private void OnApproachingStart()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("⚠️ Approaching start point");
#endif
                if (settings.EnableVibrations)
                    Vibrate(settings.ApproachingStartVibrationMs);
            });
        }

        // ── Vibration helpers ─────────────────────────────────────────────────────

        private void Vibrate(int ms)
        {
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(ms)); }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
#endif
            }
        }

        private void VibrateForParikramaCompletion()
        {
            if (settings.EnableVibrations) Vibrate(settings.CompletionVibrationMs);
        }

        private async Task VibrateForTargetCompletionAsync()
        {
            if (!settings.EnableVibrations) return;
            try
            {
                for (int i = 0; i < settings.TargetVibrationCount; i++)
                {
                    Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(settings.TargetVibrationMs));
                    await Task.Delay(settings.TargetVibrationMs + 200);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
#endif
            }
        }

        // ── Commands impl ─────────────────────────────────────────────────────────

        private void StartStop()
        {
            if (IsTracking) { sensorService.Stop();  IsTracking = false; }
            else            { sensorService.Start(); IsTracking = true;  }
        }

        private void Reset()
        {
            if (IsTracking) { sensorService.Stop(); IsTracking = false; }
            fusionEngine.Reset();
            parikramaTracker.Reset();
            ParikramaCount  = 0;
            TargetReached   = false;
            MovementStatus  = "Stationary";
            SidesInfo       = $"0/{TotalSides} sides";
            CircleDirection = "Determining...";
            CircleProgress  = 0;
            StepsInCircle   = 0;
            Steps           = 0;
            UpdateProgress();
        }

        private void UpdateProgress()
        {
            ProgressPercentage = TargetParikrama > 0
                ? (double)ParikramaCount / TargetParikrama
                : 0;
        }

        private void SaveCount() => Preferences.Set("ParikramaCount", parikramaCount);

        // ── IDisposable ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (isTracking) sensorService.Stop();
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
