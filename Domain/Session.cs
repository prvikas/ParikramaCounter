using System;
using System.Collections.Generic;

namespace ParikramaCounter.Domain
{
    // A session is one visit to a temple to perform pradhakshinas.
    // It owns the list of individual Pradhakshina domain events and
    // the temple context. This replaces SessionRecord as the domain model —
    // SessionRecord becomes a persistence projection of this type.
    public class Session
    {
        public string              Id            { get; set; } = Guid.NewGuid().ToString();
        public string?             TempleId      { get; set; }   // null = no temple selected
        public string?             TempleName    { get; set; }   // denormalised for display
        public int                 Target        { get; set; }
        public DateTime            StartedAt     { get; set; } = DateTime.UtcNow;
        public DateTime?           CompletedAt   { get; set; }
        public List<Pradhakshina>  Pradhakshinas { get; set; } = new List<Pradhakshina>();
        public int                 TotalSteps    { get; set; }

        // ── Derived ───────────────────────────────────────────────────────────────
        public int      Count          => Pradhakshinas.Count;
        public bool     TargetReached  => Count >= Target;
        public bool     IsComplete     => CompletedAt.HasValue;
        public TimeSpan Duration       => CompletedAt.HasValue
            ? CompletedAt.Value - StartedAt
            : DateTime.UtcNow - StartedAt;

        // Heading distribution across all pradhakshinas — used for temple calibration.
        // Returns a dictionary of compass bearing (0°–350° in 10° buckets) to count.
        public Dictionary<int, int> HeadingDistribution()
        {
            var result = new Dictionary<int, int>();
            foreach (var p in Pradhakshinas)
            {
                int bucket = (int)(p.StartHeading / 10.0) % 36;
                result.TryGetValue(bucket, out int c);
                result[bucket] = c + 1;
            }
            return result;
        }

        // Add a detected pradhakshina and record its heading at the temple.
        public Pradhakshina RecordPradhakshina(
            double startHeading, double peakHeading, double cumulativeDeg,
            int stepsWalked, bool isAutoDetected, TimeSpan duration)
        {
            var p = new Pradhakshina
            {
                SequenceNumber = Count + 1,
                CompletedAt    = DateTime.UtcNow,
                StartHeading   = startHeading,
                PeakHeading    = peakHeading,
                CumulativeDeg  = cumulativeDeg,
                StepsWalked    = stepsWalked,
                IsAutoDetected = isAutoDetected,
                Duration       = duration
            };
            Pradhakshinas.Add(p);
            return p;
        }

        public void Complete(int totalSteps)
        {
            TotalSteps  = totalSteps;
            CompletedAt = DateTime.UtcNow;
        }
    }
}
