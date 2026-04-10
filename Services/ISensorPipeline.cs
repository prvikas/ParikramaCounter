using System;

namespace ParikramaCounter.Services
{
    // Fix #5: the sensor fusion loop (raw sensor → fusion engine → session service)
    // runs independently of whether a ViewModel is observing it.
    // ViewModels subscribe to processed events (SensorProcessed) rather than
    // driving the pipeline themselves.
    public interface ISensorPipeline
    {
        // Fires on the thread pool after each sensor tick is processed.
        // Consumers must marshal to main thread themselves.
        event Action<Models.SensorData>? SensorProcessed;

        void Start();
        void Stop();
    }
}
