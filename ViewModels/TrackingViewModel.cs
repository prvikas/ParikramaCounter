using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    // Issue #1: no duplicate count/target fields — ViewModel reads from
    //   IPradhakshinaSessionService (single source of truth).
    // Issue #6: no longer depends on SettingsViewModel — observes IAppPreferences
    //   directly for IsDescendingMode changes via the prefs-changed event pattern.
    // Issue #10: constructor reduced from 5 params to 4 — SettingsViewModel removed.
    public class TrackingViewModel : INotifyPropertyChanged, IDisposable
    {
        private bool disposed;

        private readonly ISensorService              sensorService;
        private readonly ISensorFusionEngine         fusionEngine;
        private readonly IPradhakshinaSessionService session;
        private readonly IAppPreferences             prefs;

        private const int TotalSides = 4;

        public static readonly List<int> PresetTargets = new List<int>
            { 1, 3, 5, 7, 9, 11, 12, 21, 27, 54, 63, 108 };

        // ── Display-only backing fields (NOT count/target — those live in session) ─
        private string heading         = "0°";
        private string direction       = "N";
        private int    steps;
        private double progressPercentage;
        private string startStopText   = "Start";
        private bool   isTracking;
        private bool   targetReached;
        private string movementStatus  = "Stationary";
        private string sidesInfo       = $"0/{TotalSides} sides";
        private double circleProgress;
        private string circleDirection = "Determining...";
        private int    stepsInCircle;
        private string countModeLabel  = "Ascending";
        private int    selectedPresetIndex;

        // ── Properties ────────────────────────────────────────────────────────────

        // Issue #1: count and target are read-through to the session service —
        // no local backing field, no possibility of divergence.
        public int  ParikramaCount   => session.Count;
        public int  TargetParikrama  => session.Target;
        public int  RemainingParikramas => Math.Max(0, session.Target - session.Count);
        public string DisplayCount   => prefs.IsDescendingMode
            ? $"{Math.Max(0, session.Target - session.Count)}"
            : $"{session.Count}";

        public bool IsTracking
        {
            get => isTracking;
            private set { isTracking = value; StartStopText = value ? "Stop" : "Start"; OnPropertyChanged(); }
        }
        public string StartStopText     { get => startStopText;    private set { startStopText = value;    OnPropertyChanged(); } }
        public string Heading           { get => heading;           private set { heading = value;           OnPropertyChanged(); } }
        public string Direction         { get => direction;         private set { direction = value;         OnPropertyChanged(); } }
        public int    Steps             { get => steps;             private set { steps = value;             OnPropertyChanged(); } }
        public double ProgressPercentage{ get => progressPercentage;private set { progressPercentage = value;OnPropertyChanged(); } }
        public bool   TargetReached     { get => targetReached;     private set { targetReached = value;     OnPropertyChanged(); } }
        public string MovementStatus    { get => movementStatus;    private set { movementStatus = value;    OnPropertyChanged(); } }
        public string SidesInfo         { get => sidesInfo;         private set { sidesInfo = value;         OnPropertyChanged(); } }
        public double CircleProgress    { get => circleProgress;    private set { circleProgress = value;    OnPropertyChanged(); } }
        public string CircleDirection   { get => circleDirection;   private set { circleDirection = value;   OnPropertyChanged(); } }
        public int    StepsInCircle     { get => stepsInCircle;     private set { stepsInCircle = value;     OnPropertyChanged(); } }
        public string CountModeLabel    { get => countModeLabel;    private set { countModeLabel = value;    OnPropertyChanged(); } }

        public int SelectedPresetIndex
        {
            get => selectedPresetIndex;
            set
            {
                if (value < 0 || value >= PresetTargets.Count) return;
                selectedPresetIndex = value;
                OnPropertyChanged();
                int newTarget = PresetTargets[value];
                session.SetTarget(newTarget);
                if (newTarget > session.Count) TargetReached = false;
                RefreshTargetDisplay();
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
            ISensorService              sensorService,
            ISensorFusionEngine         fusionEngine,
            IPradhakshinaSessionService session,
            IAppPreferences             prefs)
        {
            this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));
            this.fusionEngine  = fusionEngine  ?? throw new ArgumentNullException(nameof(fusionEngine));
            this.session       = session       ?? throw new ArgumentNullException(nameof(session));
            this.prefs         = prefs         ?? throw new ArgumentNullException(nameof(prefs));

            session.CountChanged      += OnCountChanged;
            session.TargetReached     += OnTargetReachedEvent;
            // ThirdSideCompleted and ApproachingStart are handled inside session service
            // (vibration fires there). No UI action needed in the ViewModel.

            sensorService.SensorDataReceived += OnSensorDataReceived;

            StartStopCommand       = new Command(StartStop);
            ResetCommand           = new Command(async () => await ResetAsync());
            ManualIncrementCommand = new Command(async () => await session.ManualIncrementAsync());
            ManualDecrementCommand = new Command(() => session.ManualDecrement());
            // Issue #6: toggle mode via prefs directly, then notify display
            ToggleCountModeCommand = new Command(ToggleCountMode);
            SetCustomTargetCommand = new Command<string>(SetCustomTarget);

            int idx             = PresetTargets.IndexOf(session.Target);
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
                Heading        = $"{data.Heading:F1}°";
                Direction      = data.Direction;
                Steps          = data.Steps;
                MovementStatus = fusionEngine.IsMoving ? "🚶 Walking" : "🛑 Stationary";

                if (isTracking)
                {
                    CircleProgress  = session.CurrentProgress;
                    CircleDirection = session.GetDirection();
                    StepsInCircle   = session.CurrentStepsInCircle;
                    SidesInfo       = $"{session.SidesCompleted}/{TotalSides} sides";
                    session.ProcessSensorData(data.Heading, data.Steps, fusionEngine.IsMoving, data.Timestamp);
                }
            });
        }

        // ── Session event handlers ────────────────────────────────────────────────

        private void OnCountChanged(int _)
        {
            // CountChanged fires from session.ProcessSensorData which is already called
            // inside BeginInvokeOnMainThread in OnSensorDataReceived — we are on the
            // main thread here. Call OnPropertyChanged directly to avoid a one-frame lag.
            // (ManualIncrementAsync fires CountChanged from the command handler, which
            // MAUI Command infrastructure also dispatches on the main thread.)
            OnPropertyChanged(nameof(ParikramaCount));
            OnPropertyChanged(nameof(RemainingParikramas));
            OnPropertyChanged(nameof(DisplayCount));
            UpdateProgress();
        }

        private void OnTargetReachedEvent()
        {
            MainThread.BeginInvokeOnMainThread(() => TargetReached = true);
        }

        // ── Commands impl ─────────────────────────────────────────────────────────

        private void StartStop()
        {
            if (IsTracking) { IsTracking = false; _ = session.StopTrackingAsync(steps); }
            else            { session.StartTracking(); IsTracking = true; }
        }

        private async System.Threading.Tasks.Task ResetAsync()
        {
            IsTracking      = false;
            TargetReached   = false;
            CircleProgress  = 0;
            StepsInCircle   = 0;
            CircleDirection = "Determining...";
            SidesInfo       = $"0/{TotalSides} sides";
            MovementStatus  = "Stationary";
            fusionEngine.Reset();
            // session.ResetAsync() fires CountChanged(0) which calls OnCountChanged
            // on the main thread — that notifies ParikramaCount, DisplayCount,
            // RemainingParikramas, and UpdateProgress. No need to repeat them here.
            await session.ResetAsync();
        }

        private void SetCustomTarget(string text)
        {
            if (!int.TryParse(text, out int value) || value < 1) return;
            session.SetTarget(value);
            // Clear completion banner if new target is beyond current count
            if (value > session.Count) TargetReached = false;
            RefreshTargetDisplay();
            int idx = PresetTargets.IndexOf(value);
            if (idx != selectedPresetIndex)
            {
                selectedPresetIndex = idx;
                OnPropertyChanged(nameof(SelectedPresetIndex));
            }
        }

        private void ToggleCountMode()
        {
            // Issue #6: write directly to prefs — no SettingsViewModel needed
            prefs.IsDescendingMode = !prefs.IsDescendingMode;
            CountModeLabel = prefs.IsDescendingMode ? "Descending" : "Ascending";
            OnPropertyChanged(nameof(DisplayCount));
        }

        // Called from TrackingPage.OnAppearing so that a mode change made on the
        // Settings page is reflected when the user navigates back to Tracking.
        public void RefreshModeDisplay()
        {
            CountModeLabel = prefs.IsDescendingMode ? "Descending" : "Ascending";
            OnPropertyChanged(nameof(DisplayCount));
        }

        private void RefreshTargetDisplay()
        {
            OnPropertyChanged(nameof(TargetParikrama));
            OnPropertyChanged(nameof(RemainingParikramas));
            OnPropertyChanged(nameof(DisplayCount));
            UpdateProgress();
        }

        private void UpdateProgress()
        {
            ProgressPercentage = session.Target > 0
                ? (double)session.Count / session.Target
                : 0;
        }

        // ── IDisposable ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            sensorService.SensorDataReceived -= OnSensorDataReceived;
            session.CountChanged             -= OnCountChanged;
            session.TargetReached            -= OnTargetReachedEvent;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

// Note: this 