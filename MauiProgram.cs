using Microsoft.Extensions.Logging;
using ParikramaCounter.Services;
using ParikramaCounter.ViewModels;

#if ANDROID
using ParikramaCounter.Platforms.Android;
#elif IOS
using ParikramaCounter.Platforms.iOS;
#endif

namespace ParikramaCounter
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Platform sensor service
#if ANDROID
            builder.Services.AddSingleton<ISensorService, AndroidSensorService>();
#elif IOS
            builder.Services.AddSingleton<ISensorService, iOSSensorService>();
#endif

            // Fix #4: SensorFusionEngine registered as singleton so SettingsViewModel
            // and TrackingViewModel share the same instance and live tuning works.
            builder.Services.AddSingleton<SensorFusionEngine>();

            // ViewModels
            builder.Services.AddSingleton<TrackingViewModel>();

            // Fix #3: DiagnosticsViewModel reads from TrackingViewModel for accurate
            // heading/status rather than running a private shadow fusion engine.
            builder.Services.AddSingleton<DiagnosticsViewModel>(sp =>
                new DiagnosticsViewModel(
                    sp.GetRequiredService<ISensorService>(),
                    sp.GetRequiredService<TrackingViewModel>()
                ));

            // Fix #4/#5/#6: SettingsViewModel receives shared engine for live tuning
            builder.Services.AddSingleton<SettingsViewModel>(sp =>
                new SettingsViewModel(sp.GetRequiredService<SensorFusionEngine>()));

            // Pages
            builder.Services.AddTransient<Views.TrackingPage>();
            builder.Services.AddTransient<Views.DiagnosticsPage>();
            builder.Services.AddTransient<Views.SettingsPage>();

            return builder.Build();
        }
    }
}
