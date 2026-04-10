using System;

namespace ParikramaCounter.Domain
{
    // A single completed circumambulation. This is the core domain event —
    // the thing the app counts. Capturing per-pradhakshina data (start heading,
    // peak heading, step count, duration) enables per-temple accuracy analysis.
    public class Pradhakshina
    {
        public string   Id              { get; set; } = Guid.NewGuid().ToString();
        public int      SequenceNumber  { get; set; }   // 1-based within the session
        public DateTime CompletedAt     { get; set; } = DateTime.UtcNow;
        public double   StartHeading    { get; set; }   // compass bearing at start
        public double   PeakHeading     { get; set; }   // bearing at completion detection
        public double   CumulativeDeg   { get; set; }   // total degrees accumulated
        public int      StepsWalked     { get; set; }
        public bool     IsAutoDetected  { get; set; }   // false = manually counted
        public TimeSpan Duration        { get; set; }
    }
}
