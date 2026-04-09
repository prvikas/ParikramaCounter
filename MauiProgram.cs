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

#if ANDROID
            builder.Services.AddSingleton<ISensorService, AndroidSensorService>();
#elif IOS
            builder.Services.AddSingleton<ISensorService, iOSSensorService>();
#endif

            // Shared engine — wire SensorService reference after both are created
            builder.Services.AddSingleton<SensorFusionEngine>(sp =>
            {
                var engine  = new SensorFusionEngine();
                var svc     = sp.GetRequiredService<ISensorService>();
                engine.SensorService = svc;
                return engine;
            });

            // Settings loads persisted prefs on construction; created first so
            // TrackingViewModel can receive it at construction time
            builder.Services.AddSingleton<SettingsViewModel>(sp =>
                new SettingsViewModel(sp.GetRequiredService<SensorFusionEngine>()));

            // TrackingViewModel receives settings so it reads vibration config live
            builder.Services.AddSingleton<TrackingViewModel>(sp =>
                new TrackingViewModel(
                    sp.GetRequiredService<ISensorService>(),
                    sp.GetRequiredService<SensorFusionEngine>(),
                    sp.GetRequiredService<SettingsViewModel>()));

            // DiagnosticsViewModel reads heading from the shared TrackingViewModel
            builder.Services.AddSingleton<DiagnosticsViewModel>(sp =>
                new DiagnosticsViewModel(
                    sp.GetRequiredService<ISensorService>(),
                    sp.GetRequiredService<TrackingViewModel>()));

            // Pages
            builder.Services.AddTransient<Views.TrackingPage>();
            builder.Services.AddTransient<Views.DiagnosticsPage>();
            builder.Services.AddTransient<Views.SettingsPage>();

            return builder.Build();
        }
    }
}
