using System;

namespace ParikramaCounter.Models
{
    public class SensorData
    {
        public double AccelX { get; set; }
        public double AccelY { get; set; }
        public double AccelZ { get; set; }
        public double GyroX { get; set; }
        public double GyroY { get; set; }
        public double GyroZ { get; set; }
        public double MagX { get; set; }
        public double MagY { get; set; }
        public double MagZ { get; set; }
        public double Heading { get; set; }
        public double TrueHeading { get; set; }
        public string Direction { get; set; }
        public int Steps { get; set; }
        public double Distance { get; set; }
        public double StrideLength { get; set; }
        public double WalkingFrequency { get; set; }
        public double AccelerationMagnitude { get; set; }
        public int ParikramaCount { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
