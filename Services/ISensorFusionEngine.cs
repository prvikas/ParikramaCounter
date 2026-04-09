using System;
using ParikramaCounter.Models;

namespace ParikramaCounter.Services
{
    // Fix #2: interface for SensorFusionEngine so ViewModels depend on an
    // abstraction, not the concrete class. Makes the engine swappable and testable.
    public interface ISensorFusionEngine
    {
        bool IsMoving { get; }
        int  StepCount { get; }

        SensorData ProcessSensorData(double[] accel, double[] gyro, double[] mag);
        void Reset();
        void ResetForCalibration();
        void UpdateStepThreshold(int threshold);
        void UpdateMinStepInterval(int ms);
    }
}
