using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    // Fix #3: uses IUserPreferences and ISensorConfiguration — not the fat IAppPreferences.
    // Fix #10: ILogger injected for structured logging.
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly IUserPreferences    userPrefs;
        private readonly ISensorConfiguration sensorConfig;
        private readonly ISensorFusionEngine  engine;
        private readonly ILogger<SettingsViewModel> logger;
        private bool isCalibrating;
        private double calibrationProgress;

        // ── int authoritative properties ──────────────────────────────────────────
        public int StepThreshold
        {
            get => sensorConfig.StepThreshold;
            set { sensorConfig.StepThreshold = value; OnPropertyChanged(); OnPropertyChanged(nameof(StepThresholdDouble)); engine.UpdateStepThreshold(value); }
        }
        public int MinStepInterval
        {
            get => sensorConfig.MinStepInterval;
            set { sensorConfig.MinStepInterval = value; OnPropertyChanged(); OnPropertyChanged(nameof(MinStepIntervalDouble)); engine.UpdateMinStepInterval(value); }
        }
        public int ThirdSideVibrationMs     { get => userPrefs.ThirdSideVibrationMs;      set { userPrefs.ThirdSideVibrationMs = value;      OnPropertyChanged(); OnPropertyChanged(nameof(ThirdSideVibrationMsDouble)); } }
        public int ApproachingStartVibrationMs { get => userPrefs.ApproachingStartVibrationMs; set { userPrefs.ApproachingStartVibrationMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(ApproachingStartVibrationMsDouble)); } }
        public int CompletionVibrationMs    { get => userPrefs.CompletionVibrationMs;     set { userPrefs.CompletionVibrationMs = value;     OnPropertyChanged(); OnPropertyChanged(nameof(CompletionVibrationMsDouble)); } }
        public int TargetVibrationMs        { get => userPrefs.TargetVibrationMs;         set { userPrefs.TargetVibrationMs = value;         OnPropertyChanged(); OnPropertyChanged(nameof(TargetVibrationMsDouble)); } }
        public int TargetVibrationCount     { get => userPrefs.TargetVibrationCount;      set { userPrefs.TargetVibrationCount = Math.Max(1, Math.Min(10, value)); OnPropertyChanged(); OnPropertyChanged(nameof(TargetVibrationCountDouble)); } }

        // ── double Slider pass-throughs ───────────────────────────────────────────
        public double StepThresholdDouble               { get => sensorConfig.StepThreshold;             set { StepThreshold = (int)Math.Round(value); } }
        public double MinStepIntervalDouble             { get => sensorConfig.MinStepInterval;            set { MinStepInterval = (int)Math.Round(value); } }
        public double ThirdSideVibrationMsDouble        { get => userPrefs.ThirdSideVibrationMs;          set { ThirdSideVibrationMs = (int)Math.Round(value); } }
        public double ApproachingStartVibrationMsDouble { get => userPrefs.ApproachingStartVibrationMs;   set { ApproachingStartVibrationMs = (int)Math.Round(value); } }
        public double CompletionVibrationMsDouble       { get => userPrefs.CompletionVibrationMs;         set { CompletionVibrationMs = (int)Math.Round(value); } }
        public double TargetVibrationMsDouble           { get => userPrefs.TargetVibrationMs;             set { TargetVibrationMs = (int)Math.Round(value); } }
        public double TargetVibrationCountDouble        { get => userPrefs.TargetVibrationCount;          set { TargetVibrationCount = (int)Math.Round(value); } }

        // ── bool properties ───────────────────────────────────────────────────────
        public bool EnableVibrations    { get => userPrefs.EnableVibrations;    set { userPrefs.EnableVibrations = value;    OnPropertyChanged(); } }
        public bool IsDescendingMode    { get => userPrefs.IsDescendingMode;    set { userPrefs.IsDescendingMode = value;    OnPropertyChanged(); } }
        public bool AutoCountingEnabled { get => userPrefs.AutoCountingEnabled; set { userPrefs.AutoCountingEnabled = value; OnPropertyChanged(); } }

        public bool   IsCalibrating       { get => isCalibrating;       set { isCalibrating = value;       OnPropertyChanged(); } }
        public double CalibrationProgress { get => calibrationProgress; set { calibrationProgress = value; OnPropertyChanged(); } }

        public ICommand StartCalibrationCommand { get; }
        public ICommand ResetSettingsCommand    { get; }

        public SettingsViewModel(
            IUserPreferences      userPrefs,
            ISensorConfiguration  sensorConfig,
            ISensorFusionEngine   engine,
            ILogger<SettingsViewModel> logger)
        {
            this.userPrefs    = userPrefs    ?? throw new ArgumentNullException(nameof(userPrefs));
            this.sensorConfig = sensorConfig ?? throw new ArgumentNullException(nameof(sensorConfig));
            this.engine       = engine       ?? throw new ArgumentNullException(nameof(engine));
            this.logger       = logger       ?? throw new ArgumentNullException(nameof(logger));

            StartCalibrationCommand = new Command(StartCalibration);
            ResetSettingsCommand    = new Command(ResetSettings);
            engine.UpdateStepThreshold(sensorConfig.StepThreshold);
            engine.UpdateMinStepInterval(sensorConfig.MinStepInterval);
        }

        private async void StartCalibration()
        {
            if (IsCalibrating) return;
            IsCalibrating       = true;
            CalibrationProgress = 0.0;
            logger.LogInformation("Compass calibration started");
            engine.ResetForCalibration();
            for (int i = 1; i <= 20; i++)
            {
                await System.Threading.Tasks.Task.Delay(500);
                CalibrationProgress = i / 20.0;
            }
            IsCalibrating = false;
            logger.LogInformation("Compass calibration complete");
            if (Application.Current?.MainPage != null)
                await Application.Current.MainPage.DisplayAlert(
                    "Calibration Complete",
                    "Compass calibration complete. Hold the device flat and walk a few steps for best results.",
                    "OK");
        }

        private void ResetSettings()
        {
            StepThreshold               = 120;
            MinStepInterval             = 250;
            ThirdSideVibrationMs        = 400;
            ApproachingStartVibrationMs = 200;
            CompletionVibrationMs       = 500;
            TargetVibrationMs           = 300;
            TargetVibrationCount        = 3;
            EnableVibrations            = true;
            IsDescendingMode            = false;
            AutoCountingEnabled         = true;
            logger.LogInformation("Settings reset to defaults");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
