using System;
using System.Threading.Tasks;

namespace ParikramaCounter.Services
{
    public interface IPradhakshinaSessionService
    {
        // State
        int    Count           { get; }
        int    Target          { get; }
        bool   IsTargetReached { get; }
        bool   IsTracking      { get; }

        // Tracker display read-through
        double CurrentProgress      { get; }
        int    SidesCompleted       { get; }
        int    CurrentStepsInCircle { get; }
        string GetDirection();

        // Events — fired on the calling thread; consumers must marshal to main thread
        event Action<int>? CountChanged;
        event Action?      TargetReached;
        event Action?      ThirdSideCompleted;
        event Action?      ApproachingStart;

        // Session control
        void  StartTracking();
        Task  StopTrackingAsync(int totalSteps);
        Task  ResetAsync();

        // Counting
        void  ProcessSensorData(double heading, int steps, bool isMoving, DateTime timestamp);
        Task  ManualIncrementAsync();
        void  ManualDecrement();

        // Target
        void  SetTarget(int target);
    }
}
