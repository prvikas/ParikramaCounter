using System;
using System.Threading.Tasks;

namespace ParikramaCounter.Services
{
    public interface IPradhakshinaSessionService
    {
        int    Count           { get; }
        int    Target          { get; }
        bool   IsTargetReached { get; }
        bool   IsTracking      { get; }
        double CurrentProgress      { get; }
        int    SidesCompleted       { get; }
        int    CurrentStepsInCircle { get; }
        string GetDirection();

        // Domain events — fired on calling thread, consumers marshal to main thread
        event Action<int>? CountChanged;
        event Action?      TargetReached;
        event Action?      ThirdSideCompleted;
        event Action?      ApproachingStart;

        void  StartTracking();
        Task  StopTrackingAsync(int totalSteps);
        Task  ResetAsync();
        void  ProcessSensorData(double heading, int steps, bool isMoving, DateTime timestamp);
        Task  ManualIncrementAsync();
        void  ManualDecrement();
        void  SetTarget(int target);
    }
}
