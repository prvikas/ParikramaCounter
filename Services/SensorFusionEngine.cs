using System;
using ParikramaCounter.Models;

namespace ParikramaCounter.Services
{
    public class SensorFusionEngine
    {
        private readonly KalmanFilter headingFilter = new KalmanFilter(0.001, 0.5);
        private readonly StepDetector stepDetector = new StepDetector();

        private double[] gravity = new double[3];
        private double[] magnetic = new double[3];
        private const double ALPHA_HIGH_PASS = 0.8;
        private const double ALPHA_LOW_PASS = 0.1;
        public bool IsMoving => stepDetector.IsMoving; // Expose movement status


        public SensorData ProcessSensorData(double[] accel, double[] gyro, double[] mag)
        {
            // High-pass filter to remove gravity
            gravity[0] = ALPHA_HIGH_PASS * gravity[0] + (1 - ALPHA_HIGH_PASS) * accel[0];
            gravity[1] = ALPHA_HIGH_PASS * gravity[1] + (1 - ALPHA_HIGH_PASS) * accel[1];
            gravity[2] = ALPHA_HIGH_PASS * gravity[2] + (1 - ALPHA_HIGH_PASS) * accel[2];

            double linearAccelX = accel[0] - gravity[0];
            double linearAccelY = accel[1] - gravity[1];
            double linearAccelZ = accel[2] - gravity[2];

            // Low-pass filter on magnetometer
            magnetic[0] = ALPHA_LOW_PASS * mag[0] + (1 - ALPHA_LOW_PASS) * magnetic[0];
            magnetic[1] = ALPHA_LOW_PASS * mag[1] + (1 - ALPHA_LOW_PASS) * magnetic[1];
            magnetic[2] = ALPHA_LOW_PASS * mag[2] + (1 - ALPHA_LOW_PASS) * magnetic[2];

            // Calculate acceleration magnitude for step detection
            double accelMagnitude = Math.Sqrt(
                linearAccelX * linearAccelX +
                linearAccelY * linearAccelY +
                linearAccelZ * linearAccelZ
            );

            bool stepDetected = stepDetector.DetectStep(accelMagnitude);

            // Tilt-compensated compass calculation
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
                TrueHeading = filteredHeading, // Add magnetic declination if needed
                Direction = GetDirectionFromHeading(filteredHeading),
                Steps = stepDetector.StepCount,
                AccelerationMagnitude = accelMagnitude,
                Timestamp = DateTime.Now
            };
        }

        private double CalculateTiltCompensatedHeading(double[] gravity, double[] mag)
        {
            // Normalize gravity vector
            double gNorm = Math.Sqrt(gravity[0] * gravity[0] + gravity[1] * gravity[1] + gravity[2] * gravity[2]);
            if (gNorm == 0) return 0;

            double gx = gravity[0] / gNorm;
            double gy = gravity[1] / gNorm;
            double gz = gravity[2] / gNorm;

            // Calculate tilt-compensated magnetic field
            double pitchAngle = Math.Asin(-gx);
            double rollAngle = Math.Atan2(gy, gz);

            double magX = mag[0] * Math.Cos(pitchAngle) +
                         mag[2] * Math.Sin(pitchAngle);

            double magY = mag[0] * Math.Sin(rollAngle) * Math.Sin(pitchAngle) +
                         mag[1] * Math.Cos(rollAngle) -
                         mag[2] * Math.Sin(rollAngle) * Math.Cos(pitchAngle);

            double heading = Math.Atan2(magY, magX) * (180.0 / Math.PI);
            if (heading < 0) heading += 360;

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
        }
    }
}
