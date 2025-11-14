using System;

namespace ParikramaCounter.Models
{
    public class KalmanFilter
    {
        private double processNoise;
        private double measurementNoise;
        private double estimation;
        private double errorCovariance;
        private bool initialized = false;

        public KalmanFilter(double processNoise, double measurementNoise)
        {
            this.processNoise = processNoise;
            this.measurementNoise = measurementNoise;
            this.errorCovariance = 1.0;
        }

        public double Update(double measurement)
        {
            if (!initialized)
            {
                estimation = measurement;
                initialized = true;
                return estimation;
            }

            // Handle 360° wrap-around for heading
            double diff = measurement - estimation;
            if (diff > 180) diff -= 360;
            if (diff < -180) diff += 360;
            measurement = estimation + diff;

            // Prediction
            double predictionError = errorCovariance + processNoise;

            // Update
            double kalmanGain = predictionError / (predictionError + measurementNoise);
            estimation = estimation + kalmanGain * (measurement - estimation);
            errorCovariance = (1 - kalmanGain) * predictionError;

            // Normalize to 0-360
            while (estimation < 0) estimation += 360;
            while (estimation >= 360) estimation -= 360;

            return estimation;
        }

        public void Reset()
        {
            initialized = false;
            errorCovariance = 1.0;
        }
    }
}