using Microsoft.Maui.Controls;
using ParikramaCounter.ViewModels;

namespace ParikramaCounter
{
    public partial class App : Application
    {
        private readonly TrackingViewModel trackingViewModel;
        private readonly DiagnosticsViewModel diagnosticsViewModel;

        public App(TrackingViewModel trackingVm, DiagnosticsViewModel diagnosticsVm)
        {
            InitializeComponent();
            trackingViewModel    = trackingVm;
            diagnosticsViewModel = diagnosticsVm;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            // Fix #15: CleanUp() is not a real MAUI Application override and is
            // never called on iOS where the OS kills apps without lifecycle hooks.
            // Window.Destroying fires reliably on both Android and iOS when the
            // app window is closed or the process is about to be terminated.
            window.Destroying += (_, _) =>
            {
                trackingViewModel?.Dispose();
                diagnosticsViewModel?.Dispose();
            };

            return window;
        }
    }
}
