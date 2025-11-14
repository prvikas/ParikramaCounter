using System;
using ParikramaCounter.Models;

namespace ParikramaCounter.Services
{
    public class SensorFusionEngine
    {
        private readonly StepDetector stepDetector = new StepDetector();
        private double[] gravity = new double[3];
        private double[] magnetic = new double[3];
        private const double ALPHA_GRAVITY = 0.2;
        private const double ALPHA_MAG = 0.1;

        public bool IsMoving => stepDetector.IsMoving;

        public SensorData ProcessSensorData(double[] accel, double[] gyro, double[] mag)
        {
            // Low-pass filter for gravity
            gravity[0] = ALPHA_GRAVITY * accel[0] + (1 - ALPHA_GRAVITY) * gravity[0];
            gravity[1] = ALPHA_GRAVITY * accel[1] + (1 - ALPHA_GRAVITY) * gravity[1];
            gravity[2] = ALPHA_GRAVITY * accel[2] + (1 - ALPHA_GRAVITY) * gravity[2];

            // Get linear acceleration (remove gravity)
            double linearX = accel[0] - gravity[0];
            double linearY = accel[1] - gravity[1];
            double linearZ = accel[2] - gravity[2];
            double accelMag = Math.Sqrt(linearX * linearX + linearY * linearY + linearZ * linearZ);

            // Update movement detector
            stepDetector.Update(accelMag);

            // Filter magnetometer
            magnetic[0] = ALPHA_MAG * mag[0] + (1 - ALPHA_MAG) * magnetic[0];
            magnetic[1] = ALPHA_MAG * mag[1] + (1 - ALPHA_MAG) * magnetic[1];
            magnetic[2] = ALPHA_MAG * mag[2] + (1 - ALPHA_MAG) * magnetic[2];

            // Calculate tilt-compensated compass heading
            double heading = CalculateTiltCompensatedHeading(gravity, magnetic);

            return new SensorData
            {
                Heading = heading,
                Direction = GetDirectionFromHeading(heading),
                AccelerationMagnitude = accelMag,
                Timestamp = DateTime.Now
            };
        }

        private double CalculateTiltCompensatedHeading(double[] grav, double[] mag)
        {
            // Normalize gravity
            double gNorm = Math.Sqrt(grav[0] * grav[0] + grav[1] * grav[1] + grav[2] * grav[2]);
            if (gNorm < 0.01) return 0;

            double gx = grav[0] / gNorm;
            double gy = grav[1] / gNorm;
            double gz = grav[2] / gNorm;

            // Calculate pitch and roll
            double pitch = Math.Asin(-gx);
            double roll = Math.Atan2(gy, gz);

            // Tilt compensation
            double magX = mag[0] * Math.Cos(pitch) + mag[2] * Math.Sin(pitch);
            double magY = mag[0] * Math.Sin(roll) * Math.Sin(pitch) +
                         mag[1] * Math.Cos(roll) -
                         mag[2] * Math.Sin(roll) * Math.Cos(pitch);

            // Calculate heading
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
            Array.Clear(gravity, 0, gravity.Length);
            Array.Clear(magnetic, 0, magnetic.Length);
        }
    }
}
