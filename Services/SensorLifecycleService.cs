using System;

namespace ParikramaCounter.Services
{
    // Issue #5: sensors start at low rate (idle) on app launch.
    // Switches to high rate (game) when tracking starts, back to low when stopped.
    // This avoids draining battery while the devotee is navigating menus or
    // before they begin their pradhakshina walk.
    public class SensorLifecycleService : ISensorLifecycleService
    {
        private readonly ISensorService sensorService;
        private bool disposed;

        public bool IsActive { get; private set; }

        public SensorLifecycleService(ISensorService sensorService)
        {
            this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));
        }

        public void Activate()
        {
            if (IsActive) return;
            sensorService.Start(highRate: false);   // idle rate on startup
            IsActive = true;
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            sensorService.Stop();
            IsActive = false;
        }

        // Called by PradhakshinaSessionService when tracking starts/stops.
        public void SetTrackingRate(bool tracking)
        {
            if (!IsActive) return;
            sensorService.SetRate(tracking);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Deactivate();
        }
    }
}
