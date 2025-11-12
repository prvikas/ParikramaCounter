using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace ParikramaCounter.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private int stepThreshold = 120;
        private int minStepInterval = 250;
        private bool isCalibrating;
        private int calibrationProgress;

        public int StepThreshold
        {
            get => stepThreshold;
            set { stepThreshold = value; OnPropertyChanged(); }
        }

        public int MinStepInterval
        {
            get => minStepInterval;
            set { minStepInterval = value; OnPropertyChanged(); }
        }

        public bool IsCalibrating
        {
            get => isCalibrating;
            set { isCalibrating = value; OnPropertyChanged(); }
        }

        public int CalibrationProgress
        {
            get => calibrationProgress;
            set { calibrationProgress = value; OnPropertyChanged(); }
        }

        public ICommand StartCalibrationCommand { get; }
        public ICommand ResetSettingsCommand { get; }

        public SettingsViewModel()
        {
            StartCalibrationCommand = new Command(StartCalibration);
            ResetSettingsCommand = new Command(ResetSettings);
        }

        private async void StartCalibration()
        {
            IsCalibrating = true;
            CalibrationProgress = 0;

            // Simulate calibration process (replace with actual magnetometer calibration)
            for (int i = 0; i <= 100; i += 10)
            {
                await System.Threading.Tasks.Task.Delay(300);
                CalibrationProgress = i;
            }

            IsCalibrating = false;
            await Application.Current.MainPage.DisplayAlert("Calibration Complete",
                "Magnetometer has been calibrated successfully!", "OK");
        }

        private void ResetSettings()
        {
            StepThreshold = 120;
            MinStepInterval = 250;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
