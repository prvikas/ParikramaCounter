using System;
using System.Collections.Generic;

namespace ParikramaCounter.Domain
{
    // Temple is the root domain entity. A pradhakshina is always performed
    // at a specific temple — this is the context that makes counting meaningful.
    // HeadingData captures the compass readings observed at this temple across
    // all sessions, enabling future accuracy improvement per location.
    public class Temple
    {
        public string   Id          { get; set; } = Guid.NewGuid().ToString();
        public string   Name        { get; set; } = string.Empty;
        public string   Location    { get; set; } = string.Empty;   // city / district
        public double?  Latitude    { get; set; }
        public double?  Longitude   { get; set; }

        // Compass bearing at which the sanctum entrance faces (North = 0°).
        // Null until calibrated from observed session data.
        public double?  EntranceBearing { get; set; }

        // Accumulated heading observations keyed by 10° bucket (0–35).
        // Used to learn the temple's geometry and improve detection accuracy.
        public Dictionary<int, int> HeadingBucketCounts { get; set; }
            = new Dictionary<int, int>();

        public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt   { get; set; } = DateTime.UtcNow;

        // Record a heading observation. Bucket index = (int)(heading / 10).
        public void RecordHeading(double headingDegrees)
        {
            int bucket = (int)(headingDegrees / 10.0) % 36;
            HeadingBucketCounts.TryGetValue(bucket, out int existing);
            HeadingBucketCounts[bucket] = existing + 1;
            UpdatedAt = DateTime.UtcNow;
        }

        // Most-observed heading bucket — proxy for the dominant walk direction.
        public double? DominantHeading()
        {
            int maxCount = 0;
            int maxBucket = -1;
            foreach (var kv in HeadingBucketCounts)
            {
                if (kv.Value > maxCount) { maxCount = kv.Value; maxBucket = kv.Key; }
            }
            return maxBucket >= 0 ? maxBucket * 10.0 + 5.0 : null;
        }
    }
}
