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
    public class MockSensorService : ISensorService, IDisposable
    {
        private Timer?  timer;
        private double  headingDeg = 0.0;
        private int     stepPhase  = 0;
        private bool    highRate   = false;
        private bool    isRunning  = false;
        private bool    disposed   = false;

        public int HardwareStepCount { get; private set; }

        public event Action<double[], double[], double[]>? SensorDataReceived;

        // Fix: Start() takes no arguments — matches updated ISensorService
        public void Start()
        {
            if (isRunning) return;
            isRunning = true;
            // starts at idle rate; caller uses SetRate(true) when tracking begins
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

            // Advance heading: 1°/tick at high rate → one 360° circle in 7.2s
            double degreesPerTick = highRate ? 1.0 : 0.2;
            headingDeg = (headingDeg + degreesPerTick) % 360.0;

            double headingRad = headingDeg * Math.PI / 180.0;

            // Gravity — device held flat
            double[] accel = { 0.0, 0.0, 9.81 };

            // Footstep oscillation on Y axis (triggers StepDetector)
            stepPhase++;
            accel[1] += Math.Sin(stepPhase * 0.4) * 2.5;

            double[] gyro = { 0.0, 0.0, 0.0 };

            // Magnetometer rotates with heading so fusion engine computes correct bearing
            double mag = 45.0;
            double[] magField =
            {
                mag * Math.Cos(headingRad),
                0.0,
                mag * Math.Sin(headingRad)
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
