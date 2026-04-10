using System;

namespace ParikramaCounter.Services
{
    public interface ISensorService
    {
        void Start(bool highRate = false);  // highRate=false → ~200ms (idle), true → ~20ms (tracking)
        void Stop();
        void SetRate(bool highRate);        // switch rate without stop/start

        event Action<double[], double[], double[]> SensorDataReceived;

        int  HardwareStepCount { get; }
        void UpdateStepCount(int count);
    }
}
