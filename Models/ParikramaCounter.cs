using System;
using System.Collections.Generic;

namespace ParikramaCounter.Models
{
    public class ParikramaTracker
    {
        private readonly HeadingTracker headingTracker = new HeadingTracker();
        private readonly Queue<int> recentSteps = new Queue<int>(10);

        private int stepsAtStart = 0;
        private int minimumStepsRequired = 30; // Minimum steps for valid parikrama
        private DateTime lastValidationTime = DateTime.Now;

        public int ParikramaCount { get; private set; }
        public int TargetParikramaCount { get; set; } = 7;
        public double CurrentProgress => headingTracker.GetProgress();
        public int CurrentStepsInCircle { get; private set; }

        // SidesCompleted: 0-4 quadrant tracking based on heading progress
        public int SidesCompleted => (int)(headingTracker.GetProgress() / 90.0);

        public bool IsTargetReached => ParikramaCount >= TargetParikramaCount;
        public int RemainingParikramas => Math.Max(0, TargetParikramaCount - ParikramaCount);
        public double ProgressPercentage => TargetParikramaCount > 0
            ? (double)ParikramaCount / TargetParikramaCount * 100
            : 0;

        // Fired when the walker completes the 3rd side (270° mark)
        public event Action OnThirdSideCompleted;

        // Fired when the walker is within ~30° of completing the circle
        public event Action OnApproachingStart;

        private bool thirdSideFired = false;
        private bool approachingStartFired = false;

        public bool CheckAndUpdateParikrama(double currentHeading, int totalSteps, bool isMoving, DateTime timestamp)
        {
            // Only update when actually moving
            if (!isMoving)
                return false;

            // Update heading tracker
            headingTracker.Update(currentHeading, timestamp);

            // Fire vibration events at meaningful progress milestones
            double progress = headingTracker.GetProgress();
            if (!thirdSideFired && progress >= 270.0)
            {
                thirdSideFired = true;
                OnThirdSideCompleted?.Invoke();
            }
            if (!approachingStartFired && progress >= 330.0)
            {
                approachingStartFired = true;
                OnApproachingStart?.Invoke();
            }

            // Track steps in current circle
            if (stepsAtStart == 0)
            {
                stepsAtStart = totalSteps;
            }
            CurrentStepsInCircle = totalSteps - stepsAtStart;

            // Validate movement pattern
            recentSteps.Enqueue(totalSteps);
            if (recentSteps.Count > 10)
                recentSteps.Dequeue();

            // Check if completed a full 360° rotation
            if (headingTracker.HasCompletedFullRotation())
            {
                // Validate it's a real parikrama
                if (IsValidParikrama())
                {
                    ParikramaCount++;
                    ResetCircle();
                    return true; // Trigger vibration
                }
                else
                {
                    // Invalid parikrama (e.g., spinning in place)
                    ResetCircle();
                    return false;
                }
            }

            return false;
        }

        private bool IsValidParikrama()
        {
            // Must have walked minimum steps
            if (CurrentStepsInCircle < minimumStepsRequired)
                return false;

            // Check if steps are distributed over time (not all at once)
            if (recentSteps.Count >= 5)
            {
                var stepsList = new List<int>(recentSteps);
                bool hasConsistentMovement = true;

                for (int i = 1; i < stepsList.Count; i++)
                {
                    int stepDelta = stepsList[i] - stepsList[i - 1];
                    // Should have some steps in each interval
                    if (stepDelta < 1)
                    {
                        hasConsistentMovement = false;
                        break;
                    }
                }

                if (!hasConsistentMovement)
                    return false;
            }

            // Must have reasonable direction confidence
            if (headingTracker.DirectionConfidence < 50)
                return false;

            return true;
        }

        private void ResetCircle()
        {
            headingTracker.Reset();
            stepsAtStart = 0;
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
