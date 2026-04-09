using System;

namespace ParikramaCounter.Services
{
    public interface ISensorService
    {
        void Start();
        void Stop();

        event Action<double[], double[], double[]> SensorDataReceived;

        // Step count readable by the fusion engine on any platform.
        int HardwareStepCount { get; }

        // Fix #1 (layer inversion): engine calls this instead of casting to the
        // concrete Android type. Both platforms implement it — Android writes through
        // to the backing field, iOS is a no-op (CMPedometer updates the field directly).
        void UpdateStepCount(int count);
    }
}
