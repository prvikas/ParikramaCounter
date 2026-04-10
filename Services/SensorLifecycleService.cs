using System;

namespace ParikramaCounter.Services
{
    public class SensorLifecycleService : ISensorLifecycleService
    {
        private readonly ISensorService sensorService;
        private bool disposed;

        public bool IsActive { get; private set; }

        public SensorLifecycleService(ISensorService sensorService)
            => this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));

        public void Activate()
        {
            if (IsActive) return;
            sensorService.Start();
            sensorService.SetRate(false);   // idle rate on launch
            IsActive = true;
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            sensorService.Stop();
            IsActive = false;
        }

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
