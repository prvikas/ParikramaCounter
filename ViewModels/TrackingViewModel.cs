using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using ParikramaCounter.Services;
using ParikramaCounter.Models;

namespace ParikramaCounter.ViewModels
{
    public class TrackingViewModel : INotifyPropertyChanged
    {
        private readonly ISensorService sensorService;
        private readonly SensorFusionEngine fusionEngine = new SensorFusionEngine();
        private readonly ParikramaTracker parikramaTracker = new ParikramaTracker();
        private readonly TempleProfileService profileService = new TempleProfileService();

        private DateTime lastUIUpdate = DateTime.MinValue;
        private const int UI_UPDATE_INTERVAL_MS = 500;
        private bool hasStartedTracking = false;
        private TempleProfile currentTempleProfile;

        // UI Properties
        private bool isTracking;
        private string heading = "0°";
        private string direction = "N";
        private int parikramaCount;
        private int targetParikrama = 7;
        private double circleProgress;
        private string circleDirection = "Determining...";
        private double distanceInCircle;
        private string startStopButtonText = "Start";
        private bool targetReached;
        private string movementStatus = "Stationary";
        private string calibrationStatus = "Initializing...";
        private string pathShape = "Unknown";
        private string templeInfo = "";

        // Directional distances
        private double northDistance;
        private double eastDistance;
        private double southDistance;
        private double westDistance;
        private double neDistance;
        private double nwDistance;
        private double seDistance;
        private double swDistance;
        private int directionsCovered;

        // Properties
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

        public int ParikramaCount
        {
            get => parikramaCount;
            set
            {
                parikramaCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingParikramas));
            }
        }

        public int TargetParikrama
        {
            get => targetParikrama;
            set
            {
                targetParikrama = value;
                parikramaTracker.SetTarget(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingParikramas));
            }
        }

        public int RemainingParikramas => Math.Max(0, TargetParikrama - ParikramaCount);

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

        public double DistanceInCircle
        {
            get => distanceInCircle;
            set { distanceInCircle = value; OnPropertyChanged(); }
        }

        public bool TargetReached
        {
            get => targetReached;
            set { targetReached = value; OnPropertyChanged(); }
        }

        public string MovementStatus
        {
            get => movementStatus;
            set { movementStatus = value; OnPropertyChanged(); }
        }

        public string CalibrationStatus
        {
            get => calibrationStatus;
            set { calibrationStatus = value; OnPropertyChanged(); }
        }

        public string PathShape
        {
            get => pathShape;
            set { pathShape = value; OnPropertyChanged(); }
        }

        public string TempleInfo
        {
            get => templeInfo;
            set { templeInfo = value; OnPropertyChanged(); }
        }

        public double NorthDistance
        {
            get => northDistance;
            set { northDistance = value; OnPropertyChanged(); }
        }

        public double EastDistance
        {
            get => eastDistance;
            set { eastDistance = value; OnPropertyChanged(); }
        }

        public double SouthDistance
        {
            get => southDistance;
            set { southDistance = value; OnPropertyChanged(); }
        }

        public double WestDistance
        {
            get => westDistance;
            set { westDistance = value; OnPropertyChanged(); }
        }

        public double NEDistance
        {
            get => neDistance;
            set { neDistance = value; OnPropertyChanged(); }
        }

        public double NWDistance
        {
            get => nwDistance;
            set { nwDistance = value; OnPropertyChanged(); }
        }

        public double SEDistance
        {
            get => seDistance;
            set { seDistance = value; OnPropertyChanged(); }
        }

        public double SWDistance
        {
            get => swDistance;
            set { swDistance = value; OnPropertyChanged(); }
        }

        public int DirectionsCovered
        {
            get => directionsCovered;
            set { directionsCovered = value; OnPropertyChanged(); }
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

            parikramaTracker.SetTarget(targetParikrama);

            // Start sensors for background calibration
            sensorService.Start();

            // Initialize location & temple profile
            Task.Run(async () => await InitializeLocationAsync());
        }

        private async Task InitializeLocationAsync()
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status == PermissionStatus.Granted)
                {
                    var location = await Geolocation.GetLocationAsync(new GeolocationRequest
                    {
                        DesiredAccuracy = GeolocationAccuracy.Best,
                        Timeout = TimeSpan.FromSeconds(10)
                    });

                    if (location != null)
                    {
                        currentTempleProfile = await profileService.GetOrCreateProfileAsync(
                            location.Latitude,
                            location.Longitude
                        );

                        parikramaTracker.SetTempleProfile(currentTempleProfile);

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            TempleInfo = $"🕉️ {currentTempleProfile.Name}\n" +
                                       $"📊 Accuracy: {currentTempleProfile.Accuracy:F0}% ({currentTempleProfile.TotalParikramasCompleted} completed)";

                            if (currentTempleProfile.RecommendedParikramas > 0)
                            {
                                TargetParikrama = currentTempleProfile.RecommendedParikramas;
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Location init error: {ex.Message}");
            }
        }

        private void OnSensorDataReceived(double[] accel, double[] gyro, double[] mag)
        {
            var data = fusionEngine.ProcessSensorData(accel, gyro, mag);

            if (!fusionEngine.IsCalibrated)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CalibrationStatus = "⏳ Calibrating compass...";
                });
                return;
            }
            else if (CalibrationStatus != "")
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CalibrationStatus = "";
                });
            }

            if (isTracking && !hasStartedTracking)
            {
                parikramaTracker.StartTracking(fusionEngine.CalibratedStartHeading);
                hasStartedTracking = true;
            }

            bool completed = false;
            if (isTracking)
            {
                completed = parikramaTracker.Update(data.Heading, fusionEngine.IsMoving);
            }

            bool shouldUpdate = (DateTime.Now - lastUIUpdate).TotalMilliseconds >= UI_UPDATE_INTERVAL_MS || completed;

            if (!shouldUpdate)
            {
                return;
            }

            lastUIUpdate = DateTime.Now;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Heading = $"{data.Heading:F1}°";
                Direction = data.Direction;
                MovementStatus = fusionEngine.IsMoving ? "🚶 Walking" : "🛑 Stationary";
                CircleProgress = parikramaTracker.CircleProgress / 100.0;
                CircleDirection = parikramaTracker.GetDirection();
                DistanceInCircle = parikramaTracker.CurrentDistanceInCircle;

                // Update directional data
                var directions = parikramaTracker.DirectionalData;
                NorthDistance = directions.GetDistanceInDirection(DirectionalTracker.Direction.North);
                EastDistance = directions.GetDistanceInDirection(DirectionalTracker.Direction.East);
                SouthDistance = directions.GetDistanceInDirection(DirectionalTracker.Direction.South);
                WestDistance = directions.GetDistanceInDirection(DirectionalTracker.Direction.West);
                NEDistance = directions.GetDistanceInDirection(DirectionalTracker.Direction.NorthEast);
                NWDistance = directions.GetDistanceInDirection(DirectionalTracker.Direction.NorthWest);
                SEDistance = directions.GetDistanceInDirection(DirectionalTracker.Direction.SouthEast);
                SWDistance = directions.GetDistanceInDirection(DirectionalTracker.Direction.SouthWest);
                DirectionsCovered = directions.GetCoveredDirectionCount();
                PathShape = $"Shape: {directions.DetectPathShape()}";

                if (completed)
                {
                    System.Diagnostics.Debug.WriteLine($"🎉 Parikrama #{parikramaTracker.ParikramaCount}");

                    ParikramaCount = parikramaTracker.ParikramaCount;
                    OnPropertyChanged(nameof(ParikramaCount));
                    OnPropertyChanged(nameof(RemainingParikramas));

                    CircleProgress = 0;
                    DistanceInCircle = 0;

                    // FIXED: Get actual duration from tracker
                    double actualDuration = parikramaTracker.GetDuration();

                    // Update temple profile intelligence
                    if (currentTempleProfile != null)
                    {
                        Task.Run(async () =>
                        {
                            await profileService.UpdateProfileAsync(
                                currentTempleProfile,
                                parikramaTracker.DirectionalData,
                                actualDuration // Use actual duration
                            );
                        });

                        TempleInfo = $"🕉️ {currentTempleProfile.Name}\n" +
                                   $"📊 Accuracy: {currentTempleProfile.Accuracy:F0}% ({currentTempleProfile.TotalParikramasCompleted} completed)";
                    }

                    hasStartedTracking = false;

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

        private void VibrateForParikramaCompletion()
        {
            try
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(500));
            }
            catch { }
        }

        private async void VibrateForTargetCompletion()
        {
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(300));
                    await Task.Delay(200);
                }
            }
            catch { }
        }

        private void StartStop()
        {
            if (IsTracking)
            {
                IsTracking = false;
                hasStartedTracking = false;
            }
            else
            {
                if (!fusionEngine.IsCalibrated)
                {
                    CalibrationStatus = "⚠️ Please wait, calibrating...";
                    return;
                }

                IsTracking = true;
            }
        }

        private void Reset()
        {
            fusionEngine.Reset();
            parikramaTracker.Reset();
            ParikramaCount = 0;
            TargetReached = false;
            MovementStatus = "Stationary";
            CircleProgress = 0;
            DistanceInCircle = 0;
            hasStartedTracking = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
