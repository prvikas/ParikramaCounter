using System;
using System.Collections.Generic;
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

        private readonly ISensorService     sensorService;
        private readonly SensorFusionEngine fusionEngine;
        private readonly ParikramaTracker   parikramaTracker = new ParikramaTracker();
        private readonly SettingsViewModel  settings;

        private const int TotalSides = 4;

        // Traditional pradhakshina counts used across temples.
        // Backed by List<int> so IndexOf works correctly.
        public static readonly List<int> PresetTargets = new List<int>
            { 1, 3, 5, 7, 9, 11, 12, 21, 27, 54, 63, 108 };

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
        private int    selectedPresetIndex;

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

        public string DisplayCount => settings.IsDescendingMode
            ? $"{Math.Max(0, TargetParikrama - parikramaCount)}"
            : $"{parikramaCount}";

        public int TargetParikrama
        {
            get => targetParikrama;
            set
            {
                if (value < 1) return;
                targetParikrama = value;
                parikramaTracker.TargetParikramaCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingParikramas));
                OnPropertyChanged(nameof(DisplayCount));
                UpdateProgress();
                Preferences.Set("TargetParikrama", value);
            }
        }

        // Index into PresetTargets for the Picker — -1 means custom value
        public int SelectedPresetIndex
        {
            get => selectedPresetIndex;
            set
            {
                if (value < 0 || value >= PresetTargets.Count) return;
                selectedPresetIndex = value;
                OnPropertyChanged();
                TargetParikrama = PresetTargets[value];
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

        public ICommand StartStopCommand       { get; }
        public ICommand ResetCommand           { get; }
        public ICommand ManualIncrementCommand { get; }
        public ICommand ManualDecrementCommand { get; }
        public ICommand ToggleCountModeCommand { get; }
        public ICommand SetCustomTargetCommand { get; }

        private PropertyChangedEventHandler settingsChangedHandler;

        // ── Constructor ───────────────────────────────────────────────────────────

        public TrackingViewModel(ISensorService sensorService, SensorFusionEngine fusionEngine, SettingsViewModel settings)
        {
            this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));
            this.fusionEngine  = fusionEngine  ?? throw new ArgumentNullException(nameof(fusionEngine));
            this.settings      = settings      ?? throw new ArgumentNullException(nameof(settings));

            this.sensorService.SensorDataReceived += OnSensorDataReceived;
            parikramaTracker.OnThirdSideCompleted += OnThirdSideCompleted;
            parikramaTracker.OnApproachingStart   += OnApproachingStart;

            settingsChangedHandler = (_, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.IsDescendingMode))
                {
                    CountModeLabel = settings.IsDescendingMode ? "Descending" : "Ascending";
                    OnPropertyChanged(nameof(DisplayCount));
                }
            };
            settings.PropertyChanged += settingsChangedHandler;

            StartStopCommand       = new Command(StartStop);
            ResetCommand           = new Command(Reset);
            ManualIncrementCommand = new Command(ManualIncrement);
            ManualDecrementCommand = new Command(ManualDecrement);
            ToggleCountModeCommand = new Command(() => settings.IsDescendingMode = !settings.IsDescendingMode);
            SetCustomTargetCommand = new Command<string>(SetCustomTarget);

            // Restore persisted state via property setters so all notifications fire
            int restoredTarget = Preferences.Get("TargetParikrama", 7);
            int restoredCount  = Preferences.Get("ParikramaCount",  0);

            targetParikrama = restoredTarget;
            parikramaTracker.TargetParikramaCount = restoredTarget;

            // Sync picker selection to restored target.
            // Default to index 0 (value 1) if the saved target isn't in the preset list,
            // to avoid SelectedIndex = -1 which crashes on Android Picker.
            int presetIdx = PresetTargets.IndexOf(restoredTarget);
            selectedPresetIndex = presetIdx >= 0 ? presetIdx : 0;

            ParikramaCount = restoredCount;
            CountModeLabel = settings.IsDescendingMode ? "Descending" : "Ascending";
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

                // Only update parikrama progress display when actively tracking
                if (isTracking)
                {
                    CircleProgress  = parikramaTracker.CurrentProgress;
                    CircleDirection = parikramaTracker.GetDirection();
                    StepsInCircle   = parikramaTracker.CurrentStepsInCircle;
                    SidesInfo       = $"{parikramaTracker.SidesCompleted}/{TotalSides} sides";
                }

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

        private void SetCustomTarget(string text)
        {
            if (int.TryParse(text, out int value) && value > 0)
            {
                TargetParikrama = value;
                // Update picker to show the matching preset, or deselect if custom
                int idx = PresetTargets.IndexOf(value);
                if (idx != selectedPresetIndex)
                {
                    selectedPresetIndex = idx;
                    OnPropertyChanged(nameof(SelectedPresetIndex));
                }
            }
        }

        private void ManualIncrement()
        {
            // Cap at TargetParikrama × 2 as a reasonable upper bound —
            // no hardcoded number, scales with whatever target the devotee chose
            int cap = Math.Max(TargetParikrama * 2, TargetParikrama + 10);
            if (ParikramaCount >= cap) return;
            ParikramaCount++;
            parikramaTracker.ManualSetCount(parikramaCount);
            HandleCompletion();
        }

        private void ManualDecrement()
        {
            if (ParikramaCount <= 0) return;
            ParikramaCount--;
            parikramaTracker.ManualSetCount(parikramaCount); // update tracker first
            TargetReached = parikramaTracker.IsTargetReached; // then read state
        }

        private void HandleCompletion()
        {
            if (parikramaTracker.IsTargetReached && !TargetReached)
            {
                TargetReached = true;
                _ = VibrateForTargetCompletionAsync();
            }
            else if (!TargetReached && parikramaCount > 1)
            {
                // Only vibrate for intermediate completions after the first count,
                // so the first manual tap does not trigger an unexpected vibration
                VibrateForParikramaCompletion();
            }
        }

        // ── Vibration callbacks ───────────────────────────────────────────────────

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
            TargetReached   = false;
            MovementStatus  = "Stationary";
            SidesInfo       = $"0/{TotalSides} sides";
            CircleDirection = "Determining...";
            CircleProgress  = 0;
            StepsInCircle   = 0;
            Steps           = 0;
            ParikramaCount  = 0; // setter fires UpdateProgress + SaveCount
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
            settings.PropertyChanged              -= settingsChangedHandler;
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────────

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
