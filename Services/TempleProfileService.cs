using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ParikramaCounter.Models;

namespace ParikramaCounter.Services
{
    public class TempleProfileService
    {
        private readonly string profilesPath;
        private List<TempleProfile> profiles = new();

        public TempleProfileService()
        {
            profilesPath = Path.Combine(FileSystem.AppDataDirectory, "temple_profiles.json");
            _ = LoadProfilesAsync();
        }

        public async Task LoadProfilesAsync()
        {
            try
            {
                if (File.Exists(profilesPath))
                {
                    string json = await File.ReadAllTextAsync(profilesPath);
                    profiles = JsonSerializer.Deserialize<List<TempleProfile>>(json) ?? new();
                    System.Diagnostics.Debug.WriteLine($"✅ Loaded {profiles.Count} temple profiles");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Load error: {ex.Message}");
                profiles = new();
            }
        }

        public async Task SaveProfilesAsync()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(profiles, options);
                await File.WriteAllTextAsync(profilesPath, json);
                System.Diagnostics.Debug.WriteLine($"💾 Saved {profiles.Count} profiles");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Save error: {ex.Message}");
            }
        }

        public async Task<TempleProfile> GetOrCreateProfileAsync(double latitude, double longitude, double radiusMeters = 100)
        {
            // Find existing profile near this location
            var existing = profiles.FirstOrDefault(p =>
                CalculateDistance(p.Latitude, p.Longitude, latitude, longitude) <= radiusMeters
            );

            if (existing != null)
            {
                System.Diagnostics.Debug.WriteLine($"📍 Found existing profile: {existing.Name} (Accuracy: {existing.Accuracy:F0}%)");
                return existing;
            }

            // Create new profile
            var newProfile = new TempleProfile
            {
                Latitude = latitude,
                Longitude = longitude,
                Name = $"Temple at {latitude:F4}, {longitude:F4}"
            };

            profiles.Add(newProfile);
            await SaveProfilesAsync();

            System.Diagnostics.Debug.WriteLine($"🆕 Created new profile: {newProfile.Name}");
            return newProfile;
        }

        public async Task UpdateProfileAsync(TempleProfile profile, DirectionalTracker tracker, double duration)
        {
            profile.UpdateFromParikrama(tracker, duration);
            await SaveProfilesAsync();
        }

        public List<TempleProfile> GetAllProfiles() => profiles;

        public async Task<TempleProfile> GetProfileByIdAsync(string id)
        {
            return profiles.FirstOrDefault(p => p.Id == id);
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}
