#if DEBUG
using System;
using System.Threading;
using ParikramaCounter.Services;

namespace ParikramaCounter.Services
{
    // Simulator/emulator replacement for real platform sensor services.
    // Fires synthetic sensor data on a timer so all ViewModel, session, and
    // tracking logic can be exercised without any physical device or real motion.
    //
    // Simulated scenario: a person walking a full clockwise circle.
    // - Heading advances 1° per tick at high rate (20ms), 0.2° per tick at idle rate
    // - AccelY oscillates to simulate footsteps (triggers StepDetector)
    // - Magnetometer rotates with heading so tilt-compensation produces a clean heading
    //
    // Usage: in MauiProgram.cs, the #if DEBUG block registers this instead of
    // AndroidSensorService / iOSSensorService when running in the simulator.
    public class MockSensorService : ISensorService, IDisposable
    {
        private Timer? timer;
        private double headingDeg  = 0.0;      // current simulated heading
        private int    stepPhase   = 0;         // for generating footstep oscillation
        private int    stepCount   = 0;
        private bool   highRate    = false;
        private bool   isRunning   = false;
        private bool   disposed    = false;

        public int  HardwareStepCount { get; private set; }
        public void UpdateStepCount(int count) { HardwareStepCount = count; }

        public event Action<double[], double[], double[]>? SensorDataReceived;

        public void Start(bool highRate = false)
        {
            if (isRunning) return;
            isRunning      = true;
            this.highRate  = highRate;
            StartTimer();
        }

        public void SetRate(bool highRate)
        {
            if (!isRunning || this.highRate == highRate) return;
            this.highRate = highRate;
            timer?.Dispose();
            StartTimer();
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
            timer?.Dispose();
            timer = null;
        }

        private void StartTimer()
        {
            int intervalMs = highRate ? 20 : 200;
            timer = new Timer(_ => Tick(), null, 0, intervalMs);
        }

        private void Tick()
        {
            if (!isRunning) return;

            // Advance heading — 1° per tick at high rate completes one 360° circle in
            // 360 × 20ms = 7.2 seconds. Idle rate does 0.2° per tick.
            double degreesPerTick = highRate ? 1.0 : 0.2;
            headingDeg = (headingDeg + degreesPerTick) % 360.0;

            double headingRad = headingDeg * Math.PI / 180.0;

            // Gravity vector — device held flat, z-axis pointing down
            double[] accel = { 0.0, 0.0, 9.81 };

            // Add footstep oscillation to Y axis (simulates walking)
            stepPhase++;
            double stepOscillation = Math.Sin(stepPhase * 0.4) * 2.5;   // ±2.5 m/s²
            accel[1] += stepOscillation;

            // Gyro — zero (not used for heading in this app)
            double[] gyro = { 0.0, 0.0, 0.0 };

            // Magnetometer — rotate with heading so the fusion engine computes
            // the correct compass bearing
            double mag = 45.0;  // µT, typical Earth field strength
            double[] magField =
            {
                mag * Math.Cos(headingRad),   // X component
                0.0,                           // Y component (flat device)
                mag * Math.Sin(headingRad)    // Z component
            };

            SensorDataReceived?.Invoke(accel, gyro, magField);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Stop();
        }
    }
}
#endif
