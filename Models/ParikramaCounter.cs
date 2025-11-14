using System;

namespace ParikramaCounter.Models
{
    public class ParikramaTracker
    {
        private double? startHeading = null;
        private double lastHeading = 0;
        private double totalRotation = 0;
        private int rotationDirection = 0;

        private bool isActive = false;
        private DateTime startTime;
        private int movementCount = 0;
        private int debugCounter = 0;
        private int lastProgressMilestone = 0;

        // ADDED: Track if we're near completion to prevent false resets
        private bool nearCompletion = false;

        public int ParikramaCount { get; private set; }
        public int TargetParikramaCount { get; set; } = 7;
        public double CircleProgress => Math.Min(100.0, Math.Abs(totalRotation) / 360.0 * 100.0);
        public double CurrentDistanceInCircle { get; private set; } = 0;

        public bool IsTargetReached => ParikramaCount >= TargetParikramaCount;
        public int RemainingParikramas => Math.Max(0, TargetParikramaCount - ParikramaCount);

        public bool Update(double currentHeading, bool isMoving)
        {
            if (!isMoving)
            {
                debugCounter++;
                if (debugCounter % 100 == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"⏸️ PAUSED at {CircleProgress:F1}%");
                }
                return false;
            }

            debugCounter = 0;
            movementCount++;

            if (startHeading == null)
            {
                startHeading = currentHeading;
                lastHeading = currentHeading;
                isActive = true;
                startTime = DateTime.Now;
                nearCompletion = false; // RESET
                System.Diagnostics.Debug.WriteLine($"🎯 START: {currentHeading:F1}°");
                return false;
            }

            double delta = currentHeading - lastHeading;

            if (delta > 180) delta -= 360;
            if (delta < -180) delta += 360;

            if (Math.Abs(delta) < 0.3)
            {
                lastHeading = currentHeading;
                return false;
            }

            if (rotationDirection == 0 && Math.Abs(totalRotation) > 10)
            {
                rotationDirection = totalRotation > 0 ? 1 : -1;
                System.Diagnostics.Debug.WriteLine($"📍 Direction: {(rotationDirection > 0 ? "Clockwise" : "Counter-Clockwise")}");
            }

            totalRotation += delta;
            lastHeading = currentHeading;

            CurrentDistanceInCircle = Math.Abs(totalRotation) * 0.083;

            double absRotation = Math.Abs(totalRotation);

            // ADDED: Mark when near completion (90%+)
            if (absRotation >= 320 && !nearCompletion)
            {
                nearCompletion = true;
                System.Diagnostics.Debug.WriteLine($"⚠️ APPROACHING COMPLETION: {absRotation:F1}°");
            }

            // Log progress milestones
            int currentMilestone = (int)(CircleProgress / 10);
            if (currentMilestone > lastProgressMilestone)
            {
                lastProgressMilestone = currentMilestone;
                System.Diagnostics.Debug.WriteLine($"📐 Progress: {CircleProgress:F0}% | Total: {totalRotation:F1}° | Distance: {CurrentDistanceInCircle:F1}m");
            }

            // FIXED: Check completion in range 340-400 (allow overshoot to 400°)
            if (absRotation >= 340 && absRotation <= 400)
            {
                double duration = (DateTime.Now - startTime).TotalSeconds;

                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine($"🔴 CIRCLE COMPLETE:");
                System.Diagnostics.Debug.WriteLine($"   Rotation: {totalRotation:F1}°");
                System.Diagnostics.Debug.WriteLine($"   Abs Rotation: {absRotation:F1}°");
                System.Diagnostics.Debug.WriteLine($"   Distance: {CurrentDistanceInCircle:F1}m");
                System.Diagnostics.Debug.WriteLine($"   Duration: {duration:F1}s");
                System.Diagnostics.Debug.WriteLine($"   Movements: {movementCount}");
                System.Diagnostics.Debug.WriteLine($"   Direction: {GetDirection()}");

                // RELAXED validation
                if (IsValidParikrama(duration))
                {
                    ParikramaCount++;
                    System.Diagnostics.Debug.WriteLine($"✅ PARIKRAMA #{ParikramaCount} COUNTED!");
                    System.Diagnostics.Debug.WriteLine($"   Target: {TargetParikramaCount}");
                    System.Diagnostics.Debug.WriteLine($"   Remaining: {RemainingParikramas}");
                    System.Diagnostics.Debug.WriteLine("========================================");

                    ResetCircle();
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ INVALID (validation failed)");
                    System.Diagnostics.Debug.WriteLine("========================================");

                    // CHANGED: Don't reset immediately if near completion
                    // Give user a few more seconds to complete properly
                    if (absRotation > 380 || duration > 120) // Only reset if way over or timeout
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ FORCED RESET (overshoot or timeout)");
                        ResetCircle();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ KEEPING TRACKING (allowing retry)");
                    }

                    return false;
                }
            }

            // ADDED: Force reset if going way over 400° (prevent infinite accumulation)
            if (absRotation > 420)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ OVERSHOOT! Resetting at {absRotation:F1}°");
                ResetCircle();
            }

            return false;
        }

        private bool IsValidParikrama(double duration)
        {
            // RELAXED: Minimum duration from 10s to 8s
            if (duration < 8)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Too fast: {duration:F1}s < 8s");
                return false;
            }

            if (duration > 300)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Too slow: {duration:F1}s > 300s");
                return false;
            }

            // RELAXED: Minimum movements from 20 to 15
            if (movementCount < 15)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Too few movements: {movementCount} < 15");
                return false;
            }

            // RELAXED: Minimum distance from 15m to 12m
            if (CurrentDistanceInCircle < 12.0)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Too short: {CurrentDistanceInCircle:F1}m < 12m");
                return false;
            }

            if (CurrentDistanceInCircle > 100.0)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Too far: {CurrentDistanceInCircle:F1}m > 100m");
                return false;
            }

            // REMOVED: Direction confidence check (too strict)
            // if (rotationDirection == 0)
            // {
            //     System.Diagnostics.Debug.WriteLine($"   ❌ Direction not determined");
            //     return false;
            // }

            System.Diagnostics.Debug.WriteLine($"   ✅ All validations passed!");
            return true;
        }

        private void ResetCircle()
        {
            System.Diagnostics.Debug.WriteLine($"🔄 RESETTING: {totalRotation:F1}° → 0°");

            startHeading = null;
            totalRotation = 0;
            rotationDirection = 0;
            isActive = false;
            movementCount = 0;
            lastProgressMilestone = 0;
            CurrentDistanceInCircle = 0;
            nearCompletion = false;
        }

        public void Reset()
        {
            ParikramaCount = 0;
            ResetCircle();
            System.Diagnostics.Debug.WriteLine("🔄 FULL RESET - All counters cleared");
        }

        public string GetDirection()
        {
            if (rotationDirection > 0) return "Clockwise ↻";
            if (rotationDirection < 0) return "Counter-Clockwise ↺";
            return "Determining...";
        }

        public void SetTarget(int target)
        {
            TargetParikramaCount = Math.Max(1, target);
            System.Diagnostics.Debug.WriteLine($"🎯 Target set to: {TargetParikramaCount}");
        }

        public void ForceIncrement()
        {
            ParikramaCount++;
            System.Diagnostics.Debug.WriteLine($"⚡ FORCED INCREMENT: {ParikramaCount}");
        }
    }
}
