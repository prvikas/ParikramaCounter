using ParikramaCounter.ViewModels;

namespace ParikramaCounter
{
    public partial class App : Application
    {
        private readonly TrackingViewModel trackingViewModel;
        private readonly DiagnosticsViewModel diagnosticsViewModel;

        // Fix #19: MAUI's DI container does not call Dispose() on singleton services.
        // We inject the IDisposable ViewModels here and release them explicitly when
        // the application is closing, ensuring sensors stop and events unsubscribe.
        public App(TrackingViewModel trackingVm, DiagnosticsViewModel diagnosticsVm)
        {
            InitializeComponent();
            trackingViewModel    = trackingVm;
            diagnosticsViewModel = diagnosticsVm;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        protected override void CleanUp()
        {
            trackingViewModel?.Dispose();
            diagnosticsViewModel?.Dispose();
            base.CleanUp();
        }
    }
}
