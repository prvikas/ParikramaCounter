using System;

namespace ParikramaCounter.Models
{
    // Fix #12: session history model.
    // Each completed session (all target pradhakshinas done, or manually ended)
    // is recorded here. Stored via ISessionRepository for future features:
    // temple-specific accuracy, historical totals, streak tracking.
    public class SessionRecord
    {
        public string   Id             { get; set; } = Guid.NewGuid().ToString();
        public DateTime StartedAt      { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt   { get; set; }
        public int      Target         { get; set; }
        public int      CountCompleted { get; set; }
        public bool     TargetReached  => CompletedAt.HasValue && CountCompleted >= Target;
        public int      TotalSteps     { get; set; }
        public TimeSpan Duration       => CompletedAt.HasValue
            ? CompletedAt.Value - StartedAt
            : DateTime.UtcNow - StartedAt;
    }
}
