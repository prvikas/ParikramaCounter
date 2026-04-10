using System;

namespace ParikramaCounter.Services
{
    // Fix #2: removed hardware-rate detail (highRate bool) from the interface —
    // rate selection is an implementation concern of SensorLifecycleService.
    // Removed UpdateStepCount write-back — step count flows one way:
    // hardware → ISensorService.HardwareStepCount (read by engine).
    // The engine no longer writes back to the service.
    public interface ISensorService
    {
        void Start();
        void Stop();
        void SetRate(bool highRate);   // controlled only by SensorLifecycleService

        event Action<double[], double[], double[]> SensorDataReceived;

        int HardwareStepCount { get; }
    }
}
