using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    // Fix #3/#6: SettingsViewModel reads/writes via IAppPreferences.
    // It no longer calls engine methods directly — it updates prefs and the
    // engine picks up changes via the SensorFusionEngine DI binding.
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly IAppPreferences    prefs;
        private readonly ISensorFusionEngine engine;
        private bool isCalibrating;
        private double calibrationProgress;

        // ── int properties (authoritative) ───────────────────────────────────────

        public int StepThreshold
        {
            get => prefs.StepThreshold;
            set { prefs.StepThreshold = value; OnPropertyChanged(); OnPropertyChanged(nameof(StepThresholdDouble)); engine?.UpdateStepThreshold(value); }
        }
        public int MinStepInterval
        {
            get => prefs.MinStepInterval;
            set { prefs.MinStepInterval = value; OnPropertyChanged(); OnPropertyChanged(nameof(MinStepIntervalDouble)); engine?.UpdateMinStepInterval(value); }
        }
        public int ThirdSideVibrationMs
        {
            get => prefs.ThirdSideVibrationMs;
            set { prefs.ThirdSideVibrationMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(ThirdSideVibrationMsDouble)); }
        }
        public int ApproachingStartVibrationMs
        {
            get => prefs.ApproachingStartVibrationMs;
            set { prefs.ApproachingStartVibrationMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(ApproachingStartVibrationMsDouble)); }
        }
        public int CompletionVibrationMs
        {
            get => prefs.CompletionVibrationMs;
            set { prefs.CompletionVibrationMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(CompletionVibrationMsDouble)); }
        }
        public int TargetVibrationMs
        {
            get => prefs.TargetVibrationMs;
            set { prefs.TargetVibrationMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(TargetVibrationMsDouble)); }
        }
        public int TargetVibrationCount
        {
            get => prefs.TargetVibrationCount;
            set { prefs.TargetVibrationCount = Math.Max(1, Math.Min(10, value)); OnPropertyChanged(); OnPropertyChanged(nameof(TargetVibrationCountDouble)); }
        }

        // ── double pass-throughs for Slider two-way binding ───────────────────────
        public double StepThresholdDouble              { get => prefs.StepThreshold;            set { StepThreshold = (int)Math.Round(value); } }
        public double MinStepIntervalDouble            { get => prefs.MinStepInterval;           set { MinStepInterval = (int)Math.Round(value); } }
        public double ThirdSideVibrationMsDouble       { get => prefs.ThirdSideVibrationMs;      set { ThirdSideVibrationMs = (int)Math.Round(value); } }
        public double ApproachingStartVibrationMsDouble{ get => prefs.ApproachingStartVibrationMs; set { ApproachingStartVibrationMs = (int)Math.Round(value); } }
        public double CompletionVibrationMsDouble      { get => prefs.CompletionVibrationMs;     set { CompletionVibrationMs = (int)Math.Round(value); } }
        public double TargetVibrationMsDouble          { get => prefs.TargetVibrationMs;         set { TargetVibrationMs = (int)Math.Round(value); } }
        public double TargetVibrationCountDouble       { get => prefs.TargetVibrationCount;      set { TargetVibrationCount = (int)Math.Round(value); } }

        // ── bool properties ───────────────────────────────────────────────────────
        public bool EnableVibrations    { get => prefs.EnableVibrations;    set { prefs.EnableVibrations = value;    OnPropertyChanged(); } }
        public bool IsDescendingMode    { get => prefs.IsDescendingMode;    set { prefs.IsDescendingMode = value;    OnPropertyChanged(); } }
        public bool AutoCountingEnabled { get => prefs.AutoCountingEnabled; set { prefs.AutoCountingEnabled = value; OnPropertyChanged(); } }

        // ── Calibration ───────────────────────────────────────────────────────────
        public bool IsCalibrating
        {
            get => isCalibrating;
            set { isCalibrating = value; OnPropertyChanged(); }
        }
        public double CalibrationProgress
        {
            get => calibrationProgress;
            set { calibrationProgress = value; OnPropertyChanged(); }
        }

        public ICommand StartCalibrationCommand { get; }
        public ICommand ResetSettingsCommand    { get; }

        public SettingsViewModel(IAppPreferences prefs, ISensorFusionEngine engine)
        {
            this.prefs  = prefs  ?? throw new ArgumentNullException(nameof(prefs));
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
            StartCalibrationCommand = new Command(StartCalibration);
            ResetSettingsCommand    = new Command(ResetSettings);
            // Apply persisted tuning to engine on startup
            engine.UpdateStepThreshold(prefs.StepThreshold);
            engine.UpdateMinStepInterval(prefs.MinStepInterval);
        }

        private async void StartCalibration()
        {
            if (IsCalibrating) return;
            IsCalibrating       = true;
            CalibrationProgress = 0.0;
            engine?.ResetForCalibration();
            for (int i = 1; i <= 20; i++)
            {
                await System.Threading.Tasks.Task.Delay(500);
                CalibrationProgress = i / 20.0;
            }
            IsCalibrating = false;
            if (Application.Current?.MainPage != null)
                await Application.Current.MainPage.DisplayAlert(
                    "Calibration Complete",
                    "Compass calibration complete. Hold the device flat and walk a few steps for best results.",
                    "OK");
        }

        private void ResetSettings()
        {
            StepThreshold             = 120;
            MinStepInterval           = 250;
            ThirdSideVibrationMs      = 400;
            ApproachingStartVibrationMs = 200;
            CompletionVibrationMs     = 500;
            TargetVibrationMs         = 300;
            TargetVibrationCount      = 3;
            EnableVibrations          = true;
            IsDescendingMode          = false;
            AutoCountingEnabled       = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
