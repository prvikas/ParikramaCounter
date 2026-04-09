using System;

namespace ParikramaCounter.Models
{
    public class HeadingTracker
    {
        private double previousHeading = -1;
        private double cumulativeHeadingChange = 0;
        private int direction = 0; // 1 = clockwise, -1 = counter-clockwise, 0 = undetermined
        private const double HEADING_WRAP_THRESHOLD = 180.0;
        private const double MIN_DIRECTION_CONFIDENCE = 45.0; // Degrees before direction is locked
        private DateTime lastUpdateTime = DateTime.MinValue;

        // Increased from 5s → 15s: walkers pause at prayer points, obstacles, crowds
        private const int MAX_TIME_GAP_MS = 15000;

        // A full rotation window: accept 340°–400° as a completed circle.
        // Too strict (exactly 360°) rejects walkers who drift slightly short.
        // Too loose (> 400°) would count someone who over-rotated or doubled back.
        private const double FULL_ROTATION_MIN = 340.0;
        private const double FULL_ROTATION_MAX = 400.0;

        public double CumulativeChange => Math.Abs(cumulativeHeadingChange);
        public bool IsClockwise => direction > 0;
        public bool IsCounterClockwise => direction < 0;
        public int DirectionConfidence { get; private set; }

        public void Update(double currentHeading, DateTime timestamp)
        {
            // Initialise on first reading
            if (previousHeading < 0)
            {
                previousHeading = currentHeading;
                lastUpdateTime = timestamp;
                return;
            }

            // If the walker paused beyond the gap threshold, reset accumulated change
            // but keep previousHeading so the next delta is computed from real position
            if ((timestamp - lastUpdateTime).TotalMilliseconds > MAX_TIME_GAP_MS)
            {
                cumulativeHeadingChange = 0;
                direction = 0;
                DirectionConfidence = 0;
            }

            lastUpdateTime = timestamp;

            // Calculate heading delta, handling the 0°/360° wrap-around
            double headingDelta = currentHeading - previousHeading;
            if (headingDelta > HEADING_WRAP_THRESHOLD)
                headingDelta -= 360.0;
            else if (headingDelta < -HEADING_WRAP_THRESHOLD)
                headingDelta += 360.0;

            cumulativeHeadingChange += headingDelta;

            // Lock direction once we have enough cumulative change for confidence
            if (direction == 0 && Math.Abs(cumulativeHeadingChange) > MIN_DIRECTION_CONFIDENCE)
            {
                direction = cumulativeHeadingChange > 0 ? 1 : -1;
                DirectionConfidence = 100;
            }

            // Maintain direction confidence: reward consistent ticks, penalise reversals
            if (direction != 0)
            {
                double expectedSign = direction > 0 ? 1 : -1;
                double actualSign = Math.Sign(headingDelta);

                if (actualSign != 0 && expectedSign == actualSign)
                {
                    DirectionConfidence = Math.Min(100, DirectionConfidence + 5);
                }
                else if (actualSign != 0)
                {
                    DirectionConfidence = Math.Max(0, DirectionConfidence - 10);

                    // Reset if confidence collapses — walker reversed significantly
                    if (DirectionConfidence < 20)
                        Reset();
                }
            }

            previousHeading = currentHeading;
        }

        /// <summary>
        /// Returns true when the cumulative angular change falls within the valid
        /// full-rotation window (340°–400°). Using a window rather than a point
        /// threshold accommodates real-world magnetometer drift and walking variation.
        /// </summary>
        public bool HasCompletedFullRotation()
        {
            double abs = Math.Abs(cumulativeHeadingChange);
            return abs >= FULL_ROTATION_MIN && abs <= FULL_ROTATION_MAX;
        }

        /// <summary>
        /// Returns progress as a percentage of a full 360° rotation, capped at 100%.
        /// Used for progress bar display only.
        /// </summary>
        public double GetProgress()
        {
            return Math.Min(100.0, (Math.Abs(cumulativeHeadingChange) / 360.0) * 100.0);
        }

        public void Reset()
        {
            cumulativeHeadingChange = 0;
            direction = 0;
            DirectionConfidence = 0;
            // Fix #6: reset previousHeading to -1 so next Update() initialises cleanly
            // and doesn't produce a phantom delta from a stale pre-reset heading.
            previousHeading = -1;
        }
    }
}
