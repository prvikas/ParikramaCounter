using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ParikramaCounter.Domain;

namespace ParikramaCounter.Models
{
    // Persistence projection of the Session domain object.
    // Carries the full Pradhakshina list so heading-per-direction analysis
    // can be performed offline on the stored data.
    public class SessionRecord
    {
        public string    Id             { get; set; } = Guid.NewGuid().ToString();
        public string?   TempleId       { get; set; }
        public string?   TempleName     { get; set; }
        public DateTime  StartedAt      { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt    { get; set; }
        public int       Target         { get; set; }
        public int       CountCompleted { get; set; }
        public int       TotalSteps     { get; set; }
        public bool      TargetReached  { get; set; }
        public TimeSpan  Duration       { get; set; }

        // Per-pradhakshina records — heading + steps per circuit.
        // This is the data that enables per-temple direction analysis:
        // "At Tirupati, I always start facing 215° and complete at 195°."
        public List<Pradhakshina> Pradhakshinas { get; set; } = new List<Pradhakshina>();

        [JsonIgnore]
        public bool IsComplete => CompletedAt.HasValue;

        // Projects a Session domain object into this record.
        public static SessionRecord FromSession(Session session)
        {
            return new SessionRecord
            {
                Id             = session.Id,
                TempleId       = session.TempleId,
                TempleName     = session.TempleName,
                StartedAt      = session.StartedAt,
                CompletedAt    = session.CompletedAt,
                Target         = session.Target,
                CountCompleted = session.Count,
                TotalSteps     = session.TotalSteps,
                TargetReached  = session.TargetReached,
                Duration       = session.Duration,
                Pradhakshinas  = session.Pradhakshinas
            };
        }
    }
}
