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

        // ── Step detection ────────────────────────────────────────────────────────
        private int stepThreshold   = 120;
        private int minStepInterval = 250;

        // ── Vibration durations (ms) ──────────────────────────────────────────────
        private int thirdSideVibrationMs      = 400;
        private int approachingStartVibrationMs = 200;
        private int completionVibrationMs     = 500;
        private int targetVibrationMs         = 300;
        private int targetVibrationCount      = 3;
        private bool enableVibrations         = true;

        // ── Counting mode ─────────────────────────────────────────────────────────
        private bool isDescendingMode = false;  // false = ascending (default)

        // ── Auto sensor counting ──────────────────────────────────────────────────
        private bool autoCountingEnabled = true;

        // ── Calibration ───────────────────────────────────────────────────────────
        private bool isCalibrating;
        private double calibrationProgress;

        // ── Properties ────────────────────────────────────────────────────────────

        public int StepThreshold
        {
            get => stepThreshold;
            set { stepThreshold = value; OnPropertyChanged(); fusionEngine?.UpdateStepThreshold(value); Save(); }
        }

        public int MinStepInterval
        {
            get => minStepInterval;
            set { minStepInterval = value; OnPropertyChanged(); fusionEngine?.UpdateMinStepInterval(value); Save(); }
        }

        public int ThirdSideVibrationMs
        {
            get => thirdSideVibrationMs;
            set { thirdSideVibrationMs = value; OnPropertyChanged(); Save(); }
        }

        public int ApproachingStartVibrationMs
        {
            get => approachingStartVibrationMs;
            set { approachingStartVibrationMs = value; OnPropertyChanged(); Save(); }
        }

        public int CompletionVibrationMs
        {
            get => completionVibrationMs;
            set { completionVibrationMs = value; OnPropertyChanged(); Save(); }
        }

        public int TargetVibrationMs
        {
            get => targetVibrationMs;
            set { targetVibrationMs = value; OnPropertyChanged(); Save(); }
        }

        public int TargetVibrationCount
        {
            get => targetVibrationCount;
            set { targetVibrationCount = Math.Max(1, Math.Min(10, value)); OnPropertyChanged(); OnPropertyChanged(nameof(TargetVibrationCountDouble)); Save(); }
        }

        // Slider requires double — use this for two-way binding to avoid type coercion warnings
        public double TargetVibrationCountDouble
        {
            get => targetVibrationCount;
            set { TargetVibrationCount = (int)Math.Round(value); }
        }

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

        // ── Persistence ───────────────────────────────────────────────────────────

        private void Save()
        {
            Preferences.Set("StepThreshold",           stepThreshold);
            Preferences.Set("MinStepInterval",         minStepInterval);
            Preferences.Set("ThirdSideVibMs",          thirdSideVibrationMs);
            Preferences.Set("ApproachingVibMs",        approachingStartVibrationMs);
            Preferences.Set("CompletionVibMs",         completionVibrationMs);
            Preferences.Set("TargetVibMs",             targetVibrationMs);
            Preferences.Set("TargetVibCount",          targetVibrationCount);
            Preferences.Set("EnableVibrations",        enableVibrations);
            Preferences.Set("IsDescendingMode",        isDescendingMode);
            Preferences.Set("AutoCountingEnabled",     autoCountingEnabled);
        }

        private void Load()
        {
            stepThreshold             = Preferences.Get("StepThreshold",       120);
            minStepInterval           = Preferences.Get("MinStepInterval",     250);
            thirdSideVibrationMs      = Preferences.Get("ThirdSideVibMs",      400);
            approachingStartVibrationMs = Preferences.Get("ApproachingVibMs",  200);
            completionVibrationMs     = Preferences.Get("CompletionVibMs",     500);
            targetVibrationMs         = Preferences.Get("TargetVibMs",         300);
            targetVibrationCount      = Preferences.Get("TargetVibCount",        3);
            enableVibrations          = Preferences.Get("EnableVibrations",    true);
            isDescendingMode          = Preferences.Get("IsDescendingMode",   false);
            autoCountingEnabled       = Preferences.Get("AutoCountingEnabled", true);

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
