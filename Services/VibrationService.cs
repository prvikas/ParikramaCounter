using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;

namespace ParikramaCounter.Services
{
    // Issue #9: bare catch{} replaced with typed catches that log in DEBUG.
    // FeatureNotSupportedException (device has no vibrator) is silently ignored —
    // the app can still function. Any other exception is logged so it's visible
    // during development and doesn't hide programming errors.
    public class VibrationService : IVibrationService
    {
        private readonly IAppPreferences prefs;

        public VibrationService(IAppPreferences prefs) => this.prefs = prefs;

        public void VibrateThirdSide()        => Vibrate(prefs.ThirdSideVibrationMs);
        public void VibrateApproachingStart() => Vibrate(prefs.ApproachingStartVibrationMs);
        public void VibrateCompletion()       => Vibrate(prefs.CompletionVibrationMs);

        public async Task VibrateTargetReachedAsync()
        {
            if (!prefs.EnableVibrations) return;
            for (int i = 0; i < prefs.TargetVibrationCount; i++)
            {
                Vibrate(prefs.TargetVibrationMs);
                await Task.Delay(prefs.TargetVibrationMs + 200);
            }
        }

        private void Vibrate(int ms)
        {
            if (!prefs.EnableVibrations) return;
            try
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(ms));
            }
            catch (FeatureNotSupportedException)
            {
                // Device has no vibrator — silent, expected on some hardware
            }
            catch (Exception ex)
            {
                // Unexpected — log in DEBUG so it's visible during development
                Debug.WriteLine($"[VibrationService] Unexpected error: {ex.Message}");
            }
        }
    }
}
