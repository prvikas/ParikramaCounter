using Microsoft.Extensions.Logging;
using ParikramaCounter.Repositories;
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
                    fonts.AddFont("OpenSans-Regular.ttf",  "OpenSansRegular");
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

            // Issue #4: standard two-type registration — no unnecessary lambda factory.
            // The container resolves ISensorService from its own registration automatically.
            builder.Services.AddSingleton<ISensorFusionEngine, SensorFusionEngine>();
            builder.Services.AddSingleton<ISensorLifecycleService, SensorLifecycleService>();
            builder.Services.AddSingleton<IAppPreferences, AppPreferences>();
            builder.Services.AddSingleton<IVibrationService, VibrationService>();
            builder.Services.AddSingleton<ISessionRepository, JsonSessionRepository>();

            // Issue #3: registered against IPradhakshinaSessionService so consumers
            // depend on the abstraction and the service is independently testable.
            builder.Services.AddSingleton<IPradhakshinaSessionService, PradhakshinaSessionService>();

            // ViewModels — standard registration; DI resolves constructor params automatically
            builder.Services.AddSingleton<SettingsViewModel>();
            builder.Services.AddSingleton<TrackingViewModel>();
            builder.Services.AddSingleton<DiagnosticsViewModel>();

            builder.Services.AddTransient<Views.TrackingPage>();
            builder.Services.AddTransient<Views.DiagnosticsPage>();
            builder.Services.AddTransient<Views.SettingsPage>();

            return builder.Build();
        }
    }
}
