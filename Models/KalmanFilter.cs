using System;

namespace ParikramaCounter.Models
{
    public class KalmanFilter
    {
        private double estimate;
        private double errorCovariance;
        private readonly double processNoise;
        private readonly double measurementNoise;

        public KalmanFilter(double processNoise = 0.001, double measurementNoise = 0.5)
        {
            this.processNoise = processNoise;
            this.measurementNoise = measurementNoise;
            this.estimate = 0;
            this.errorCovariance = 1;
        }

        public double Update(double measurement)
        {
            // Prediction
            errorCovariance += processNoise;

            // Update
            double kalmanGain = errorCovariance / (errorCovariance + measurementNoise);
            estimate += kalmanGain * (measurement - estimate);
            errorCovariance *= (1 - kalmanGain);

            return estimate;
        }

        public void Reset(double initialValue = 0)
        {
            estimate = initialValue;
            errorCovariance = 1;
        }
    }
}
