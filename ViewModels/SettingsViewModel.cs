using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly SensorFusionEngine fusionEngine;

        // All int backing fields — double pass-through properties used for Slider binding
        private int stepThreshold             = 120;
        private int minStepInterval           = 250;
        private int thirdSideVibrationMs      = 400;
        private int approachingStartVibMs     = 200;
        private int completionVibrationMs     = 500;
        private int targetVibrationMs         = 300;
        private int targetVibrationCount      = 3;
        private bool enableVibrations         = true;
        private bool isDescendingMode         = false;
        private bool autoCountingEnabled      = true;
        private bool isCalibrating;
        private double calibrationProgress;

        // ── int properties (authoritative) ───────────────────────────────────────

        public int StepThreshold
        {
            get => stepThreshold;
            set { stepThreshold = value; OnPropertyChanged(); OnPropertyChanged(nameof(StepThresholdDouble)); fusionEngine?.UpdateStepThreshold(value); Save(); }
        }

        public int MinStepInterval
        {
            get => minStepInterval;
            set { minStepInterval = value; OnPropertyChanged(); OnPropertyChanged(nameof(MinStepIntervalDouble)); fusionEngine?.UpdateMinStepInterval(value); Save(); }
        }

        public int ThirdSideVibrationMs
        {
            get => thirdSideVibrationMs;
            set { thirdSideVibrationMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(ThirdSideVibrationMsDouble)); Save(); }
        }

        public int ApproachingStartVibrationMs
        {
            get => approachingStartVibMs;
            set { approachingStartVibMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(ApproachingStartVibrationMsDouble)); Save(); }
        }

        public int CompletionVibrationMs
        {
            get => completionVibrationMs;
            set { completionVibrationMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(CompletionVibrationMsDouble)); Save(); }
        }

        public int TargetVibrationMs
        {
            get => targetVibrationMs;
            set { targetVibrationMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(TargetVibrationMsDouble)); Save(); }
        }

        public int TargetVibrationCount
        {
            get => targetVibrationCount;
            set { targetVibrationCount = Math.Max(1, Math.Min(10, value)); OnPropertyChanged(); OnPropertyChanged(nameof(TargetVibrationCountDouble)); Save(); }
        }

        // ── double pass-throughs for Slider two-way binding ───────────────────────
        // Slider.Value is always double. Binding int directly causes InvalidCastException
        // on iOS and binding warnings on Android when the slider fires value-changed.

        public double StepThresholdDouble
        {
            get => stepThreshold;
            set { StepThreshold = (int)Math.Round(value); }
        }

        public double MinStepIntervalDouble
        {
            get => minStepInterval;
            set { MinStepInterval = (int)Math.Round(value); }
        }

        public double ThirdSideVibrationMsDouble
        {
            get => thirdSideVibrationMs;
            set { ThirdSideVibrationMs = (int)Math.Round(value); }
        }

        public double ApproachingStartVibrationMsDouble
        {
            get => approachingStartVibMs;
            set { ApproachingStartVibrationMs = (int)Math.Round(value); }
        }

        public double CompletionVibrationMsDouble
        {
            get => completionVibrationMs;
            set { CompletionVibrationMs = (int)Math.Round(value); }
        }

        public double TargetVibrationMsDouble
        {
            get => targetVibrationMs;
            set { TargetVibrationMs = (int)Math.Round(value); }
        }

        public double TargetVibrationCountDouble
        {
            get => targetVibrationCount;
            set { TargetVibrationCount = (int)Math.Round(value); }
        }

        // ── bool / calibration properties ────────────────────────────────────────

        public bool EnableVibrations
        {
            get => enableVibrations;
            set { enableVibrations = value; OnPropertyChanged(); Save(); }
        }

        public bool IsDescendingMode
        {
            get => isDescendingMode;
            set { isDescendingMode = value; OnPropertyChanged(); Save(); }
        }

        public bool AutoCountingEnabled
        {
            get => autoCountingEnabled;
            set { autoCountingEnabled = value; OnPropertyChanged(); Save(); }
        }

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

        public SettingsViewModel(SensorFusionEngine fusionEngine)
        {
            this.fusionEngine       = fusionEngine;
            StartCalibrationCommand = new Command(StartCalibration);
            ResetSettingsCommand    = new Command(ResetSettings);
            Load();
        }

        private void Save()
        {
            Preferences.Set("StepThreshold",       stepThreshold);
            Preferences.Set("MinStepInterval",     minStepInterval);
            Preferences.Set("ThirdSideVibMs",      thirdSideVibrationMs);
            Preferences.Set("ApproachingVibMs",    approachingStartVibMs);
            Preferences.Set("CompletionVibMs",     completionVibrationMs);
            Preferences.Set("TargetVibMs",         targetVibrationMs);
            Preferences.Set("TargetVibCount",      targetVibrationCount);
            Preferences.Set("EnableVibrations",    enableVibrations);
            Preferences.Set("IsDescendingMode",    isDescendingMode);
            Preferences.Set("AutoCountingEnabled", autoCountingEnabled);
        }

        private void Load()
        {
            stepThreshold         = Preferences.Get("StepThreshold",       120);
            minStepInterval       = Preferences.Get("MinStepInterval",     250);
            thirdSideVibrationMs  = Preferences.Get("ThirdSideVibMs",      400);
            approachingStartVibMs = Preferences.Get("ApproachingVibMs",    200);
            completionVibrationMs = Preferences.Get("CompletionVibMs",     500);
            targetVibrationMs     = Preferences.Get("TargetVibMs",         300);
            targetVibrationCount  = Preferences.Get("TargetVibCount",        3);
            enableVibrations      = Preferences.Get("EnableVibrations",    true);
            isDescendingMode      = Preferences.Get("IsDescendingMode",   false);
            autoCountingEnabled   = Preferences.Get("AutoCountingEnabled", true);

            fusionEngine?.UpdateStepThreshold(stepThreshold);
            fusionEngine?.UpdateMinStepInterval(minStepInterval);
        }

        private async void StartCalibration()
        {
            if (IsCalibrating) return;
            IsCalibrating       = true;
            CalibrationProgress = 0.0;
            fusionEngine?.ResetForCalibration();
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
