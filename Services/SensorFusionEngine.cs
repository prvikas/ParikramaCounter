using System;
using ParikramaCounter.Models;

namespace ParikramaCounter.Services
{
    // Fix #2: implements ISensorFusionEngine so ViewModels depend on the interface.
    // Fix #1: no longer casts to AndroidSensorService — calls ISensorService.UpdateStepCount()
    // Fix #8: SensorService injected via constructor — no longer a mutable public property.
    public class SensorFusionEngine : ISensorFusionEngine
    {
        private readonly KalmanFilter  headingFilter;
        private readonly StepDetector  stepDetector;
        private readonly ISensorService sensorService;

        private const double GRAVITY_ALPHA = 0.8;
        private const double MAG_ALPHA     = 0.1;

        private double[] gravity  = { 0.0, 0.0, 9.81 };
        private double[] magnetic = { 20.0, 0.0, 45.0 };

        public bool IsMoving  => stepDetector.IsMoving;
        public int  StepCount => stepDetector.StepCount;

        public SensorFusionEngine(ISensorService sensorService)
        {
            this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));
            headingFilter = new KalmanFilter(0.001, 0.5);
            stepDetector  = new StepDetector();
        }

        public SensorData ProcessSensorData(double[] accel, double[] gyro, double[] mag)
        {
            gravity[0] = GRAVITY_ALPHA * gravity[0] + (1 - GRAVITY_ALPHA) * accel[0];
            gravity[1] = GRAVITY_ALPHA * gravity[1] + (1 - GRAVITY_ALPHA) * accel[1];
            gravity[2] = GRAVITY_ALPHA * gravity[2] + (1 - GRAVITY_ALPHA) * accel[2];

            double lx = accel[0] - gravity[0];
            double ly = accel[1] - gravity[1];
            double lz = accel[2] - gravity[2];

            magnetic[0] = MAG_ALPHA * mag[0] + (1 - MAG_ALPHA) * magnetic[0];
            magnetic[1] = MAG_ALPHA * mag[1] + (1 - MAG_ALPHA) * magnetic[1];
            magnetic[2] = MAG_ALPHA * mag[2] + (1 - MAG_ALPHA) * magnetic[2];

            double accelMag = Math.Sqrt(lx*lx + ly*ly + lz*lz);
            stepDetector.DetectStep(accelMag);

            int swSteps = stepDetector.StepCount;

            // Fix #1: no more #if ANDROID cast to concrete type.
            // UpdateStepCount is defined on ISensorService — both platforms implement it.
            // On Android it writes to the backing field; on iOS it's a no-op (CMPedometer owns the value).
            sensorService.UpdateStepCount(swSteps);

            int hwSteps = sensorService.HardwareStepCount;
            int steps   = hwSteps > 0 ? hwSteps : swSteps;

            double heading         = CalculateTiltCompensatedHeading(gravity, magnetic);
            double filteredHeading = headingFilter.Update(heading);

            return new SensorData
            {
                AccelX = accel[0], AccelY = accel[1], AccelZ = accel[2],
                GyroX  = gyro[0],  GyroY  = gyro[1],  GyroZ  = gyro[2],
                MagX   = magnetic[0], MagY = magnetic[1], MagZ = magnetic[2],
                Heading     = filteredHeading,
                TrueHeading = filteredHeading,
                Direction   = GetDirectionFromHeading(filteredHeading),
                Steps       = steps,
                AccelerationMagnitude = accelMag,
                Timestamp   = DateTime.Now
            };
        }

        private double CalculateTiltCompensatedHeading(double[] grav, double[] mag)
        {
            double gNorm = Math.Sqrt(grav[0]*grav[0] + grav[1]*grav[1] + grav[2]*grav[2]);
            if (gNorm < 0.001) return 0;
            double gx = grav[0]/gNorm, gy = grav[1]/gNorm, gz = grav[2]/gNorm;
            double pitch = Math.Asin(Math.Max(-1.0, Math.Min(1.0, -gx)));
            double roll  = Math.Atan2(gy, gz);
            double mX    = mag[0]*Math.Cos(pitch) + mag[2]*Math.Sin(pitch);
            double mY    = mag[0]*Math.Sin(roll)*Math.Sin(pitch)
                         + mag[1]*Math.Cos(roll)
                         - mag[2]*Math.Sin(roll)*Math.Cos(pitch);
            double h     = Math.Atan2(mY, mX) * (180.0 / Math.PI);
            if (h < 0) h += 360.0;
            return h;
        }

        private static string GetDirectionFromHeading(double heading)
        {
            string[] d = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return d[(int)Math.Round(heading / 45.0) % 8];
        }

        public void Reset()
        {
            stepDetector.Reset();
            headingFilter.Reset();
            gravity  = new double[] { 0.0, 0.0, 9.81 };
            magnetic = new double[] { 20.0, 0.0, 45.0 };
        }

        public void ResetForCalibration()
        {
            headingFilter.Reset();
            magnetic = new double[] { 20.0, 0.0, 45.0 };
        }

        public void UpdateStepThreshold(int threshold) => stepDetector.SetThresholdMultiplier(threshold / 100.0);
        public void UpdateMinStepInterval(int ms)       => stepDetector.SetMinStepInterval(ms);
    }
}
