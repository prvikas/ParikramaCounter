using System;

namespace ParikramaCounter.Models
{
    public class HeadingTracker
    {
        private double previousHeading = -1;
        private double cumulativeRotation = 0;
        private DateTime lastUpdate = DateTime.MinValue;
        private bool isInitialized = false;  // NEW: Track if we've started

        private const double MIN_ROTATION_DEGREES = 340.0;
        private const double MAX_ROTATION_DEGREES = 380.0;
        private const double MIN_HEADING_CHANGE = 0.3;

        public double CurrentHeading { get; private set; }
        public double CumulativeChange => Math.Abs(cumulativeRotation);
        public bool IsClockwise => cumulativeRotation > 0;
        public bool IsCounterClockwise => cumulativeRotation < 0;
        public int DirectionConfidence { get; private set; } = 0;

        public void Update(double currentHeading, bool isMoving, DateTime timestamp)
        {
            // FIXED: If not moving, just update current heading but don't process
            if (!isMoving)
            {
                CurrentHeading = currentHeading;
                // Don't update previousHeading - keep it for when movement resumes
                return;
            }

            // Initialize on first movement
            if (!isInitialized)
            {
                previousHeading = currentHeading;
                CurrentHeading = currentHeading;
                lastUpdate = timestamp;
                isInitialized = true;
                System.Diagnostics.Debug.WriteLine($"📍 START HEADING LOCKED: {currentHeading:F1}°");
                return;
            }

            double deltaTime = (timestamp - lastUpdate).TotalSeconds;
            if (deltaTime <= 0 || deltaTime > 2.0)
            {
                lastUpdate = timestamp;
                return;
            }

            // Calculate heading change with wrap-around
            double headingDelta = currentHeading - previousHeading;

            if (headingDelta > 180)
                headingDelta -= 360;
            else if (headingDelta < -180)
                headingDelta += 360;

            // Ignore very small changes (noise)
            if (Math.Abs(headingDelta) < MIN_HEADING_CHANGE)
            {
                previousHeading = currentHeading;
                CurrentHeading = currentHeading;
                lastUpdate = timestamp;
                return;
            }

            // Accumulate rotation
            cumulativeRotation += headingDelta;
            CurrentHeading = currentHeading;

            // Update direction confidence
            UpdateDirectionConfidence(headingDelta);

            previousHeading = currentHeading;
            lastUpdate = timestamp;

            System.Diagnostics.Debug.WriteLine($"Heading: {currentHeading:F1}° | Delta: {headingDelta:F2}° | Cumulative: {cumulativeRotation:F1}° | Confidence: {DirectionConfidence}");
        }

        private void UpdateDirectionConfidence(double deltaHeading)
        {
            if (Math.Abs(cumulativeRotation) > 45 && DirectionConfidence == 0)
            {
                DirectionConfidence = 80;
            }

            if (DirectionConfidence > 0)
            {
                double expectedSign = cumulativeRotation > 0 ? 1 : -1;
                double actualSign = Math.Sign(deltaHeading);

                if (expectedSign == actualSign)
                {
                    DirectionConfidence = Math.Min(100, DirectionConfidence + 2);
                }
                else if (Math.Abs(deltaHeading) < 1.0)
                {
                    // Small noise - don't penalize
                }
                else
                {
                    DirectionConfidence = Math.Max(0, DirectionConfidence - 8);
                }
            }
        }

        public bool HasCompletedFullRotation()
        {
            double absChange = Math.Abs(cumulativeRotation);
            bool completed = absChange >= MIN_ROTATION_DEGREES && absChange <= MAX_ROTATION_DEGREES;

            if (completed)
            {
                System.Diagnostics.Debug.WriteLine($"✅ 360° ROTATION COMPLETED: {absChange:F1}°");
            }

            return completed;
        }

        public double GetProgress()
        {
            return Math.Min(100.0, (Math.Abs(cumulativeRotation) / 360.0) * 100.0);
        }

        public double GetRotationQuality()
        {
            double absChange = Math.Abs(cumulativeRotation);
            double deviation = Math.Abs(absChange - 360.0);
            return Math.Max(0, 100.0 - (deviation * 5.0));
        }

        public void Reset()
        {
            cumulativeRotation = 0;
            DirectionConfidence = 0;
            isInitialized = false;  // FIXED: Allow new start position
            System.Diagnostics.Debug.WriteLine("🔄 HeadingTracker Reset - Ready for new circle");
        }
    }
}
