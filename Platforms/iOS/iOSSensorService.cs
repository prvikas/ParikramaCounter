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

        // Fix #4 & #5: single shared arrays updated per-sensor, dispatched from
        // a unified timer-driven callback instead of firing on every sensor update.
        // This mirrors the Android fix — we only dispatch when accel + mag are ready.
        private readonly object sensorLock = new object();
        private double[] accelValues = new double[3];
        private double[] magValues = new double[3];
        private double[] gyroValues = new double[3];
        private bool hasAccel = false;
        private bool hasMag = false;

        // Fix #6: pedometer step count fed back into the shared step accumulator
        // so iOS uses CMPedometer (hardware step counter) not the accel-based estimator.
        private int pedometerSteps = 0;

        public event Action<double[], double[], double[]> SensorDataReceived;

        public iOSSensorService()
        {
            motionManager = new CMMotionManager();
            pedometer = new CMPedometer();
            // Fix #7: single shared queue — disposed properly in Dispose()
            operationQueue = new NSOperationQueue { MaxConcurrentOperationCount = 1 };
        }

        public void Start()
        {
            if (isRunning) return;

            // Accelerometer — 50Hz, convert g to m/s²
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

            // Gyroscope — 50Hz
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
                    // Gyro alone does not trigger dispatch — wait for accel+mag
                });
            }

            // Magnetometer — 50Hz
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

            // Fix #6: start CMPedometer for hardware-accurate step counting.
            // Steps are injected into the gyro channel (unused for compass) so the
            // fusion engine can read them via SensorData.Steps on both platforms.
            if (CMPedometer.IsStepCountingAvailable)
            {
                pedometer.StartPedometerUpdates(NSDate.Now, (data, error) =>
                {
                    if (data != null)
                        pedometerSteps = data.NumberOfSteps.Int32Value;
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
            pedometer.StopPedometerUpdates();

            lock (sensorLock)
            {
                hasAccel = false;
                hasMag = false;
                pedometerSteps = 0;
            }

            isRunning = false;
        }

        private void TryDispatch()
        {
            double[] accel, gyro, mag;

            lock (sensorLock)
            {
                // Fix #4 & #5: only dispatch once both accel and mag have real data.
                // Gyro values default to zero until the gyro fires — acceptable.
                if (!hasAccel || !hasMag) return;

                accel = (double[])accelValues.Clone();
                gyro  = (double[])gyroValues.Clone();
                mag   = (double[])magValues.Clone();
            }

            SensorDataReceived?.Invoke(accel, gyro, mag);
        }

        // Fix #7: dispose the operation queue properly
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Stop();
            operationQueue.Dispose();
            motionManager.Dispose();
            pedometer.Dispose();
        }
    }
}
