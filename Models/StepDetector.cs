using System;
using System.Collections.Generic;
using System.Linq;

namespace ParikramaCounter.Models
{
    public class StepDetector
    {
        private readonly Queue<double> accelerationHistory = new Queue<double>(50);
        private readonly Queue<DateTime> recentSteps = new Queue<DateTime>(10);

        private double dynamicThreshold = 1.5;
        private double lastPeakValue = 0;
        private DateTime lastStepTime = DateTime.MinValue;
        private readonly int minStepIntervalMs = 300;
        private readonly int maxStepIntervalMs = 2000;

        // Motion detection
        private double movingAverage = 0;
        private const double MOTION_THRESHOLD = 0.3; // Minimum movement to count steps

        public int StepCount { get; private set; }
        public double CurrentThreshold => dynamicThreshold;
        public bool IsMoving { get; private set; }

        public bool DetectStep(double accelerationMagnitude)
        {
            // Calculate moving average for motion detection
            movingAverage = 0.9 * movingAverage + 0.1 * accelerationMagnitude;

            // Add to history
            accelerationHistory.Enqueue(accelerationMagnitude);
            if (accelerationHistory.Count > 50)
                accelerationHistory.Dequeue();

            // Need minimum data points
            if (accelerationHistory.Count < 20)
                return false;

            // Check if actually moving (not just hand movements or noise)
            double variance = CalculateVariance();
            IsMoving = variance > MOTION_THRESHOLD;

            // Don't count steps if not moving
            if (!IsMoving)
            {
                lastPeakValue = 0;
                return false;
            }

            // Update dynamic threshold
            double mean = accelerationHistory.Average();
            double stdDev = Math.Sqrt(variance);
            dynamicThreshold = mean + (1.8 * stdDev); // Higher threshold for accuracy

            var now = DateTime.Now;

            // Reset on idle
            if ((now - lastStepTime).TotalMilliseconds > maxStepIntervalMs)
            {
                lastPeakValue = 0;
            }

            // Peak detection with stricter validation
            bool isPeak = accelerationMagnitude > dynamicThreshold &&
                         accelerationMagnitude > lastPeakValue * 1.1 && // 10% higher than last peak
                         (now - lastStepTime).TotalMilliseconds > minStepIntervalMs &&
                         IsValidStepPattern();

            if (isPeak)
            {
                lastStepTime = now;
                lastPeakValue = accelerationMagnitude;
                StepCount++;

                recentSteps.Enqueue(now);
                while (recentSteps.Count > 10)
                    recentSteps.Dequeue();

                return true;
            }

            // Decay peak value
            if ((now - lastStepTime).TotalMilliseconds > 600)
            {
                lastPeakValue *= 0.85;
            }

            return false;
        }

        private double CalculateVariance()
        {
            if (accelerationHistory.Count < 10)
                return 0;

            double mean = accelerationHistory.Average();
            return accelerationHistory.Average(v => Math.Pow(v - mean, 2));
        }

        private bool IsValidStepPattern()
        {
            // Check if recent steps follow a walking pattern (not random spikes)
            if (recentSteps.Count < 3)
                return true;

            var intervals = new List<double>();
            var stepList = recentSteps.ToList();
            for (int i = 1; i < stepList.Count; i++)
            {
                intervals.Add((stepList[i] - stepList[i - 1]).TotalMilliseconds);
            }

            // Walking has consistent intervals (250ms - 1000ms typically)
            double avgInterval = intervals.Average();
            return avgInterval >= 250 && avgInterval <= 1200;
        }

        public void Reset()
        {
            StepCount = 0;
            accelerationHistory.Clear();
            recentSteps.Clear();
            lastStepTime = DateTime.MinValue;
            lastPeakValue = 0;
            movingAverage = 0;
        }
    }
}
