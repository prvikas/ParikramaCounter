using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using ParikramaCounter.Services;
using ParikramaCounter.ViewModels;

namespace ParikramaCounter
{
    public partial class App : Application
    {
        private ISensorLifecycleService? lifecycleService;
        private IVibrationService?       vibrationService;
        private TrackingViewModel?       trackingViewModel;
        private DiagnosticsViewModel?    diagnosticsViewModel;

        public App() { InitializeComponent(); }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var sp = IPlatformApplication.Current!.Services;
            lifecycleService     = sp.GetRequiredService<ISensorLifecycleService>();
            trackingViewModel    = sp.GetRequiredService<TrackingViewModel>();
            diagnosticsViewModel = sp.GetRequiredService<DiagnosticsViewModel>();

            // Resolve VibrationService so its constructor runs and event subscriptions
            // to IPradhakshinaSessionService are established before tracking starts.
            vibrationService = sp.GetRequiredService<IVibrationService>();

            // Start sensors at idle rate — SensorPipeline starts when user presses Start
            lifecycleService.Activate();

            var window = new Window(new AppShell());
            window.Destroying += (_, _) =>
            {
                lifecycleService?.Deactivate();
                (vibrationService as System.IDisposable)?.Dispose();
                trackingViewModel?.Dispose();
                diagnosticsViewModel?.Dispose();
            };
            return window;
        }
    }
}
