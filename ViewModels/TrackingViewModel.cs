using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    // Fix #5: TrackingViewModel is now a thin UI adapter.
    // Session logic lives in PradhakshinaSessionService.
    // Sensor lifecycle owned by SensorLifecycleService (started in App).
    // Vibration owned by VibrationService.
    // Preferences owned by IAppPreferences.
    public class TrackingViewModel : INotifyPropertyChanged, IDisposable
    {
        private bool disposed;

        private readonly ISensorService             sensorService;
        private readonly ISensorFusionEngine        fusionEngine;
        private readonly PradhakshinaSessionService session;
        private readonly IAppPreferences            prefs;
        private readonly SettingsViewModel          settings;

        private const int TotalSides = 4;

        public static readonly List<int> PresetTargets = new List<int>
            { 1, 3, 5, 7, 9, 11, 12, 21, 27, 54, 63, 108 };

        // ── Backing fields ────────────────────────────────────────────────────────
        private bool   isTracking;
        private string heading         = "0°";
        private string direction       = "N";
        private int    steps;
        private int    parikramaCount;
        private int    targetParikrama;
        private double progressPercentage;
        private string startStopText   = "Start";
        private bool   targetReached;
        private string movementStatus  = "Stationary";
        private string sidesInfo;
        private double circleProgress;
        private string circleDirection = "Determining...";
        private int    stepsInCircle;
        private string countModeLabel  = "Ascending";
        private int    selectedPresetIndex;

        private PropertyChangedEventHandler settingsChangedHandler;

        // ── Properties ────────────────────────────────────────────────────────────

        public bool IsTracking
        {
            get => isTracking;
            private set { isTracking = value; StartStopText = value ? "Stop" : "Start"; OnPropertyChanged(); }
        }
        public string StartStopText        { get => startStopText;    private set { startStopText = value;    OnPropertyChanged(); } }
        public string Heading              { get => heading;           private set { heading = value;           OnPropertyChanged(); } }
        public string Direction            { get => direction;         private set { direction = value;         OnPropertyChanged(); } }
        public int    Steps                { get => steps;             private set { steps = value;             OnPropertyChanged(); } }
        public double ProgressPercentage   { get => progressPercentage;private set { progressPercentage = value;OnPropertyChanged(); } }
        public bool   TargetReached        { get => targetReached;     private set { targetReached = value;     OnPropertyChanged(); } }
        public string MovementStatus       { get => movementStatus;    private set { movementStatus = value;    OnPropertyChanged(); } }
        public string SidesInfo            { get => sidesInfo;         private set { sidesInfo = value;         OnPropertyChanged(); } }
        public double CircleProgress       { get => circleProgress;    private set { circleProgress = value;    OnPropertyChanged(); } }
        public string CircleDirection      { get => circleDirection;   private set { circleDirection = value;   OnPropertyChanged(); } }
        public int    StepsInCircle        { get => stepsInCircle;     private set { stepsInCircle = value;     OnPropertyChanged(); } }
        public string CountModeLabel       { get => countModeLabel;    private set { countModeLabel = value;    OnPropertyChanged(); } }

        public int ParikramaCount
        {
            get => parikramaCount;
            private set
            {
                parikramaCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingParikramas));
                OnPropertyChanged(nameof(DisplayCount));
                UpdateProgress();
            }
        }

        public int TargetParikrama
        {
            get => targetParikrama;
            private set
            {
                targetParikrama = value;
                session.SetTarget(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingParikramas));
                OnPropertyChanged(nameof(DisplayCount));
                UpdateProgress();
            }
        }

        public string DisplayCount => prefs.IsDescendingMode
            ? $"{Math.Max(0, targetParikrama - parikramaCount)}"
            : $"{parikramaCount}";

        public int RemainingParikramas => Math.Max(0, targetParikrama - parikramaCount);

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

        // ── Commands ──────────────────────────────────────────────────────────────

        public ICommand StartStopCommand       { get; }
        public ICommand ResetCommand           { get; }
        public ICommand ManualIncrementCommand { get; }
        public ICommand ManualDecrementCommand { get; }
        public ICommand ToggleCountModeCommand { get; }
        public ICommand SetCustomTargetCommand { get; }

        // ── Constructor ───────────────────────────────────────────────────────────

        public TrackingViewModel(
            ISensorService             sensorService,
            ISensorFusionEngine        fusionEngine,
            PradhakshinaSessionService session,
            IAppPreferences            prefs,
            SettingsViewModel          settings)
        {
            this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));
            this.fusionEngine  = fusionEngine  ?? throw new ArgumentNullException(nameof(fusionEngine));
            this.session       = session       ?? throw new ArgumentNullException(nameof(session));
            this.prefs         = prefs         ?? throw new ArgumentNullException(nameof(prefs));
            this.settings      = settings      ?? throw new ArgumentNullException(nameof(settings));

            // Subscribe to session events
            session.CountChanged      += OnCountChanged;
            session.TargetReached     += OnTargetReached;

            // Subscribe to raw sensor for display (heading, steps, movement)
            sensorService.SensorDataReceived += OnSensorDataReceived;

            // Track settings changes that affect display
            settingsChangedHandler = (_, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.IsDescendingMode))
                {
                    CountModeLabel = prefs.IsDescendingMode ? "Descending" : "Ascending";
                    OnPropertyChanged(nameof(DisplayCount));
                }
            };
            settings.PropertyChanged += settingsChangedHandler;

            StartStopCommand       = new Command(StartStop);
            ResetCommand           = new Command(async () => await ResetAsync());
            ManualIncrementCommand = new Command(async () => await session.ManualIncrementAsync(steps));
            ManualDecrementCommand = new Command(() => session.ManualDecrement());
            ToggleCountModeCommand = new Command(() => settings.IsDescendingMode = !settings.IsDescendingMode);
            SetCustomTargetCommand = new Command<string>(SetCustomTarget);

            // Restore state
            targetParikrama     = prefs.TargetParikrama;
            parikramaCount      = prefs.ParikramaCount;
            sidesInfo           = $"0/{TotalSides} sides";
            int idx             = PresetTargets.IndexOf(targetParikrama);
            selectedPresetIndex = idx >= 0 ? idx : 0;
            CountModeLabel      = prefs.IsDescendingMode ? "Descending" : "Ascending";
            UpdateProgress();
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

                if (isTracking)
                {
                    CircleProgress  = session.CurrentProgress;
                    CircleDirection = session.GetDirection();
                    StepsInCircle   = session.CurrentStepsInCircle;
                    SidesInfo       = $"{session.SidesCompleted}/{TotalSides} sides";

                    // Delegate auto-counting to session service
                    session.ProcessSensorData(data.Heading, data.Steps, fusionEngine.IsMoving, data.Timestamp);
                }
            });
        }

        // ── Session event handlers ────────────────────────────────────────────────

        private void OnCountChanged(int newCount)
        {
            MainThread.BeginInvokeOnMainThread(() => ParikramaCount = newCount);
        }

        private void OnTargetReached()
        {
            MainThread.BeginInvokeOnMainThread(() => TargetReached = true);
        }

        // ── Commands impl ─────────────────────────────────────────────────────────

        private void StartStop()
        {
            if (IsTracking)
            {
                IsTracking = false;
                _ = session.StopTrackingAsync(steps);
            }
            else
            {
                session.StartTracking();
                IsTracking = true;
            }
        }

        private async System.Threading.Tasks.Task ResetAsync()
        {
            IsTracking  = false;
            TargetReached = false;
            CircleProgress  = 0;
            StepsInCircle   = 0;
            CircleDirection = "Determining...";
            SidesInfo       = $"0/{TotalSides} sides";
            MovementStatus  = "Stationary";
            fusionEngine.Reset();
            await session.ResetAsync();
            ParikramaCount = 0;
        }

        private void SetCustomTarget(string text)
        {
            if (!int.TryParse(text, out int value) || value < 1) return;
            TargetParikrama = value;
            int idx = PresetTargets.IndexOf(value);
            if (idx != selectedPresetIndex)
            {
                selectedPresetIndex = idx;
                OnPropertyChanged(nameof(SelectedPresetIndex));
            }
        }

        private void UpdateProgress()
        {
            ProgressPercentage = targetParikrama > 0
                ? (double)parikramaCount / targetParikrama
                : 0;
        }

        // ── IDisposable ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            sensorService.SensorDataReceived -= OnSensorDataReceived;
            session.CountChanged             -= OnCountChanged;
            session.TargetReached            -= OnTargetReached;
            settings.PropertyChanged         -= settingsChangedHandler;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
