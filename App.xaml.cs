using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ParikramaCounter.ViewModels;

namespace ParikramaCounter
{
    public partial class App : Application
    {
        // MAUI resolves App via reflection using the DI container.
        // We store references only for the Destroying lifecycle hook.
        private TrackingViewModel    trackingViewModel;
        private DiagnosticsViewModel diagnosticsViewModel;

        // Parameterless constructor path for MAUI bootstrapper compatibility.
        // Real instances are pulled from the service provider after build.
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Resolve singletons now that the DI container is fully built
            trackingViewModel    = IPlatformApplication.Current?.Services.GetService<TrackingViewModel>();
            diagnosticsViewModel = IPlatformApplication.Current?.Services.GetService<DiagnosticsViewModel>();

            var window = new Window(new AppShell());

            window.Destroying += (_, _) =>
            {
                trackingViewModel?.Dispose();
                diagnosticsViewModel?.Dispose();
            };

            return window;
        }
    }
}
