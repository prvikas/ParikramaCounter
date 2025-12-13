using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ParikramaCounter.Models
{
    public class TempleProfile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("name")]
        public string Name { get; set; } = "Unknown Temple";

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("detectedShape")]
        public string DetectedShape { get; set; } = "Unknown";

        [JsonPropertyName("avgCircumference")]
        public double AverageCircumference { get; set; }

        [JsonPropertyName("avgDuration")]
        public double AverageDuration { get; set; }

        [JsonPropertyName("directionalDistances")]
        public Dictionary<string, double> DirectionalDistances { get; set; } = new();

        [JsonPropertyName("recommendedParikramas")]
        public int RecommendedParikramas { get; set; } = 7;

        [JsonPropertyName("totalParikramasCompleted")]
        public int TotalParikramasCompleted { get; set; }

        [JsonPropertyName("lastVisited")]
        public DateTime LastVisited { get; set; } = DateTime.Now;

        [JsonPropertyName("createdDate")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [JsonPropertyName("accuracy")]
        public double Accuracy { get; set; } = 0;

        public void UpdateFromParikrama(DirectionalTracker tracker, double duration)
        {
            TotalParikramasCompleted++;
            LastVisited = DateTime.Now;

            // Update shape
            DetectedShape = tracker.DetectPathShape().ToString();

            // Update average circumference
            double newCircumference = tracker.GetTotalDistance();
            if (AverageCircumference == 0)
            {
                AverageCircumference = newCircumference;
            }
            else
            {
                AverageCircumference = (AverageCircumference + newCircumference) / 2.0;
            }

            // Update average duration
            if (AverageDuration == 0)
            {
                AverageDuration = duration;
            }
            else
            {
                AverageDuration = (AverageDuration + duration) / 2.0;
            }

            // Update directional distances (weighted average)
            var currentDistances = tracker.GetDirectionalDistances();
            foreach (var kvp in currentDistances)
            {
                if (DirectionalDistances.ContainsKey(kvp.Key))
                {
                    DirectionalDistances[kvp.Key] = (DirectionalDistances[kvp.Key] + kvp.Value) / 2.0;
                }
                else
                {
                    DirectionalDistances[kvp.Key] = kvp.Value;
                }
            }

            // Calculate accuracy (improves with more data)
            Accuracy = Math.Min(100, (TotalParikramasCompleted / 10.0) * 100);

            System.Diagnostics.Debug.WriteLine($"📊 Profile Updated: {Name}");
            System.Diagnostics.Debug.WriteLine($"   Completed: {TotalParikramasCompleted}");
            System.Diagnostics.Debug.WriteLine($"   Avg Distance: {AverageCircumference:F1}m");
            System.Diagnostics.Debug.WriteLine($"   Accuracy: {Accuracy:F0}%");
        }
    }
}
