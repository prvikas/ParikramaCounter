using System;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;

namespace ParikramaCounter.Services
{
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
            try
            {
                for (int i = 0; i < prefs.TargetVibrationCount; i++)
                {
                    Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(prefs.TargetVibrationMs));
                    await Task.Delay(prefs.TargetVibrationMs + 200);
                }
            }
            catch { }
        }

        private void Vibrate(int ms)
        {
            if (!prefs.EnableVibrations) return;
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(ms)); }
            catch { }
        }
    }
}
