using System;

namespace ParikramaCounter.Models
{
    public class HeadingTracker
    {
        private double previousHeading = -1;
        private double cumulativeHeadingChange = 0;
        private int direction = 0; // 1 = clockwise, -1 = counter-clockwise, 0 = undetermined
        private const double HEADING_WRAP_THRESHOLD = 180.0;
        private const double MIN_DIRECTION_CONFIDENCE = 45.0; // Degrees to determine direction
        private DateTime lastUpdateTime = DateTime.MinValue;
        private const int MAX_TIME_GAP_MS = 5000; // Reset if no update for 5 seconds

        public double CumulativeChange => Math.Abs(cumulativeHeadingChange);
        public bool IsClockwise => direction > 0;
        public bool IsCounterClockwise => direction < 0;
        public int DirectionConfidence { get; private set; }

        public void Update(double currentHeading, DateTime timestamp)
        {
            // Initialize on first reading
            if (previousHeading < 0)
            {
                previousHeading = currentHeading;
                lastUpdateTime = timestamp;
                return;
            }

            // Reset if too much time has passed (stopped moving)
            if ((timestamp - lastUpdateTime).TotalMilliseconds > MAX_TIME_GAP_MS)
            {
                cumulativeHeadingChange = 0;
                direction = 0;
                DirectionConfidence = 0;
            }

            lastUpdateTime = timestamp;

            // Calculate heading change, accounting for 360° wrap-around
            double headingDelta = currentHeading - previousHeading;

            // Handle wrap-around at 0°/360° boundary
            if (headingDelta > HEADING_WRAP_THRESHOLD)
            {
                headingDelta -= 360.0;
            }
            else if (headingDelta < -HEADING_WRAP_THRESHOLD)
            {
                headingDelta += 360.0;
            }

            // Accumulate heading change
            cumulativeHeadingChange += headingDelta;

            // Determine direction once we have confidence
            if (direction == 0 && Math.Abs(cumulativeHeadingChange) > MIN_DIRECTION_CONFIDENCE)
            {
                direction = cumulativeHeadingChange > 0 ? 1 : -1;
                DirectionConfidence = 100;
            }

            // Update direction confidence
            if (direction != 0)
            {
                double expectedSign = direction > 0 ? 1 : -1;
                double actualSign = Math.Sign(headingDelta);

                if (expectedSign == actualSign)
                {
                    DirectionConfidence = Math.Min(100, DirectionConfidence + 5);
                }
                else
                {
                    DirectionConfidence = Math.Max(0, DirectionConfidence - 10);

                    // Reset if direction confidence lost
                    if (DirectionConfidence < 20)
                    {
                        Reset();
                    }
                }
            }

            previousHeading = currentHeading;
        }

        public bool HasCompletedFullRotation()
        {
            return Math.Abs(cumulativeHeadingChange) >= 360.0;
        }

        public double GetProgress()
        {
            return Math.Min(100.0, (Math.Abs(cumulativeHeadingChange) / 360.0) * 100.0);
        }

        public void Reset()
        {
            cumulativeHeadingChange = 0;
            direction = 0;
            DirectionConfidence = 0;
            // Keep previousHeading to maintain continuity
        }
    }
}
