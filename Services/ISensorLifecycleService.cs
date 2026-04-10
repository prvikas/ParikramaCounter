using System;

namespace ParikramaCounter.Services
{
    public interface ISensorLifecycleService : IDisposable
    {
        bool IsActive { get; }

        // App lifecycle — called by App.xaml.cs
        void Activate();
        void Deactivate();

        // Issue #5 (battery): sensors run at low rate when idle, high rate when
        // tracking. SensorRate.Idle ≈ 200ms (UI level), SensorRate.Tracking ≈ 20ms (Game level).
        void SetTrackingRate(bool tracking);
    }
}
