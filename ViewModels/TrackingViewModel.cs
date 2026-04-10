using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using ParikramaCounter.Models;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    // Fix #5: TrackingViewModel is now a pure observer.
    // It no longer calls fusionEngine.ProcessSensorData or drives the sensor loop.
    // The SensorPipeline owns that loop; TrackingViewModel subscribes to its output.
    public class TrackingViewModel : INotifyPropertyChanged, IDisposable
    {
        private bool disposed;

        private readonly ISensorPipeline             pipeline;
        private readonly IPradhakshinaSessionService session;
        private readonly IUserPreferences            userPrefs;
        private readonly ISensorFusionEngine         fusionEngine;

        private const int TotalSides = 4;

        public static readonly List<int> PresetTargets = new List<int>
            { 1, 3, 5, 7, 9, 11, 12, 21, 27, 54, 63, 108 };

        // ── Display-only backing fields ───────────────────────────────────────────
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

        // Count and target are read-through to session — single source of truth
        public int    ParikramaCount     => session.Count;
        public int    TargetParikrama    => session.Target;
        public int    RemainingParikramas => Math.Max(0, session.Target - session.Count);
        public string DisplayCount       => userPrefs.IsDescendingMode
            ? $"{Math.Max(0, session.Target - session.Count)}"
            : $"{session.Count}";

        public bool   IsTracking        { get => isTracking;         private set { isTracking = value;         StartStopText = value ? "Stop" : "Start"; OnPropertyChanged(); } }
        public string StartStopText     { get => startStopText;      private set { startStopText = value;      OnPropertyChanged(); } }
        public string Heading           { get => heading;             private set { heading = value;             OnPropertyChanged(); } }
        public string Direction         { get => direction;           private set { direction = value;           OnPropertyChanged(); } }
        public int    Steps             { get => steps;               private set { steps = value;               OnPropertyChanged(); } }
        public double ProgressPercentage{ get => progressPercentage;  private set { progressPercentage = value;  OnPropertyChanged(); } }
        public bool   TargetReached     { get => targetReached;       private set { targetReached = value;       OnPropertyChanged(); } }
        public string MovementStatus    { get => movementStatus;      private set { movementStatus = value;      OnPropertyChanged(); } }
        public string SidesInfo         { get => sidesInfo;           private set { sidesInfo = value;           OnPropertyChanged(); } }
        public double CircleProgress    { get => circleProgress;      private set { circleProgress = value;      OnPropertyChanged(); } }
        public string CircleDirection   { get => circleDirection;     private set { circleDirection = value;     OnPropertyChanged(); } }
        public int    StepsInCircle     { get => stepsInCircle;       private set { stepsInCircle = value;       OnPropertyChanged(); } }
        public string CountModeLabel    { get => countModeLabel;      private set { countModeLabel = value;      OnPropertyChanged(); } }

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

        public TrackingViewModel(
            ISensorPipeline              pipeline,
            IPradhakshinaSessionService  session,
            IUserPreferences             userPrefs,
            ISensorFusionEngine          fusionEngine)
        {
            this.pipeline     = pipeline     ?? throw new ArgumentNullException(nameof(pipeline));
            this.session      = session      ?? throw new ArgumentNullException(nameof(session));
            this.userPrefs    = userPrefs    ?? throw new ArgumentNullException(nameof(userPrefs));
            this.fusionEngine = fusionEngine ?? throw new ArgumentNullException(nameof(fusionEngine));

            // Subscribe to processed sensor data — not raw arrays
            pipeline.SensorProcessed     += OnSensorProcessed;
            session.CountChanged         += OnCountChanged;
            session.TargetReached        += OnTargetReachedEvent;

            StartStopCommand       = new Command(StartStop);
            ResetCommand           = new Command(async () => await ResetAsync());
            ManualIncrementCommand = new Command(async () => await session.ManualIncrementAsync());
            ManualDecrementCommand = new Command(() => session.ManualDecrement());
            ToggleCountModeCommand = new Command(ToggleCountMode);
            SetCustomTargetCommand = new Command<string>(SetCustomTarget);

            int idx             = PresetTargets.IndexOf(session.Target);
            selectedPresetIndex = idx >= 0 ? idx : 0;
            CountModeLabel      = userPrefs.IsDescendingMode ? "Descending" : "Ascending";
            UpdateProgress();
        }

        // ── Pipeline observer ─────────────────────────────────────────────────────

        private void OnSensorProcessed(SensorData data)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
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
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrackingViewModel] Display update error: {ex.Message}");
                }
            });
        }

        private void OnCountChanged(int _)
        {
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
            if (IsTracking)
            {
                IsTracking = false;
                pipeline.Stop();
                _ = session.StopTrackingAsync(steps);
            }
            else
            {
                session.StartTracking();
                pipeline.Start();
                IsTracking = true;
            }
        }

        private async System.Threading.Tasks.Task ResetAsync()
        {
            if (IsTracking) { pipeline.Stop(); IsTracking = false; }
            TargetReached   = false;
            CircleProgress  = 0;
            StepsInCircle   = 0;
            CircleDirection = "Determining...";
            SidesInfo       = $"0/{TotalSides} sides";
            MovementStatus  = "Stationary";
            fusionEngine.Reset();
            await session.ResetAsync();
        }

        private void SetCustomTarget(string text)
        {
            if (!int.TryParse(text, out int value) || value < 1) return;
            session.SetTarget(value);
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
            userPrefs.IsDescendingMode = !userPrefs.IsDescendingMode;
            CountModeLabel = userPrefs.IsDescendingMode ? "Descending" : "Ascending";
            OnPropertyChanged(nameof(DisplayCount));
        }

        public void RefreshModeDisplay()
        {
            CountModeLabel = userPrefs.IsDescendingMode ? "Descending" : "Ascending";
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

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            pipeline.SensorProcessed -= OnSensorProcessed;
            session.CountChanged     -= OnCountChanged;
            session.TargetReached    -= OnTargetReachedEvent;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
