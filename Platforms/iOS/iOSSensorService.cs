using System;
using CoreMotion;
using Foundation;
using ParikramaCounter.Services;

namespace ParikramaCounter.Platforms.iOS
{
    public class iOSSensorService : ISensorService, IDisposable
    {
        private readonly CMMotionManager  motionManager;
        private readonly CMPedometer      pedometer;
        private readonly NSOperationQueue operationQueue;
        private bool isRunning;
        private bool disposed;
        private bool currentHighRate = false;

        private readonly object sensorLock = new object();
        private double[] accelValues = new double[3];
        private double[] gyroValues  = new double[3];
        private double[] magValues   = new double[3];
        private bool hasAccel = false;
        private bool hasMag   = false;

        public int HardwareStepCount { get; private set; }

        public event Action<double[], double[], double[]> SensorDataReceived;

        public iOSSensorService()
        {
            motionManager  = new CMMotionManager();
            pedometer      = new CMPedometer();
            operationQueue = new NSOperationQueue { MaxConcurrentOperationCount = 1 };
        }

        // Fix #2: Start() no args — idle rate; caller uses SetRate() to switch
        public void Start()
        {
            if (isRunning) return;
            StartSensors(false);
            isRunning = true;
        }

        public void SetRate(bool highRate)
        {
            if (!isRunning || currentHighRate == highRate) return;
            currentHighRate = highRate;
            motionManager.StopAccelerometerUpdates();
            motionManager.StopGyroUpdates();
            motionManager.StopMagnetometerUpdates();
            pedometer.StopPedometerUpdates();
            lock (sensorLock) { hasAccel = false; hasMag = false; }
            StartSensors(highRate);
        }

        private void StartSensors(bool highRate)
        {
            double interval = highRate ? 0.02 : 0.2;
            currentHighRate = highRate;

            if (motionManager.AccelerometerAvailable)
            {
                motionManager.AccelerometerUpdateInterval = interval;
                motionManager.StartAccelerometerUpdates(operationQueue, (data, _) =>
                {
                    if (data == null) return;
                    lock (sensorLock)
                    {
                        accelValues[0] = data.Acceleration.X * 9.81;
                        accelValues[1] = data.Acceleration.Y * 9.81;
                        accelValues[2] = data.Acceleration.Z * 9.81;
                        hasAccel = true;
                    }
                    TryDispatch();
                });
            }
            if (motionManager.GyroAvailable)
            {
                motionManager.GyroUpdateInterval = interval;
                motionManager.StartGyroUpdates(operationQueue, (data, _) =>
                {
                    if (data == null) return;
                    lock (sensorLock)
                    {
                        gyroValues[0] = data.RotationRate.x;
                        gyroValues[1] = data.RotationRate.y;
                        gyroValues[2] = data.RotationRate.z;
                    }
                });
            }
            if (motionManager.MagnetometerAvailable)
            {
                motionManager.MagnetometerUpdateInterval = interval;
                motionManager.StartMagnetometerUpdates(operationQueue, (data, _) =>
                {
                    if (data == null) return;
                    lock (sensorLock)
                    {
                        magValues[0] = data.MagneticField.X;
                        magValues[1] = data.MagneticField.Y;
                        magValues[2] = data.MagneticField.Z;
                        hasMag = true;
                    }
                    TryDispatch();
                });
            }
            if (CMPedometer.IsStepCountingAvailable)
                pedometer.StartPedometerUpdates(NSDate.Now, (data, _) =>
                {
                    if (data != null) HardwareStepCount = data.NumberOfSteps.Int32Value;
                });
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
            motionManager.StopAccelerometerUpdates();
            motionManager.StopGyroUpdates();
            motionManager.StopMagnetometerUpdates();
            pedometer.StopPedometerUpdates();
            lock (sensorLock) { hasAccel = false; hasMag = false; HardwareStepCount = 0; }
        }

        private void TryDispatch()
        {
            double[] accel, gyro, mag;
            lock (sensorLock)
            {
                if (!hasAccel || !hasMag) return;
                accel = (double[])accelValues.Clone();
                gyro  = (double[])gyroValues.Clone();
                mag   = (double[])magValues.Clone();
            }
            SensorDataReceived?.Invoke(accel, gyro, mag);
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            disposed = true;
            operationQueue.Dispose();
            motionManager.Dispose();
            pedometer.Dispose();
        }
    }
}
