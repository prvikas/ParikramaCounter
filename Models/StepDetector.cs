using System;
using System.Collections.Generic;
using System.Linq;

namespace ParikramaCounter.Models
{
    public class StepDetector
    {
        private readonly Queue<double> accelHistory = new Queue<double>(50);
        private DateTime lastMovementTime = DateTime.MinValue;

        public bool IsMoving { get; private set; }

        public void Update(double accelMagnitude)
        {
            accelHistory.Enqueue(accelMagnitude);
            if (accelHistory.Count > 50)
                accelHistory.Dequeue();

            if (accelHistory.Count < 10)
            {
                IsMoving = false;
                return;
            }

            double mean = accelHistory.Average();
            double variance = accelHistory.Sum(v => Math.Pow(v - mean, 2)) / accelHistory.Count;

            // Lower threshold for gentle walking
            bool currentlyMoving = variance > 0.08;

            if (currentlyMoving)
            {
                lastMovementTime = DateTime.Now;
                IsMoving = true;
            }
            else
            {
                // Debounce: keep "moving" for 2 seconds
                IsMoving = (DateTime.Now - lastMovementTime).TotalSeconds < 2.0;
            }

            // Reduced logging
            if (accelHistory.Count % 100 == 0)
            {
                System.Diagnostics.Debug.WriteLine($"Var: {variance:F3} | Moving: {IsMoving}");
            }
        }

        public void Reset()
        {
            accelHistory.Clear();
            IsMoving = false;
            lastMovementTime = DateTime.MinValue;
        }
    }
}
