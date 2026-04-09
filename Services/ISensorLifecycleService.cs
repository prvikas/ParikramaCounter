using System;

namespace ParikramaCounter.Services
{
    // Fix #4 (sensor lifecycle ownership): single declared owner of sensor hardware.
    // The sensor starts when the app becomes active and stops on dispose.
    // ViewModels subscribe to ISensorService.SensorDataReceived — they never
    // call Start()/Stop() directly. TrackingViewModel toggles tracking state
    // via StartTracking/StopTracking on this service, which gates whether the
    // ParikramaTracker processes data — the hardware keeps running.
    public interface ISensorLifecycleService : IDisposable
    {
        bool IsActive { get; }
        void Activate();    // called by App on launch
        void Deactivate();  // called by App on destroy
    }
}
