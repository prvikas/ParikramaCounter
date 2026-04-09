using System;
using ParikramaCounter.Models;

namespace ParikramaCounter.Services
{
    public class SensorFusionEngine
    {
        private readonly KalmanFilter headingFilter = new KalmanFilter(0.001, 0.5);
        private readonly StepDetector stepDetector = new StepDetector();

        // Fix #12: renamed to GRAVITY_ALPHA to accurately reflect its role.
        // This is a low-pass filter coefficient — a high value (0.8) means the
        // gravity estimate changes slowly, which is correct for isolating the
        // gravity component from raw accelerometer data.
        private const double GRAVITY_ALPHA = 0.8;
        private const double MAG_ALPHA = 0.1;

        // Fix #12 & #15: initialise gravity to a neutral downward vector (0, 0, 9.81)
        // and magnetic to a reasonable non-zero mid-field value so the first few
        // sensor readings don't produce garbage heading output during warm-up.
        private double[] gravity  = { 0.0, 0.0, 9.81 };
        private double[] magnetic = { 20.0, 0.0, 45.0 }; // rough mid-latitude defaults (µT)

        public bool IsMoving => stepDetector.IsMoving;

        public SensorData ProcessSensorData(double[] accel, double[] gyro, double[] mag)
        {
            // Low-pass filter on raw accelerometer to extract gravity component.
            // Fix #12: was named ALPHA_HIGH_PASS but is actually a low-pass filter.
            gravity[0] = GRAVITY_ALPHA * gravity[0] + (1 - GRAVITY_ALPHA) * accel[0];
            gravity[1] = GRAVITY_ALPHA * gravity[1] + (1 - GRAVITY_ALPHA) * accel[1];
            gravity[2] = GRAVITY_ALPHA * gravity[2] + (1 - GRAVITY_ALPHA) * accel[2];

            // Subtract gravity to isolate linear acceleration
            double linearAccelX = accel[0] - gravity[0];
            double linearAccelY = accel[1] - gravity[1];
            double linearAccelZ = accel[2] - gravity[2];

            // Low-pass filter on magnetometer to reduce noise
            magnetic[0] = MAG_ALPHA * mag[0] + (1 - MAG_ALPHA) * magnetic[0];
            magnetic[1] = MAG_ALPHA * mag[1] + (1 - MAG_ALPHA) * magnetic[1];
            magnetic[2] = MAG_ALPHA * mag[2] + (1 - MAG_ALPHA) * magnetic[2];

            double accelMagnitude = Math.Sqrt(
                linearAccelX * linearAccelX +
                linearAccelY * linearAccelY +
                linearAccelZ * linearAccelZ
            );

            // Fix #13: DetectStep return value was assigned but never used.
            // Step count is read from stepDetector.StepCount — the bool is not needed.
            stepDetector.DetectStep(accelMagnitude);

            double heading = CalculateTiltCompensatedHeading(gravity, magnetic);
            double filteredHeading = headingFilter.Update(heading);

            return new SensorData
            {
                AccelX = accel[0],
                AccelY = accel[1],
                AccelZ = accel[2],
                GyroX = gyro[0],
                GyroY = gyro[1],
                GyroZ = gyro[2],
                MagX = magnetic[0],
                MagY = magnetic[1],
                MagZ = magnetic[2],
                Heading = filteredHeading,
                TrueHeading = filteredHeading,
                Direction = GetDirectionFromHeading(filteredHeading),
                Steps = stepDetector.StepCount,
                AccelerationMagnitude = accelMagnitude,
                Timestamp = DateTime.Now
            };
        }

        private double CalculateTiltCompensatedHeading(double[] grav, double[] mag)
        {
            double gNorm = Math.Sqrt(grav[0] * grav[0] + grav[1] * grav[1] + grav[2] * grav[2]);
            if (gNorm < 0.001) return 0; // guard against near-zero gravity vector

            double gx = grav[0] / gNorm;
            double gy = grav[1] / gNorm;
            double gz = grav[2] / gNorm;

            double pitchAngle = Math.Asin(Math.Max(-1.0, Math.Min(1.0, -gx)));
            double rollAngle  = Math.Atan2(gy, gz);

            double magX = mag[0] * Math.Cos(pitchAngle) +
                          mag[2] * Math.Sin(pitchAngle);

            double magY = mag[0] * Math.Sin(rollAngle) * Math.Sin(pitchAngle) +
                          mag[1] * Math.Cos(rollAngle) -
                          mag[2] * Math.Sin(rollAngle) * Math.Cos(pitchAngle);

            double heading = Math.Atan2(magY, magX) * (180.0 / Math.PI);
            if (heading < 0) heading += 360.0;

            return heading;
        }

        private string GetDirectionFromHeading(double heading)
        {
            string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            int index = (int)Math.Round(heading / 45.0) % 8;
            return directions[index];
        }

        public void Reset()
        {
            stepDetector.Reset();
            headingFilter.Reset();
            gravity  = new double[] { 0.0, 0.0, 9.81 };
            magnetic = new double[] { 20.0, 0.0, 45.0 };
        }
    }
}
