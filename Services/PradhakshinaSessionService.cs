using System;
using System.Threading.Tasks;
using ParikramaCounter.Models;
using ParikramaCounter.Repositories;

namespace ParikramaCounter.Services
{
    // Fix #5 (TrackingViewModel responsibility split):
    // Owns the active pradhakshina session — count, target, mode, completion detection,
    // history saving. TrackingViewModel becomes a thin UI adapter over this service.
    public class PradhakshinaSessionService
    {
        private readonly IAppPreferences    prefs;
        private readonly IVibrationService  vibration;
        private readonly ISessionRepository repository;
        private readonly ParikramaTracker   tracker = new ParikramaTracker();

        private SessionRecord? activeSession;
        private bool isTracking;

        // ── Events ────────────────────────────────────────────────────────────────

        public event Action<int>? CountChanged;           // new count value
        public event Action?      TargetReached;          // target hit
        public event Action?      ThirdSideCompleted;     // 3rd side buzz trigger
        public event Action?      ApproachingStart;       // approaching start trigger

        // ── State ─────────────────────────────────────────────────────────────────

        public int  Count          => tracker.ParikramaCount;
        public int  Target         => prefs.TargetParikrama;
        public bool IsTargetReached => tracker.IsTargetReached;
        public bool IsTracking     => isTracking;

        // Tracker read-through for display
        public double CurrentProgress      => tracker.CurrentProgress;
        public int    SidesCompleted       => tracker.SidesCompleted;
        public int    CurrentStepsInCircle => tracker.CurrentStepsInCircle;
        public string GetDirection()       => tracker.GetDirection();

        public PradhakshinaSessionService(
            IAppPreferences    prefs,
            IVibrationService  vibration,
            ISessionRepository repository)
        {
            this.prefs      = prefs      ?? throw new ArgumentNullException(nameof(prefs));
            this.vibration  = vibration  ?? throw new ArgumentNullException(nameof(vibration));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));

            tracker.TargetParikramaCount  = prefs.TargetParikrama;
            tracker.OnThirdSideCompleted += () => { ThirdSideCompleted?.Invoke(); vibration.VibrateThirdSide(); };
            tracker.OnApproachingStart   += () => { ApproachingStart?.Invoke();   vibration.VibrateApproachingStart(); };

            // Restore persisted count
            tracker.ManualSetCount(prefs.ParikramaCount);
        }

        // ── Session control ───────────────────────────────────────────────────────

        public void StartTracking()
        {
            if (isTracking) return;
            isTracking    = true;
            activeSession = new SessionRecord
            {
                Target    = prefs.TargetParikrama,
                StartedAt = DateTime.UtcNow
            };
        }

        public async Task StopTrackingAsync(int totalSteps)
        {
            if (!isTracking) return;
            isTracking = false;

            if (activeSession != null)
            {
                activeSession.CompletedAt   = DateTime.UtcNow;
                activeSession.CountCompleted = tracker.ParikramaCount;
                activeSession.TotalSteps     = totalSteps;
                await repository.SaveAsync(activeSession);
                activeSession = null;
            }
        }

        // ── Auto-detection via sensor data ────────────────────────────────────────

        public void ProcessSensorData(double heading, int steps, bool isMoving, DateTime timestamp)
        {
            if (!isTracking || !prefs.AutoCountingEnabled) return;

            bool completed = tracker.CheckAndUpdateParikrama(heading, steps, isMoving, timestamp);
            if (!completed) return;

            int newCount = tracker.ParikramaCount;
            prefs.ParikramaCount = newCount;
            CountChanged?.Invoke(newCount);

            if (tracker.IsTargetReached)
            {
                TargetReached?.Invoke();
                _ = vibration.VibrateTargetReachedAsync();
            }
            else
            {
                vibration.VibrateCompletion();
            }
        }

        // ── Manual counting ───────────────────────────────────────────────────────

        public async Task ManualIncrementAsync(int totalSteps)
        {
            int cap = Math.Max(Target * 2, Target + 10);
            if (tracker.ParikramaCount >= cap) return;

            tracker.ManualSetCount(tracker.ParikramaCount + 1);
            prefs.ParikramaCount = tracker.ParikramaCount;
            CountChanged?.Invoke(tracker.ParikramaCount);

            if (tracker.IsTargetReached)
            {
                TargetReached?.Invoke();
                await vibration.VibrateTargetReachedAsync();
            }
            else if (tracker.ParikramaCount > 1)
            {
                vibration.VibrateCompletion();
            }
        }

        public void ManualDecrement()
        {
            if (tracker.ParikramaCount <= 0) return;
            tracker.ManualSetCount(tracker.ParikramaCount - 1);
            prefs.ParikramaCount = tracker.ParikramaCount;
            CountChanged?.Invoke(tracker.ParikramaCount);
        }

        // ── Target management ─────────────────────────────────────────────────────

        public void SetTarget(int target)
        {
            prefs.TargetParikrama            = target;
            tracker.TargetParikramaCount     = target;
        }

        // ── Reset ─────────────────────────────────────────────────────────────────

        public async Task ResetAsync()
        {
            if (isTracking) await StopTrackingAsync(0);
            tracker.Reset();
            prefs.ParikramaCount = 0;
            CountChanged?.Invoke(0);
        }
    }
}
