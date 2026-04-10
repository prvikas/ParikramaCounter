using System;
using System.Text.Json.Serialization;

namespace ParikramaCounter.Models
{
    public class SessionRecord
    {
        public string    Id             { get; set; } = Guid.NewGuid().ToString();
        public DateTime  StartedAt      { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt    { get; set; }
        public int       Target         { get; set; }
        public int       CountCompleted { get; set; }
        public int       TotalSteps     { get; set; }

        // Issue #8: store computed values as settable properties so System.Text.Json
        // serialises them into the file. A consumer reading raw JSON gets complete data.
        // Setters are private — only the deserialiser and this class write them.
        public bool     TargetReached { get; set; }
        public TimeSpan Duration      { get; set; }

        // Called once when a session is finalised before saving.
        // Captures the computed values so they round-trip through JSON correctly.
        [JsonIgnore]
        public bool IsComplete => CompletedAt.HasValue;

        public void Finalise()
        {
            TargetReached = CompletedAt.HasValue && CountCompleted >= Target;
            Duration      = CompletedAt.HasValue
                ? CompletedAt.Value - StartedAt
                : TimeSpan.Zero;
        }
    }
}
