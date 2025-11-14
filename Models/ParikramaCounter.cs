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
        private bool nearCompletion = false;

        public int ParikramaCount { get; private set; }
        public int TargetParikramaCount { get; set; } = 7;
        public double CircleProgress => Math.Min(100.0, Math.Abs(totalRotation) / 360.0 * 100.0);
        public double CurrentDistanceInCircle { get; private set; } = 0;

        public bool IsTargetReached => ParikramaCount >= TargetParikramaCount;
        public int RemainingParikramas => Math.Max(0, TargetParikramaCount - ParikramaCount);

        // ADDED: Initialize with calibrated heading
        public void StartTracking(double calibratedHeading)
        {
            if (startHeading == null)
            {
                startHeading = calibratedHeading;
                lastHeading = calibratedHeading;
                isActive = true;
                startTime = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"🎯 TRACKING STARTED: {calibratedHeading:F1}° (pre-calibrated)");
            }
        }

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

            // If not started yet, can't track
            if (startHeading == null)
            {
                return false;
            }

            double delta = currentHeading - lastHeading;

            if (delta > 180) delta -= 360;
            if (delta < -180) delta += 360;

            // Ignore tiny changes
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

            if (absRotation >= 320 && !nearCompletion)
            {
                nearCompletion = true;
                System.Diagnostics.Debug.WriteLine($"⚠️ APPROACHING COMPLETION: {absRotation:F1}°");
            }

            int currentMilestone = (int)(CircleProgress / 10);
            if (currentMilestone > lastProgressMilestone)
            {
                lastProgressMilestone = currentMilestone;
                System.Diagnostics.Debug.WriteLine($"📐 Progress: {CircleProgress:F0}% | Total: {totalRotation:F1}° | Distance: {CurrentDistanceInCircle:F1}m");
            }

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

                    if (absRotation > 380 || duration > 120)
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

            if (absRotation > 420)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ OVERSHOOT! Resetting at {absRotation:F1}°");
                ResetCircle();
            }

            return false;
        }

        private bool IsValidParikrama(double duration)
        {
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

            if (movementCount < 15)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Too few movements: {movementCount} < 15");
                return false;
            }

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

            System.Diagnostics.Debug.WriteLine($"   ✅ All validations passed!");
            return true;
        }

        private void ResetCircle()
        {
            System.Diagnostics.Debug.WriteLine($"🔄 RESETTING: {totalRotation:F1}° → 0°");

            startHeading = null; // Allow re-initialization with new calibrated heading
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
    }
}
