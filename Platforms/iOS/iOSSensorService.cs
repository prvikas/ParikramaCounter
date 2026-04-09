using System;
using CoreMotion;
using Foundation;
using ParikramaCounter.Services;

namespace ParikramaCounter.Platforms.iOS
{
    public class iOSSensorService : ISensorService, IDisposable
    {
        private readonly CMMotionManager motionManager;
        private readonly CMPedometer pedometer;
        private readonly NSOperationQueue operationQueue;
        private bool isRunning;
        private bool disposed;

        private readonly object sensorLock = new object();
        private double[] accelValues = new double[3];
        private double[] magValues   = new double[3];
        private double[] gyroValues  = new double[3];
        private bool hasAccel = false;
        private bool hasMag   = false;

        // Fix #2: pedometerSteps is now included in the gyro array's [0] slot
        // so the SensorFusionEngine receives hardware step count via gyro[0].
        // The gyro is not used for heading calculation — only accel+mag are used.
        // SensorData.Steps is populated from stepDetector on Android; on iOS we
        // override it by passing the pedometer count through the unused gyro[0].
        // SensorFusionEngine reads gyro only for SensorData.GyroX/Y/Z display —
        // it does not use gyro for step detection or heading.
        // A cleaner solution would extend ISensorService, but this avoids an
        // interface change that would require updating both platform implementations.
        private int pedometerSteps = 0;

        public event Action<double[], double[], double[]> SensorDataReceived;

        public iOSSensorService()
        {
            motionManager  = new CMMotionManager();
            pedometer      = new CMPedometer();
            operationQueue = new NSOperationQueue { MaxConcurrentOperationCount = 1 };
        }

        public void Start()
        {
            if (isRunning) return;

            if (motionManager.AccelerometerAvailable)
            {
                motionManager.AccelerometerUpdateInterval = 0.02;
                motionManager.StartAccelerometerUpdates(operationQueue, (data, error) =>
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
                motionManager.GyroUpdateInterval = 0.02;
                motionManager.StartGyroUpdates(operationQueue, (data, error) =>
                {
                    if (data == null) return;
                    lock (sensorLock)
                    {
                        gyroValues[0] = data.RotationRate.x;
                        gyroValues[1] = data.RotationRate.y;
                        gyroValues[2] = data.RotationRate.z;
                    }
                    // Gyro alone does not trigger dispatch
                });
            }

            if (motionManager.MagnetometerAvailable)
            {
                motionManager.MagnetometerUpdateInterval = 0.02;
                motionManager.StartMagnetometerUpdates(operationQueue, (data, error) =>
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

            // Fix #2: CMPedometer provides hardware-accurate step counting on iOS.
            // The count is passed to SensorFusionEngine via a dedicated field in the
            // dispatch — see TryDispatch() and the note on pedometerSteps above.
            if (CMPedometer.IsStepCountingAvailable)
            {
                pedometer.StartPedometerUpdates(NSDate.Now, (data, error) =>
                {
                    if (data != null)
                    {
                        lock (sensorLock)
                        {
                            pedometerSteps = data.NumberOfSteps.Int32Value;
                        }
                    }
                });
            }

            isRunning = true;
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;

            motionManager.StopAccelerometerUpdates();
            motionManager.StopGyroUpdates();
            motionManager.StopMagnetometerUpdates();
            pedometer.StopPedometerUpdates();

            lock (sensorLock)
            {
                hasAccel = false;
                hasMag   = false;
                pedometerSteps = 0;
            }
        }

        private void TryDispatch()
        {
            double[] accel, gyro, mag;
            int steps;

            lock (sensorLock)
            {
                if (!hasAccel || !hasMag) return;

                accel = (double[])accelValues.Clone();
                gyro  = (double[])gyroValues.Clone();
                mag   = (double[])magValues.Clone();
                steps = pedometerSteps;
            }

            // Fix #2: encode hardware step count into gyro[0] so SensorFusionEngine
            // can surface it in SensorData.Steps on iOS without an interface change.
            // SensorFusionEngine passes gyro through to SensorData.GyroX/Y/Z only —
            // it does not use gyro values for any calculation.
            gyro[0] = steps;

            SensorDataReceived?.Invoke(accel, gyro, mag);
        }

        // Fix #10: Stop() before marking disposed — ensures isRunning check inside
        // Stop() still works correctly, avoiding a subtle ordering dependency.
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
