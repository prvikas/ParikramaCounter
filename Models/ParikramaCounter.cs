using System;
using System.Collections.Generic;

namespace ParikramaCounter.Models
{
    public class ParikramaTracker
    {
        private readonly HeadingTracker headingTracker = new HeadingTracker();
        private readonly Queue<int> recentSteps = new Queue<int>(10);

        // Fix #5: boolean flag instead of == 0 guard, which breaks when step count
        // genuinely starts at zero or resets between parikramas.
        private bool hasSetStartSteps = false;
        private int stepsAtStart = 0;
        private int minimumStepsRequired = 30;

        // Milestone ranges (in degrees of CumulativeChange, not GetProgress %).
        // Using ranges rather than exact thresholds because a walking compass heading
        // accumulates unevenly — a person can cross or skip a single degree point
        // between sensor samples, but will always pass through a degree range.
        //
        // 3rd-side buzz:     fire once walker enters 250°–290° window
        // Approaching-start: fire once walker enters 320°–350° window
        // These are intentionally conservative: the lower bound is generous enough
        // to catch slow walkers; the upper bound stops the buzz firing again if
        // sensors briefly read above the window before completion.
        private const double THIRD_SIDE_MIN = 250.0;
        private const double THIRD_SIDE_MAX = 290.0;
        private const double APPROACHING_START_MIN = 320.0;
        private const double APPROACHING_START_MAX = 350.0;

        // Each quadrant (side) is 90° ± 15° tolerance.
        // SidesCompleted goes 0→1→2→3→4 as CumulativeChange crosses each boundary.
        private const double DEGREES_PER_SIDE = 90.0;

        public int ParikramaCount { get; private set; }
        public int TargetParikramaCount { get; set; } = 7;

        // Progress as 0–100 percentage for progress bar display
        public double CurrentProgress => headingTracker.GetProgress();

        public int CurrentStepsInCircle { get; private set; }

        // Fix #2: use CumulativeChange (degrees) divided by 90, clamped to 4.
        // Previously used GetProgress() (0–100%) / 90 which could only return 0 or 1.
        public int SidesCompleted => Math.Min(4, (int)(headingTracker.CumulativeChange / DEGREES_PER_SIDE));

        public bool IsTargetReached => ParikramaCount >= TargetParikramaCount;
        public int RemainingParikramas => Math.Max(0, TargetParikramaCount - ParikramaCount);
        public double ProgressPercentage => TargetParikramaCount > 0
            ? (double)ParikramaCount / TargetParikramaCount * 100
            : 0;

        // Fired once when walker enters the 3rd-side range (250°–290°)
        public event Action OnThirdSideCompleted;

        // Fired once when walker enters the approaching-start range (320°–350°)
        public event Action OnApproachingStart;

        private bool thirdSideFired = false;
        private bool approachingStartFired = false;

        public bool CheckAndUpdateParikrama(double currentHeading, int totalSteps, bool isMoving, DateTime timestamp)
        {
            // Only process heading when walker is actually moving
            if (!isMoving)
                return false;

            headingTracker.Update(currentHeading, timestamp);

            // Fix #1 & #3: use CumulativeChange (degrees) for milestone comparisons,
            // not GetProgress() (%) which was compared against degree-valued thresholds.
            // Fix: use ranges, not exact points — a sensor tick can skip over a single degree.
            double cumulative = headingTracker.CumulativeChange;

            if (!thirdSideFired && cumulative >= THIRD_SIDE_MIN && cumulative <= THIRD_SIDE_MAX)
            {
                thirdSideFired = true;
                OnThirdSideCompleted?.Invoke();
            }

            if (!approachingStartFired && cumulative >= APPROACHING_START_MIN && cumulative <= APPROACHING_START_MAX)
            {
                approachingStartFired = true;
                OnApproachingStart?.Invoke();
            }

            // Fix #5: use boolean flag so step baseline is set correctly even when
            // totalSteps starts at 0, and correctly resets between parikramas.
            if (!hasSetStartSteps)
            {
                stepsAtStart = totalSteps;
                hasSetStartSteps = true;
            }
            CurrentStepsInCircle = totalSteps - stepsAtStart;

            // Track rolling window of cumulative step counts for consistency check
            recentSteps.Enqueue(totalSteps);
            if (recentSteps.Count > 10)
                recentSteps.Dequeue();

            // Check for full rotation — threshold is 340° with no upper bound.
            // The accumulator is unbounded; ResetCircle() fires immediately after detection.
            if (headingTracker.HasCompletedFullRotation())
            {
                if (IsValidParikrama())
                {
                    ParikramaCount++;
                    ResetCircle();
                    return true;
                }
                else
                {
                    // Rotation detected but not a valid walk (spinning, too few steps, etc.)
                    ResetCircle();
                    return false;
                }
            }

            return false;
        }

        private bool IsValidParikrama()
        {
            // Must have walked a minimum number of steps
            if (CurrentStepsInCircle < minimumStepsRequired)
                return false;

            // Check that steps were distributed across the walk (not all at once).
            // Fix: allow up to 3 consecutive samples with no new steps — a walker
            // can briefly pause mid-circle without the whole parikrama being invalidated.
            // The original stepDelta < 1 on every sample was too strict.
            if (recentSteps.Count >= 5)
            {
                var stepsList = new List<int>(recentSteps);
                int consecutiveIdle = 0;
                const int maxConsecutiveIdle = 3;

                for (int i = 1; i < stepsList.Count; i++)
                {
                    int stepDelta = stepsList[i] - stepsList[i - 1];
                    if (stepDelta < 1)
                    {
                        consecutiveIdle++;
                        if (consecutiveIdle > maxConsecutiveIdle)
                            return false;
                    }
                    else
                    {
                        consecutiveIdle = 0;
                    }
                }
            }

            // Lowered from 50 → 30: real Parikrama paths are not perfect circles;
            // GPS/magnetometer noise in open courtyards degrades confidence.
            // 30 still blocks erratic spinning or direction reversals.
            if (headingTracker.DirectionConfidence < 30)
                return false;

            return true;
        }

        private void ResetCircle()
        {
            headingTracker.Reset();
            stepsAtStart = 0;
            hasSetStartSteps = false;  // Fix #5: reset the flag too
            CurrentStepsInCircle = 0;
            recentSteps.Clear();
            thirdSideFired = false;
            approachingStartFired = false;
        }

        public void Reset()
        {
            ParikramaCount = 0;
            ResetCircle();
        }

        public string GetDirection()
        {
            if (headingTracker.IsClockwise)
                return "Clockwise ↻";
            else if (headingTracker.IsCounterClockwise)
                return "Counter-Clockwise ↺";
            else
                return "Determining...";
        }
    }
}
