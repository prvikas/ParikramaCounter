//using System;
//using CoreMotion;
//using Foundation;
//using ParikramaCounter.Services;

//namespace ParikramaCounter.Platforms.iOS
//{
//    public class iOSSensorService : ISensorService
//    {
//        private CMMotionManager motionManager;
//        private CMPedometer pedometer;
//        private NSOperationQueue operationQueue;
//        private bool isRunning;

//        private double[] accelValues = new double[3];
//        private double[] gyroValues = new double[3];
//        private double[] magValues = new double[3];

//        private int totalSteps = 0;
//        private DateTime startTime;

//        public event Action<double[], double[], double[]> SensorDataReceived;
//        public event Action<int> StepCountChanged;

//        public iOSSensorService()
//        {
//            motionManager = new CMMotionManager();
//            pedometer = new CMPedometer();
//            operationQueue = new NSOperationQueue();
//        }

//        public void Start()
//        {
//            if (isRunning) return;

//            startTime = DateTime.Now;

//            // Start Accelerometer
//            if (motionManager.AccelerometerAvailable)
//            {
//                motionManager.AccelerometerUpdateInterval = 0.02; // 50Hz
//                motionManager.StartAccelerometerUpdates(operationQueue, (data, error) =>
//                {
//                    if (data != null)
//                    {
//                        accelValues[0] = data.Acceleration.X * 9.81;
//                        accelValues[1] = data.Acceleration.Y * 9.81;
//                        accelValues[2] = data.Acceleration.Z * 9.81;
//                        NotifySensorData();
//                    }
//                });
//            }

//            // Start Gyroscope
//            if (motionManager.GyroAvailable)
//            {
//                motionManager.GyroUpdateInterval = 0.02;
//                motionManager.StartGyroUpdates(operationQueue, (data, error) =>
//                {
//                    if (data != null)
//                    {
//                        gyroValues[0] = data.RotationRate.x;
//                        gyroValues[1] = data.RotationRate.y;
//                        gyroValues[2] = data.RotationRate.z;
//                        NotifySensorData();
//                    }
//                });
//            }

//            // Start Magnetometer
//            if (motionManager.MagnetometerAvailable)
//            {
//                motionManager.MagnetometerUpdateInterval = 0.02;
//                motionManager.StartMagnetometerUpdates(operationQueue, (data, error) =>
//                {
//                    if (data != null)
//                    {
//                        magValues[0] = data.MagneticField.X;
//                        magValues[1] = data.MagneticField.Y;
//                        magValues[2] = data.MagneticField.Z;
//                        NotifySensorData();
//                    }
//                });
//            }

//            // Start Pedometer (Step Counter)
//            if (CMPedometer.IsStepCountingAvailable)
//            {
//                pedometer.StartPedometerUpdates(NSDate.Now, (data, error) =>
//                {
//                    if (data != null)
//                    {
//                        totalSteps = (int)data.NumberOfSteps.Int32Value;
//                        StepCountChanged?.Invoke(totalSteps);
//                    }
//                });
//            }

//            isRunning = true;
//        }

//        public void Stop()
//        {
//            if (!isRunning) return;

//            motionManager.StopAccelerometerUpdates();
//            motionManager.StopGyroUpdates();
//            motionManager.StopMagnetometerUpdates();
//            pedometer.StopPedometerUpdates();

//            isRunning = false;
//        }

//        private void NotifySensorData()
//        {
//            SensorDataReceived?.Invoke(
//                (double[])accelValues.Clone(),
//                (double[])gyroValues.Clone(),
//                (double[])magValues.Clone()
//            );
//        }
//    }
//}
