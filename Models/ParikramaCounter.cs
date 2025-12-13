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

        private readonly DirectionalTracker directionalTracker = new DirectionalTracker();
        private TempleProfile currentProfile;

        public int ParikramaCount { get; private set; }
        public int TargetParikramaCount { get; set; } = 7;
        public double CircleProgress => Math.Min(100.0, Math.Abs(totalRotation) / 360.0 * 100.0);
        public double CurrentDistanceInCircle => directionalTracker.GetTotalDistance();

        public bool IsTargetReached => ParikramaCount >= TargetParikramaCount;
        public int RemainingParikramas => Math.Max(0, TargetParikramaCount - ParikramaCount);

        public DirectionalTracker DirectionalData => directionalTracker;
        public TempleProfile CurrentProfile => currentProfile;

        public void SetTempleProfile(TempleProfile profile)
        {
            currentProfile = profile;
            if (profile != null && profile.RecommendedParikramas > 0)
            {
                TargetParikramaCount = profile.RecommendedParikramas;
            }
            System.Diagnostics.Debug.WriteLine($"🕉️ Profile set: {profile?.Name ?? "None"}");

            // Show what we know about this temple
            if (profile != null && profile.TotalParikramasCompleted > 0)
            {
                System.Diagnostics.Debug.WriteLine($"   Known shape: {profile.DetectedShape}");
                System.Diagnostics.Debug.WriteLine($"   Avg circumference: {profile.AverageCircumference:F1}m");
                System.Diagnostics.Debug.WriteLine($"   Avg duration: {profile.AverageDuration:F1}s");
                System.Diagnostics.Debug.WriteLine($"   Accuracy: {profile.Accuracy:F0}%");
            }
        }

        public void StartTracking(double calibratedHeading)
        {
            if (startHeading == null)
            {
                startHeading = calibratedHeading;
                lastHeading = calibratedHeading;
                isActive = true;
                startTime = DateTime.Now;
                directionalTracker.Reset();
                System.Diagnostics.Debug.WriteLine($"🎯 TRACKING STARTED: {calibratedHeading:F1}°");
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

            directionalTracker.Update(currentHeading, isMoving);

            if (startHeading == null)
            {
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
                System.Diagnostics.Debug.WriteLine($"📐 {CircleProgress:F0}% | {totalRotation:F1}° | {CurrentDistanceInCircle:F1}m | {directionalTracker.GetCoveredDirectionCount()}/8");
            }

            if (absRotation >= 340 && absRotation <= 400)
            {
                double duration = (DateTime.Now - startTime).TotalSeconds;

                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine($"🔴 CIRCLE COMPLETE:");
                System.Diagnostics.Debug.WriteLine($"   Rotation: {totalRotation:F1}°");
                System.Diagnostics.Debug.WriteLine($"   Distance: {CurrentDistanceInCircle:F1}m");
                System.Diagnostics.Debug.WriteLine($"   Duration: {duration:F1}s");

                directionalTracker.LogDirectionalStats();

                // CHANGED: Use intelligent validation
                if (IsValidParikrama(duration))
                {
                    ParikramaCount++;
                    System.Diagnostics.Debug.WriteLine($"✅ PARIKRAMA #{ParikramaCount} COUNTED!");
                    System.Diagnostics.Debug.WriteLine("========================================");

                    ResetCircle();
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ INVALID");
                    System.Diagnostics.Debug.WriteLine("========================================");

                    if (absRotation > 380 || duration > 120)
                    {
                        ResetCircle();
                    }

                    return false;
                }
            }

            if (absRotation > 420)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ OVERSHOOT! Resetting");
                ResetCircle();
            }

            return false;
        }

        private bool IsValidParikrama(double duration)
        {
            // Basic validations (always apply)
            if (duration < 8)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Too fast: {duration:F1}s");
                return false;
            }

            if (duration > 300)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Too slow: {duration:F1}s");
                return false;
            }

            if (movementCount < 15)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Too few movements: {movementCount}");
                return false;
            }

            // INTELLIGENT VALIDATION: Use saved temple data if available
            if (currentProfile != null && currentProfile.TotalParikramasCompleted >= 3)
            {
                return ValidateWithIntelligence(duration);
            }
            else
            {
                // Fallback: Generic validation for new/unknown temples
                return ValidateGeneric(duration);
            }
        }

        private bool ValidateWithIntelligence(double duration)
        {
            System.Diagnostics.Debug.WriteLine($"🧠 INTELLIGENT VALIDATION (Accuracy: {currentProfile.Accuracy:F0}%)");

            // 1. Check distance against learned average
            double expectedDistance = currentProfile.AverageCircumference;
            double distanceTolerance = Math.Max(5.0, expectedDistance * 0.15); // ±15% or min 5m

            if (CurrentDistanceInCircle < expectedDistance - distanceTolerance)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Too short: {CurrentDistanceInCircle:F1}m (expected {expectedDistance:F1}m ±{distanceTolerance:F1}m)");
                return false;
            }

            if (CurrentDistanceInCircle > expectedDistance + distanceTolerance)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Too far: {CurrentDistanceInCircle:F1}m (expected {expectedDistance:F1}m ±{distanceTolerance:F1}m)");
                return false;
            }

            // 2. Check duration against learned average
            double expectedDuration = currentProfile.AverageDuration;
            double durationTolerance = Math.Max(10.0, expectedDuration * 0.25); // ±25% or min 10s

            if (Math.Abs(duration - expectedDuration) > durationTolerance)
            {
                System.Diagnostics.Debug.WriteLine($"   ⚠️ Unusual duration: {duration:F1}s (expected {expectedDuration:F1}s ±{durationTolerance:F1}s) - but allowing");
                // Don't fail on duration alone, just warn
            }

            // 3. Use saved shape for validation (DON'T recalculate!)
            var savedShape = Enum.TryParse<DirectionalTracker.PathShape>(currentProfile.DetectedShape, out var shape)
                ? shape
                : DirectionalTracker.PathShape.Unknown;

            System.Diagnostics.Debug.WriteLine($"   Using saved shape: {savedShape}");

            bool shapeValid = savedShape switch
            {
                DirectionalTracker.PathShape.Square or DirectionalTracker.PathShape.Rectangle
                    => ValidateSquareWithLearning(),
                DirectionalTracker.PathShape.Circle
                    => ValidateCircleWithLearning(),
                _ => directionalTracker.IsValidPath() // Fallback to detection
            };

            if (!shapeValid)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Shape validation failed");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"   ✅ Valid (matches learned pattern)");
            return true;
        }

        private bool ValidateSquareWithLearning()
        {
            // Use saved directional distances if available
            if (currentProfile.DirectionalDistances.Count >= 4)
            {
                double expectedN = currentProfile.DirectionalDistances.GetValueOrDefault("North", 0);
                double expectedE = currentProfile.DirectionalDistances.GetValueOrDefault("East", 0);
                double expectedS = currentProfile.DirectionalDistances.GetValueOrDefault("South", 0);
                double expectedW = currentProfile.DirectionalDistances.GetValueOrDefault("West", 0);

                double currentN = directionalTracker.GetDistanceInDirection(DirectionalTracker.Direction.North);
                double currentE = directionalTracker.GetDistanceInDirection(DirectionalTracker.Direction.East);
                double currentS = directionalTracker.GetDistanceInDirection(DirectionalTracker.Direction.South);
                double currentW = directionalTracker.GetDistanceInDirection(DirectionalTracker.Direction.West);

                // Check if current matches expected pattern (±50% tolerance)
                bool nOk = currentN >= expectedN * 0.5 && currentN <= expectedN * 1.5;
                bool eOk = currentE >= expectedE * 0.5 && currentE <= expectedE * 1.5;
                bool sOk = currentS >= expectedS * 0.5 && currentS <= expectedS * 1.5;
                bool wOk = currentW >= expectedW * 0.5 && currentW <= expectedW * 1.5;

                int matches = (nOk ? 1 : 0) + (eOk ? 1 : 0) + (sOk ? 1 : 0) + (wOk ? 1 : 0);

                System.Diagnostics.Debug.WriteLine($"   Directional match: {matches}/4 directions match learned pattern");

                return matches >= 3; // At least 3 out of 4 must match
            }

            // Fallback to generic square validation
            return directionalTracker.GetDistanceInDirection(DirectionalTracker.Direction.North) >= 2.0 &&
                   directionalTracker.GetDistanceInDirection(DirectionalTracker.Direction.East) >= 2.0 &&
                   directionalTracker.GetDistanceInDirection(DirectionalTracker.Direction.South) >= 2.0 &&
                   directionalTracker.GetDistanceInDirection(DirectionalTracker.Direction.West) >= 2.0;
        }

        private bool ValidateCircleWithLearning()
        {
            // For circles, check that all 8 directions match learned pattern
            if (currentProfile.DirectionalDistances.Count >= 6)
            {
                int matchingDirections = 0;
                int totalChecked = 0;

                foreach (var kvp in currentProfile.DirectionalDistances)
                {
                    if (Enum.TryParse<DirectionalTracker.Direction>(kvp.Key, out var dir))
                    {
                        double expected = kvp.Value;
                        double current = directionalTracker.GetDistanceInDirection(dir);

                        if (current >= expected * 0.4 && current <= expected * 1.6) // ±60% tolerance
                        {
                            matchingDirections++;
                        }
                        totalChecked++;
                    }
                }

                double matchRate = (double)matchingDirections / totalChecked;
                System.Diagnostics.Debug.WriteLine($"   Directional match: {matchingDirections}/{totalChecked} ({matchRate:P0})");

                return matchRate >= 0.75; // 75% of directions must match
            }

            // Fallback
            return directionalTracker.GetCoveredDirectionCount() >= 6;
        }

        private bool ValidateGeneric(double duration)
        {
            System.Diagnostics.Debug.WriteLine($"📝 GENERIC VALIDATION (learning mode)");

            if (CurrentDistanceInCircle < 12.0)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Too short: {CurrentDistanceInCircle:F1}m");
                return false;
            }

            if (CurrentDistanceInCircle > 100.0)
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Too far: {CurrentDistanceInCircle:F1}m");
                return false;
            }

            if (!directionalTracker.IsValidPath())
            {
                System.Diagnostics.Debug.WriteLine($"   ❌ Invalid path shape");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"   ✅ Valid (generic rules)");
            return true;
        }

        private void ResetCircle()
        {
            System.Diagnostics.Debug.WriteLine($"🔄 RESETTING");

            startHeading = null;
            totalRotation = 0;
            rotationDirection = 0;
            isActive = false;
            movementCount = 0;
            lastProgressMilestone = 0;
            nearCompletion = false;
            directionalTracker.Reset();
        }

        public void Reset()
        {
            ParikramaCount = 0;
            ResetCircle();
            System.Diagnostics.Debug.WriteLine("🔄 FULL RESET");
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
        }

        public double GetDuration()
        {
            return (DateTime.Now - startTime).TotalSeconds;
        }
    }
}
