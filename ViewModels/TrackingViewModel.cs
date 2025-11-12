using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using ParikramaCounter.Services;
using ParikramaCounter.Models;

namespace ParikramaCounter.ViewModels
{
    public class TrackingViewModel : INotifyPropertyChanged
    {
        private readonly ISensorService sensorService;
        private readonly SensorFusionEngine fusionEngine = new SensorFusionEngine();
        private readonly ParikramaTracker parikramaTracker = new ParikramaTracker();

        private bool isTracking;
        private string heading = "0°";
        private string direction = "N";
        private int steps;
        private int parikramaCount;
        private int targetParikrama = 7;
        private double progressPercentage;
        private string accuracy = "High";
        private string startStopButtonText = "Start";
        private bool targetReached;
        private bool isMoving;
        private string movementStatus = "Stationary";

        public bool IsTracking
        {
            get => isTracking;
            set
            {
                isTracking = value;
                StartStopButtonText = value ? "Stop" : "Start";
                OnPropertyChanged();
            }
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
                UpdateProgress();
            }
        }

        public int TargetParikrama
        {
            get => targetParikrama;
            set
            {
                targetParikrama = value;
                parikramaTracker.TargetParikramaCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingParikramas));
                UpdateProgress();
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

        public string Accuracy
        {
            get => accuracy;
            set { accuracy = value; OnPropertyChanged(); }
        }

        public string MovementStatus
        {
            get => movementStatus;
            set { movementStatus = value; OnPropertyChanged(); }
        }

        public ICommand StartStopCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand IncrementTargetCommand { get; }
        public ICommand DecrementTargetCommand { get; }

        public TrackingViewModel(ISensorService sensorService)
        {
            this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));
            this.sensorService.SensorDataReceived += OnSensorDataReceived;

            StartStopCommand = new Command(StartStop);
            ResetCommand = new Command(Reset);
            IncrementTargetCommand = new Command(() => TargetParikrama++);
            DecrementTargetCommand = new Command(() => { if (TargetParikrama > 1) TargetParikrama--; });

            parikramaTracker.TargetParikramaCount = targetParikrama;
        }

        private void OnSensorDataReceived(double[] accel, double[] gyro, double[] mag)
        {
            var data = fusionEngine.ProcessSensorData(accel, gyro, mag);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Heading = $"{data.Heading:F1}°";
                Direction = data.Direction;
                Steps = data.Steps;

                // Update movement status
                MovementStatus = fusionEngine.IsMoving ? "Walking" : "Stationary";

                // Update parikrama progress
                CircleProgress = parikramaTracker.CurrentProgress;
                CircleDirection = parikramaTracker.GetDirection();
                StepsInCircle = parikramaTracker.CurrentStepsInCircle;

                // Check parikrama completion using heading-based algorithm
                bool completed = parikramaTracker.CheckAndUpdateParikrama(
                    data.Heading,
                    data.Steps,
                    fusionEngine.IsMoving,
                    data.Timestamp
                );

                if (completed)
                {
                    ParikramaCount = parikramaTracker.ParikramaCount;

                    if (parikramaTracker.IsTargetReached && !TargetReached)
                    {
                        TargetReached = true;
                        VibrateForTargetCompletion();
                    }
                    else
                    {
                        VibrateForParikramaCompletion();
                    }
                }
            });
        }

        // Add new properties
        private double circleProgress;
        private string circleDirection = "Determining...";
        private int stepsInCircle;

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


        private void UpdateProgress()
        {
            ProgressPercentage = TargetParikrama > 0
                ? (double)ParikramaCount / TargetParikrama
                : 0;
            OnPropertyChanged(nameof(RemainingParikramas));
        }

        private void VibrateForParikramaCompletion()
        {
            try
            {
                var duration = TimeSpan.FromMilliseconds(500);
                Vibration.Default.Vibrate(duration);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
            }
        }

        private async void VibrateForTargetCompletion()
        {
            try
            {
                // Triple vibration for target completion
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(300));
                await System.Threading.Tasks.Task.Delay(200);
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(300));
                await System.Threading.Tasks.Task.Delay(200);
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(300));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
            }
        }

        private void StartStop()
        {
            if (IsTracking)
                sensorService.Stop();
            else
                sensorService.Start();

            IsTracking = !IsTracking;
        }

        private void Reset()
        {
            fusionEngine.Reset();
            parikramaTracker.Reset();
            Steps = 0;
            ParikramaCount = 0;
            TargetReached = false;
            MovementStatus = "Stationary";
            UpdateProgress();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
