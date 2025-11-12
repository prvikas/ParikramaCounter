using System;

namespace ParikramaCounter.Services
{
    public interface ISensorService
    {
        void Start();
        void Stop();
        event Action<double[], double[], double[]> SensorDataReceived; // accel, gyro, mag
    }
}
