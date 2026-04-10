using System;
using System.Diagnostics;
using ParikramaCounter.Models;

namespace ParikramaCounter.Services
{
    // Fix #5: owns the raw-sensor → fusion → session pipeline.
    // TrackingViewModel observes SensorProcessed events and IPradhakshinaSessionService
    // events instead of driving this loop itself. This means the pipeline runs correctly
    // even if no ViewModel is active (e.g. during background calibration).
    //
    // Fix #7: error boundary wraps every tick — a transient sensor anomaly cannot
    // crash the app or silently stop counting.
    public class SensorPipeline : ISensorPipeline, IDisposable
    {
        private readonly ISensorService              sensorService;
        private readonly ISensorFusionEngine         fusionEngine;
        private readonly IPradhakshinaSessionService session;
        private bool running;
        private bool disposed;

        public event Action<SensorData>? SensorProcessed;

        public SensorPipeline(
            ISensorService              sensorService,
            ISensorFusionEngine         fusionEngine,
            IPradhakshinaSessionService session)
        {
            this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));
            this.fusionEngine  = fusionEngine  ?? throw new ArgumentNullException(nameof(fusionEngine));
            this.session       = session       ?? throw new ArgumentNullException(nameof(session));
        }

        public void Start()
        {
            if (running) return;
            running = true;
            sensorService.SensorDataReceived += OnRawSensorData;
        }

        public void Stop()
        {
            if (!running) return;
            running = false;
            sensorService.SensorDataReceived -= OnRawSensorData;
        }

        private void OnRawSensorData(double[] accel, double[] gyro, double[] mag)
        {
            // Fix #7: error boundary — the entire tick is protected.
            // A bad magnetometer reading or arithmetic exception cannot break counting.
            try
            {
                var data = fusionEngine.ProcessSensorData(accel, gyro, mag);

                // Feed into session service for auto-detection
                session.ProcessSensorData(
                    data.Heading, data.Steps, fusionEngine.IsMoving, data.Timestamp);

                // Notify all display observers (ViewModels)
                SensorProcessed?.Invoke(data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SensorPipeline] Tick error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Stop();
        }
    }
}
