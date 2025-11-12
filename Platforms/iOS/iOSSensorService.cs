using System;
using CoreMotion;
using Foundation;
using ParikramaCounter.Services;

namespace ParikramaCounter.Platforms.iOS
{
    public class iOSSensorService : ISensorService
    {
        private readonly CMMotionManager motionManager;
        private readonly CMPedometer pedometer;
        private readonly NSOperationQueue operationQueue;
        private bool isRunning;

        private double[] accelValues = new double[3];
        private double[] gyroValues = new double[3];
        private double[] magValues = new double[3];

        public event Action<double[], double[], double[]> SensorDataReceived;

        public iOSSensorService()
        {
            motionManager = new CMMotionManager();
            pedometer = new CMPedometer();
            operationQueue = new NSOperationQueue();
        }

        public void Start()
        {
            if (isRunning) return;

            // Start Accelerometer
            if (motionManager.AccelerometerAvailable)
            {
                motionManager.AccelerometerUpdateInterval = 0.02; // 50Hz
                motionManager.StartAccelerometerUpdates(operationQueue, (data, error) =>
                {
                    if (data != null)
                    {
                        accelValues[0] = data.Acceleration.X * 9.81; // Convert to m/s²
                        accelValues[1] = data.Acceleration.Y * 9.81;
                        accelValues[2] = data.Acceleration.Z * 9.81;
                        NotifySensorData();
                    }
                });
            }

            // Start Gyroscope
            if (motionManager.GyroAvailable)
            {
                motionManager.GyroUpdateInterval = 0.02; // 50Hz
                motionManager.StartGyroUpdates(operationQueue, (data, error) =>
                {
                    if (data != null)
                    {
                        gyroValues[0] = data.RotationRate.x;
                        gyroValues[1] = data.RotationRate.y;
                        gyroValues[2] = data.RotationRate.z;
                        NotifySensorData();
                    }
                });
            }

            // Start Magnetometer
            if (motionManager.MagnetometerAvailable)
            {
                motionManager.MagnetometerUpdateInterval = 0.02; // 50Hz
                motionManager.StartMagnetometerUpdates(operationQueue, (data, error) =>
                {
                    if (data != null)
                    {
                        magValues[0] = data.MagneticField.X;
                        magValues[1] = data.MagneticField.Y;
                        magValues[2] = data.MagneticField.Z;
                        NotifySensorData();
                    }
                });
            }

            isRunning = true;
        }

        public void Stop()
        {
            if (!isRunning) return;

            motionManager.StopAccelerometerUpdates();
            motionManager.StopGyroUpdates();
            motionManager.StopMagnetometerUpdates();

            isRunning = false;
        }

        private void NotifySensorData()
        {
            SensorDataReceived?.Invoke(
                (double[])accelValues.Clone(),
                (double[])gyroValues.Clone(),
                (double[])magValues.Clone()
            );
        }
    }
}
