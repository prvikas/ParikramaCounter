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
        private TrackingViewModel?       trackingViewModel;
        private DiagnosticsViewModel?    diagnosticsViewModel;

        public App() { InitializeComponent(); }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var sp = IPlatformApplication.Current!.Services;
            lifecycleService     = sp.GetRequiredService<ISensorLifecycleService>();
            trackingViewModel    = sp.GetRequiredService<TrackingViewModel>();
            diagnosticsViewModel = sp.GetRequiredService<DiagnosticsViewModel>();

            // Fix #4: sensor hardware starts with the app — not gated behind Start button.
            // TrackingViewModel controls whether the tracker processes data, not the hardware.
            lifecycleService.Activate();

            var window = new Window(new AppShell());
            window.Destroying += (_, _) =>
            {
                lifecycleService?.Deactivate();
                trackingViewModel?.Dispose();
                diagnosticsViewModel?.Dispose();
            };
            return window;
        }
    }
}
