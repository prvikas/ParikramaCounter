using System;
using System.Threading.Tasks;
using ParikramaCounter.Models;
using ParikramaCounter.Repositories;

namespace ParikramaCounter.Services
{
    // Issue #3: implements IPradhakshinaSessionService.
    // Issue #1 (state sync): this service is the single source of truth for
    //   count and target. TrackingViewModel holds NO duplicate backing fields for
    //   these values — it reads them directly from this service via the interface.
    // Issue #5 (battery): switches sensor to high-rate on StartTracking,
    //   back to idle rate on StopTracking.
    public class PradhakshinaSessionService : IPradhakshinaSessionService
    {
        private readonly IAppPreferences          prefs;
        private readonly IVibrationService        vibration;
        private readonly ISessionRepository       repository;
        private readonly ISensorLifecycleService  lifecycle;
        private readonly ParikramaTracker         tracker = new ParikramaTracker();

        private SessionRecord? activeSession;
        private bool isTracking;

        // ── Events ────────────────────────────────────────────────────────────────
        public event Action<int>? CountChanged;
        public event Action?      TargetReached;
        public event Action?      ThirdSideCompleted;
        public event Action?      ApproachingStart;

        // ── State (single source of truth) ────────────────────────────────────────
        public int    Count           => tracker.ParikramaCount;
        public int    Target          => prefs.TargetParikrama;
        public bool   IsTargetReached => tracker.IsTargetReached;
        public bool   IsTracking      => isTracking;

        // Tracker read-through for display
        public double CurrentProgress      => tracker.CurrentProgress;
        public int    SidesCompleted       => tracker.SidesCompleted;
        public int    CurrentStepsInCircle => tracker.CurrentStepsInCircle;
        public string GetDirection()       => tracker.GetDirection();

        public PradhakshinaSessionService(
            IAppPreferences         prefs,
            IVibrationService       vibration,
            ISessionRepository      repository,
            ISensorLifecycleService lifecycle)
        {
            this.prefs     = prefs     ?? throw new ArgumentNullException(nameof(prefs));
            this.vibration = vibration ?? throw new ArgumentNullException(nameof(vibration));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.lifecycle = lifecycle  ?? throw new ArgumentNullException(nameof(lifecycle));

            tracker.TargetParikramaCount  = prefs.TargetParikrama;
            tracker.OnThirdSideCompleted += () => { ThirdSideCompleted?.Invoke(); vibration.VibrateThirdSide(); };
            tracker.OnApproachingStart   += () => { ApproachingStart?.Invoke();   vibration.VibrateApproachingStart(); };

            tracker.ManualSetCount(prefs.ParikramaCount);
        }

        // ── Session control ───────────────────────────────────────────────────────

        public void StartTracking()
        {
            if (isTracking) return;
            isTracking = true;
            lifecycle.SetTrackingRate(true);      // Issue #5: switch to high rate
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
            lifecycle.SetTrackingRate(false);     // Issue #5: back to idle rate

            if (activeSession != null)
            {
                activeSession.CompletedAt    = DateTime.UtcNow;
                activeSession.CountCompleted = tracker.ParikramaCount;
                activeSession.TotalSteps     = totalSteps;
                await repository.SaveAsync(activeSession);
                activeSession = null;
            }
        }

        // ── Auto-detection ────────────────────────────────────────────────────────

        public void ProcessSensorData(double heading, int steps, bool isMoving, DateTime timestamp)
        {
            if (!isTracking || !prefs.AutoCountingEnabled) return;

            if (!tracker.CheckAndUpdateParikrama(heading, steps, isMoving, timestamp)) return;

            int newCount         = tracker.ParikramaCount;
            prefs.ParikramaCount = newCount;
            CountChanged?.Invoke(newCount);

            if (tracker.IsTargetReached) { TargetReached?.Invoke(); _ = vibration.VibrateTargetReachedAsync(); }
            else                          vibration.VibrateCompletion();
        }

        // ── Manual counting ───────────────────────────────────────────────────────

        public async Task ManualIncrementAsync()
        {
            int cap = Math.Max(Target * 2, Target + 10);
            if (tracker.ParikramaCount >= cap) return;

            tracker.ManualSetCount(tracker.ParikramaCount + 1);
            prefs.ParikramaCount = tracker.ParikramaCount;
            CountChanged?.Invoke(tracker.ParikramaCount);

            if (tracker.IsTargetReached)      { TargetReached?.Invoke(); await vibration.VibrateTargetReachedAsync(); }
            else if (tracker.ParikramaCount > 1) vibration.VibrateCompletion();
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
            prefs.TargetParikrama        = target;
            tracker.TargetParikramaCount = target;
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
