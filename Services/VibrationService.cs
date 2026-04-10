using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;

namespace ParikramaCounter.Services
{
    // Fix #4: VibrationService now subscribes to IPradhakshinaSessionService events
    // rather than being called directly. This decouples the session service from
    // the vibration implementation entirely.
    public class VibrationService : IVibrationService, IDisposable
    {
        private readonly IUserPreferences          prefs;
        private readonly IPradhakshinaSessionService session;
        private bool disposed;

        public VibrationService(IUserPreferences prefs, IPradhakshinaSessionService session)
        {
            this.prefs   = prefs   ?? throw new ArgumentNullException(nameof(prefs));
            this.session = session ?? throw new ArgumentNullException(nameof(session));

            // Subscribe to domain events — session service fires events, we handle side effects
            session.ThirdSideCompleted += VibrateThirdSide;
            session.ApproachingStart   += VibrateApproachingStart;
            session.CountChanged       += OnCountChanged;
            session.TargetReached      += OnTargetReached;
        }

        private void OnCountChanged(int count)
        {
            // Vibrate on each completion except the first tap (count > 1)
            if (count > 1 && !session.IsTargetReached)
                VibrateCompletion();
        }

        private void OnTargetReached() => _ = VibrateTargetReachedAsync();

        public void VibrateThirdSide()        => Vibrate(prefs.ThirdSideVibrationMs);
        public void VibrateApproachingStart() => Vibrate(prefs.ApproachingStartVibrationMs);
        public void VibrateCompletion()       => Vibrate(prefs.CompletionVibrationMs);

        public async Task VibrateTargetReachedAsync()
        {
            if (!prefs.EnableVibrations) return;
            int count = prefs.TargetVibrationCount;
            int ms    = prefs.TargetVibrationMs;
            for (int i = 0; i < count; i++)
            {
                Vibrate(ms);
                await Task.Delay(ms + 200);
            }
        }

        private void Vibrate(int ms)
        {
            if (!prefs.EnableVibrations) return;
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(ms)); }
            catch (FeatureNotSupportedException) { }
            catch (Exception ex) { Debug.WriteLine($"[VibrationService] {ex.Message}"); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            session.ThirdSideCompleted -= VibrateThirdSide;
            session.ApproachingStart   -= VibrateApproachingStart;
            session.CountChanged       -= OnCountChanged;
            session.TargetReached      -= OnTargetReached;
        }
    }
}
