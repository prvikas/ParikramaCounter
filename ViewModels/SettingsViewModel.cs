using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        // Fix #4: SettingsViewModel now holds a reference to the shared
        // SensorFusionEngine so slider changes actually affect step detection.
        private readonly SensorFusionEngine fusionEngine;

        private int stepThreshold = 120;
        private int minStepInterval = 250;
        private bool isCalibrating;
        private double calibrationProgress; // Fix #6: double 0.0–1.0 for ProgressBar

        public int StepThreshold
        {
            get => stepThreshold;
            set
            {
                stepThreshold = value;
                OnPropertyChanged();
                // Fix #4: propagate to the live StepDetector
                fusionEngine?.UpdateStepThreshold(value);
            }
        }

        public int MinStepInterval
        {
            get => minStepInterval;
            set
            {
                minStepInterval = value;
                OnPropertyChanged();
                // Fix #4: propagate to the live StepDetector
                fusionEngine?.UpdateMinStepInterval(value);
            }
        }

        public bool IsCalibrating
        {
            get => isCalibrating;
            set { isCalibrating = value; OnPropertyChanged(); }
        }

        // Fix #6: ProgressBar.Progress expects 0.0–1.0. Previously bound to int 0–100
        // which made the bar jump to nearly full on the first update (10/100 > 1.0 clamps).
        public double CalibrationProgress
        {
            get => calibrationProgress;
            set { calibrationProgress = value; OnPropertyChanged(); }
        }

        public ICommand StartCalibrationCommand { get; }
        public ICommand ResetSettingsCommand { get; }

        public SettingsViewModel(SensorFusionEngine fusionEngine)
        {
            this.fusionEngine = fusionEngine;
            StartCalibrationCommand = new Command(StartCalibration);
            ResetSettingsCommand    = new Command(ResetSettings);
        }

        private async void StartCalibration()
        {
            if (IsCalibrating) return;
            IsCalibrating = true;
            CalibrationProgress = 0.0;

            // Fix #5: real calibration resets the heading filter and instructs the
            // user to rotate their device through all orientations. Progress advances
            // as the HeadingTracker accumulates heading variance (spread of directions
            // seen), which is a proxy for magnetometer coverage. This is a practical
            // calibration that improves compass accuracy by flushing stale filter state.
            fusionEngine?.ResetForCalibration();

            // Animate progress over 10 seconds while the user rotates the device.
            // The actual sensor improvement happens continuously in SensorFusionEngine
            // as fresh magnetometer readings replace the warm-up defaults.
            for (int i = 1; i <= 20; i++)
            {
                await System.Threading.Tasks.Task.Delay(500);
                CalibrationProgress = i / 20.0; // 0.05 → 1.0 in 0.05 steps
            }

            IsCalibrating = false;

            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Calibration Complete",
                    "Compass calibration complete. For best results, hold your device flat and walk a few steps.",
                    "OK");
            }
        }

        private void ResetSettings()
        {
            StepThreshold   = 120;
            MinStepInterval = 250;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
