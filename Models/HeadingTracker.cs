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

        // Full rotation threshold: >= 340° (no upper bound).
        // cumulativeHeadingChange is an unbounded accumulator of heading deltas —
        // it does NOT wrap at 360°. The 0°/360° compass wrap is already corrected
        // per-tick in Update(), so the accumulator climbs continuously: 180, 270, 362...
        // There is no ceiling to enforce — ResetCircle() resets the accumulator
        // immediately after detection. The -20° tolerance covers real walkers who
        // drift slightly short due to magnetometer noise.
        private const double FULL_ROTATION_THRESHOLD = 340.0;

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
        /// Returns true once cumulative heading change reaches FULL_ROTATION_THRESHOLD (340°).
        /// No upper bound — the accumulator is unbounded and ResetCircle() fires immediately
        /// after a valid detection, so overcounting is not possible.
        /// </summary>
        public bool HasCompletedFullRotation()
        {
            return Math.Abs(cumulativeHeadingChange) >= FULL_ROTATION_THRESHOLD;
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
