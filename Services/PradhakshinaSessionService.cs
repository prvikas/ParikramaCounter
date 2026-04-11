using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ParikramaCounter.Domain;
using ParikramaCounter.Repositories;

namespace ParikramaCounter.Services
{
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
        private DateTime  circleStartTime    = DateTime.UtcNow;
        private double    circleStartHeading = 0;

        // Active temple — set by UI via SetActiveTemple(); cached so StartTracking()
        // can access the name synchronously without an async repository call.
        private string? _activeTempleId;
        private string? _activeTempleName;

        public string? ActiveTempleId   => _activeTempleId;
        public string? ActiveTempleName => _activeTempleName;

        public void SetActiveTemple(string? id, string? name)
        {
            _activeTempleId   = id;
            _activeTempleName = name;
            sessionState.ActiveTempleId = id;
        }

        public event Action<int>? CountChanged;
        public event Action?      TargetReached;
        public event Action?      ThirdSideCompleted;
        public event Action?      ApproachingStart;

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

            tracker.TargetParikramaCount = sessionState.TargetParikrama;
            tracker.OnThirdSideCompleted += () => ThirdSideCompleted?.Invoke();
            tracker.OnApproachingStart   += () => ApproachingStart?.Invoke();
            tracker.ManualSetCount(sessionState.ParikramaCount);

            // Restore active temple id from persisted prefs (name cached on next SetActiveTemple call)
            _activeTempleId = sessionState.ActiveTempleId;
        }

        public void StartTracking()
        {
            if (isTracking) return;
            isTracking = true;
            lifecycle.SetTrackingRate(true);

            activeSession = new Session
            {
                Target     = sessionState.TargetParikrama,
                StartedAt  = DateTime.UtcNow,
                TempleId   = _activeTempleId,
                TempleName = _activeTempleName   // populated by SetActiveTemple
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
                await repository.SaveAsync(Models.SessionRecord.FromSession(activeSession));

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
                if (tracker.SidesCompleted == 0 && tracker.CurrentProgress < 1.0)
                    circleStartHeading = heading;

                if (!tracker.CheckAndUpdateParikrama(heading, steps, isMoving, timestamp)) return;

                if (activeSession != null)
                {
                    var duration = DateTime.UtcNow - circleStartTime;
                    activeSession.RecordPradhakshina(
                        startHeading:   circleStartHeading,
                        peakHeading:    heading,
                        cumulativeDeg:  tracker.CurrentProgress * 3.6,
                        stepsWalked:    tracker.CurrentStepsInCircle,
                        isAutoDetected: true,
                        duration:       duration);
                    circleStartTime    = DateTime.UtcNow;
                    circleStartHeading = heading;
                }

                int newCount = tracker.ParikramaCount;
                sessionState.ParikramaCount = newCount;
                CountChanged?.Invoke(newCount);

                if (tracker.IsTargetReached) TargetReached?.Invoke();
            }
            catch (Exception ex)
            {
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
            sessionState.TargetParikrama    = target;
            tracker.TargetParikramaCount    = target;
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
