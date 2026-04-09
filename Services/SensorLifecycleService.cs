using System;

namespace ParikramaCounter.Services
{
    // Fix #4: owns the sensor hardware lifecycle for the entire app.
    // The sensor starts when the app activates and stops when it deactivates.
    // ViewModels subscribe to ISensorService.SensorDataReceived for data —
    // they never call Start/Stop on the hardware directly.
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
            sensorService.Start();
            IsActive = true;
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            sensorService.Stop();
            IsActive = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Deactivate();
        }
    }
}
