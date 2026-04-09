using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using ParikramaCounter.Models;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    public class TrackingViewModel : INotifyPropertyChanged, IDisposable
    {
        // Fix #3: track disposal to prevent double-unsubscribe
        private bool disposed = false;

        private readonly ISensorService sensorService;
        private readonly SensorFusionEngine fusionEngine = new SensorFusionEngine();
        private readonly ParikramaTracker parikramaTracker = new ParikramaTracker();

        // Fix #14: single source of truth for total sides — no more magic "4" scattered around
        private const int TotalSides = 4;

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
        private string movementStatus = "Stationary";

        // Vibration event support
        private string sidesInfo = $"0/{TotalSides} sides";

        // Circle tracking backing fields
        private double circleProgress;
        private string circleDirection = "Determining...";
        private int stepsInCircle;

        // ── Properties ────────────────────────────────────────────────────────────

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
                // RemainingParikramas is a computed property — notify it here
                OnPropertyChanged(nameof(RemainingParikramas));
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

        // Sides tracking display — Fix #14: uses TotalSides constant
        public string SidesInfo
        {
            get => sidesInfo;
            set { sidesInfo = value; OnPropertyChanged(); }
        }

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

        public ICommand StartStopCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand IncrementTargetCommand { get; }
        public ICommand DecrementTargetCommand { get; }

        // ── Constructor ───────────────────────────────────────────────────────────

        public TrackingViewModel(ISensorService sensorService)
        {
            this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));

            // Fix #3: event subscriptions are tracked and cleaned up in Dispose()
            this.sensorService.SensorDataReceived += OnSensorDataReceived;
            parikramaTracker.OnThirdSideCompleted += OnThirdSideCompleted;
            parikramaTracker.OnApproachingStart += OnApproachingStart;

            StartStopCommand = new Command(StartStop);
            ResetCommand = new Command(Reset);

            // Fix #11: upper bound (108) guards against unbounded increment
            IncrementTargetCommand = new Command(() => { if (TargetParikrama < 108) TargetParikrama++; });
            DecrementTargetCommand = new Command(() => { if (TargetParikrama > 1) TargetParikrama--; });

            parikramaTracker.TargetParikramaCount = targetParikrama;
        }

        // ── Sensor data handler ───────────────────────────────────────────────────

        private void OnSensorDataReceived(double[] accel, double[] gyro, double[] mag)
        {
            var data = fusionEngine.ProcessSensorData(accel, gyro, mag);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Heading = $"{data.Heading:F1}°";
                Direction = data.Direction;
                Steps = data.Steps;

                MovementStatus = fusionEngine.IsMoving ? "🚶 Walking" : "🛑 Stationary";

                CircleProgress = parikramaTracker.CurrentProgress;
                CircleDirection = parikramaTracker.GetDirection();
                StepsInCircle = parikramaTracker.CurrentStepsInCircle;

                // Fix #14: TotalSides constant used here
                SidesInfo = $"{parikramaTracker.SidesCompleted}/{TotalSides} sides";

                bool completed = parikramaTracker.CheckAndUpdateParikrama(
                    data.Heading,
                    data.Steps,
                    fusionEngine.IsMoving,
                    data.Timestamp
                );

                if (completed)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"🎉 Parikrama #{parikramaTracker.ParikramaCount}");
#endif
                    // Fix #2: ParikramaCount setter already fires OnPropertyChanged +
                    // RemainingParikramas + UpdateProgress — no manual duplicates needed
                    ParikramaCount = parikramaTracker.ParikramaCount;

                    if (parikramaTracker.IsTargetReached && !TargetReached)
                    {
                        TargetReached = true;
                        // Fix #4: async Task method; discard is intentional fire-and-forget
                        _ = VibrateForTargetCompletionAsync();
                    }
                    else
                    {
                        VibrateForParikramaCompletion();
                    }
                }
            });
        }

        // ── Vibration event callbacks ─────────────────────────────────────────────

        // Handle 3rd side completion
        private void OnThirdSideCompleted()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("🔔 3/4 sides completed!");
#endif
                VibrateForThirdSide();
            });
        }

        // Handle approaching start point
        private void OnApproachingStart()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("⚠️ Approaching starting point!");
#endif
                VibrateForApproachingStart();
            });
        }

        // ── Vibration methods ─────────────────────────────────────────────────────

        private void VibrateForThirdSide()
        {
            try
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(400));
            }
            catch { }
        }

        private void VibrateForApproachingStart()
        {
            try
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(200));
            }
            catch { }
        }

        private void VibrateForParikramaCompletion()
        {
            try
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(500));
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
#endif
            }
        }

        // Fix #4: async Task (not async void) — caller uses discard `_ =` to
        //         intentionally fire-and-forget while keeping exceptions observable
        // Fix #1: vibration duration is no longer hardcoded per-iteration
        private async Task VibrateForTargetCompletionAsync()
        {
            try
            {
                // Triple vibration pattern for target completion
                for (int i = 0; i < 3; i++)
                {
                    Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(300));
                    await Task.Delay(500);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Vibration error: {ex.Message}");
#endif
            }
        }

        // ── Commands ──────────────────────────────────────────────────────────────

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
            // Fix #7: always stop tracking on reset so the sensor loop cannot silently restart
            if (IsTracking)
            {
                sensorService.Stop();
                IsTracking = false;
            }

            fusionEngine.Reset();
            parikramaTracker.Reset();

            Steps = 0;
            ParikramaCount = 0;
            TargetReached = false;
            MovementStatus = "Stationary";

            // Fix #6: reset SidesInfo and CircleDirection so stale values are cleared
            // Fix #14: TotalSides constant used
            SidesInfo = $"0/{TotalSides} sides";
            CircleDirection = "Determining...";
            CircleProgress = 0;
            StepsInCircle = 0;

            UpdateProgress();
        }

        private void UpdateProgress()
        {
            ProgressPercentage = TargetParikrama > 0
                ? (double)ParikramaCount / TargetParikrama
                : 0;
            OnPropertyChanged(nameof(RemainingParikramas));
        }

        // ── IDisposable ───────────────────────────────────────────────────────────

        // Fix #3: unsubscribe all events to prevent memory leaks when ViewModel
        //         is discarded (e.g., page navigation, DI container teardown)
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            sensorService.SensorDataReceived -= OnSensorDataReceived;
            parikramaTracker.OnThirdSideCompleted -= OnThirdSideCompleted;
            parikramaTracker.OnApproachingStart -= OnApproachingStart;
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────────

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
