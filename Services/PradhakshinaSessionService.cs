using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ParikramaCounter.Domain;
using ParikramaCounter.Repositories;

namespace ParikramaCounter.Services
{
    // Fix #4: service fires domain events only — does NOT call IVibrationService directly.
    // VibrationService subscribes to the events externally in MauiProgram.
    // This means adding a new side effect (sound, notification) doesn't touch this class.
    //
    // Fix: uses Session domain object internally — rich per-pradhakshina data
    // including start heading, cumulative degrees, and steps per circuit.
    public class PradhakshinaSessionService : IPradhakshinaSessionService
    {
        private readonly ISessionState          sessionState;
        private readonly IUserPreferences       userPrefs;
        private readonly ISessionRepository     repository;
        private readonly ITempleRepository      templeRepo;
        private readonly ISensorLifecycleService lifecycle;
        private readonly Models.ParikramaTracker tracker = new Models.ParikramaTracker();

        private Session?  activeSession;
        private bool      isTracking;
        private DateTime  circleStartTime = DateTime.UtcNow;
        private double    circleStartHeading = 0;

        // ── Domain events (not UI events — consumers decide what to do) ───────────
        public event Action<int>?    CountChanged;
        public event Action?         TargetReached;
        public event Action?         ThirdSideCompleted;
        public event Action?         ApproachingStart;

        // ── State ─────────────────────────────────────────────────────────────────
        public int    Count           => tracker.ParikramaCount;
        public int    Target          => sessionState.TargetParikrama;
        public bool   IsTargetReached => tracker.IsTargetReached;
        public bool   IsTracking      => isTracking;
        public double CurrentProgress      => tracker.CurrentProgress;
        public int    SidesCompleted       => tracker.SidesCompleted;
        public int    CurrentStepsInCircle => tracker.CurrentStepsInCircle;
        public string GetDirection()       => tracker.GetDirection();

        public PradhakshinaSessionService(
            ISessionState           sessionState,
            IUserPreferences        userPrefs,
            ISessionRepository      repository,
            ITempleRepository       templeRepo,
            ISensorLifecycleService lifecycle)
        {
            this.sessionState = sessionState ?? throw new ArgumentNullException(nameof(sessionState));
            this.userPrefs    = userPrefs    ?? throw new ArgumentNullException(nameof(userPrefs));
            this.repository   = repository   ?? throw new ArgumentNullException(nameof(repository));
            this.templeRepo   = templeRepo   ?? throw new ArgumentNullException(nameof(templeRepo));
            this.lifecycle    = lifecycle    ?? throw new ArgumentNullException(nameof(lifecycle));

            tracker.TargetParikramaCount  = sessionState.TargetParikrama;

            // Fix #4: tracker events become service events — no vibration here
            tracker.OnThirdSideCompleted += () => ThirdSideCompleted?.Invoke();
            tracker.OnApproachingStart   += () => ApproachingStart?.Invoke();

            tracker.ManualSetCount(sessionState.ParikramaCount);
        }

        public void StartTracking()
        {
            if (isTracking) return;
            isTracking = true;
            lifecycle.SetTrackingRate(true);

            string? templeId   = sessionState.ActiveTempleId;
            string? templeName = null;

            activeSession = new Session
            {
                Target     = sessionState.TargetParikrama,
                StartedAt  = DateTime.UtcNow,
                TempleId   = templeId,
                TempleName = templeName
            };
            circleStartTime    = DateTime.UtcNow;
            circleStartHeading = 0;
        }

        public async Task StopTrackingAsync(int totalSteps)
        {
            if (!isTracking) return;
            isTracking = false;
            lifecycle.SetTrackingRate(false);

            if (activeSession != null)
            {
                activeSession.Complete(totalSteps);
                var record = SessionRecord.FromSession(activeSession);
                await repository.SaveAsync(record);

                // Update temple heading data if a temple is selected
                if (!string.IsNullOrEmpty(activeSession.TempleId))
                    await UpdateTempleHeadingDataAsync(activeSession);

                activeSession = null;
            }
        }

        public void ProcessSensorData(double heading, int steps, bool isMoving, DateTime timestamp)
        {
            if (!isTracking || !userPrefs.AutoCountingEnabled) return;

            try
            {
                // Track start heading for the current circle
                if (tracker.SidesCompleted == 0 && tracker.CurrentProgress < 1.0)
                    circleStartHeading = heading;

                if (!tracker.CheckAndUpdateParikrama(heading, steps, isMoving, timestamp)) return;

                // Record the completed pradhakshina with full heading data
                if (activeSession != null)
                {
                    var duration = DateTime.UtcNow - circleStartTime;
                    activeSession.RecordPradhakshina(
                        startHeading:  circleStartHeading,
                        peakHeading:   heading,
                        cumulativeDeg: tracker.CurrentProgress * 3.6,  // 0–100% → 0–360°
                        stepsWalked:   tracker.CurrentStepsInCircle,
                        isAutoDetected: true,
                        duration:      duration);
                    circleStartTime    = DateTime.UtcNow;
                    circleStartHeading = heading;
                }

                int newCount = tracker.ParikramaCount;
                sessionState.ParikramaCount = newCount;
                CountChanged?.Invoke(newCount);

                // Fix #4: fire domain event — let subscribers decide on vibration/sound/UI
                if (tracker.IsTargetReached) TargetReached?.Invoke();
            }
            catch (Exception ex)
            {
                // Fix #7: error boundary — sensor anomalies should not break counting
                Debug.WriteLine($"[SessionService] ProcessSensorData error: {ex.Message}");
            }
        }

        public async Task ManualIncrementAsync()
        {
            int cap = Math.Max(Target * 2, Target + 10);
            if (tracker.ParikramaCount >= cap) return;

            tracker.ManualSetCount(tracker.ParikramaCount + 1);

            if (activeSession != null)
                activeSession.RecordPradhakshina(0, 0, 0, 0, false, TimeSpan.Zero);

            sessionState.ParikramaCount = tracker.ParikramaCount;
            CountChanged?.Invoke(tracker.ParikramaCount);
            if (tracker.IsTargetReached) TargetReached?.Invoke();
        }

        public void ManualDecrement()
        {
            if (tracker.ParikramaCount <= 0) return;
            tracker.ManualSetCount(tracker.ParikramaCount - 1);
            sessionState.ParikramaCount = tracker.ParikramaCount;
            CountChanged?.Invoke(tracker.ParikramaCount);
        }

        public void SetTarget(int target)
        {
            sessionState.TargetParikrama        = target;
            tracker.TargetParikramaCount         = target;
        }

        public async Task ResetAsync()
        {
            if (isTracking) await StopTrackingAsync(0);
            tracker.Reset();
            sessionState.ParikramaCount = 0;
            CountChanged?.Invoke(0);
        }

        private async Task UpdateTempleHeadingDataAsync(Session session)
        {
            try
            {
                var temple = await templeRepo.GetByIdAsync(session.TempleId!);
                if (temple == null) return;
                foreach (var p in session.Pradhakshinas)
                    temple.RecordHeading(p.StartHeading);
                await templeRepo.SaveAsync(temple);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SessionService] Temple heading update error: {ex.Message}");
            }
        }
    }
}
