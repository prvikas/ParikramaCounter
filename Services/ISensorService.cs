using System;

namespace ParikramaCounter.Services
{
    public interface ISensorService
    {
        void Start();
        void Stop();

        // Raw sensor arrays: accel (m/s²), gyro (rad/s), mag (µT)
        event Action<double[], double[], double[]> SensorDataReceived;

        // Hardware step count — updated by CMPedometer on iOS, StepDetector on Android.
        // Separate from the raw sensor event so iOS pedometer updates don't force
        // a full sensor dispatch and the gyro[0] encoding hack is eliminated.
        int HardwareStepCount { get; }
    }
}
