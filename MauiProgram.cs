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

            // ── Platform sensor service ───────────────────────────────────────────
#if ANDROID
            builder.Services.AddSingleton<ISensorService, AndroidSensorService>();
#elif IOS
            builder.Services.AddSingleton<ISensorService, iOSSensorService>();
#endif

            // ── Fix #2/#8: engine uses interface + constructor injection ────────────
            builder.Services.AddSingleton<ISensorFusionEngine, SensorFusionEngine>(sp =>
                new SensorFusionEngine(sp.GetRequiredService<ISensorService>()));

            // ── Fix #4: declared sensor lifecycle owner ───────────────────────────
            builder.Services.AddSingleton<ISensorLifecycleService, SensorLifecycleService>();

            // ── Fix #6: centralised preferences ──────────────────────────────────
            builder.Services.AddSingleton<IAppPreferences, AppPreferences>();

            // ── Fix #5: vibration service ─────────────────────────────────────────
            builder.Services.AddSingleton<IVibrationService, VibrationService>();

            // ── Fix #12: session repository ───────────────────────────────────────
            builder.Services.AddSingleton<ISessionRepository, JsonSessionRepository>();

            // ── Fix #5: session service ───────────────────────────────────────────
            builder.Services.AddSingleton<PradhakshinaSessionService>();

            // ── ViewModels ────────────────────────────────────────────────────────
            builder.Services.AddSingleton<SettingsViewModel>();
            builder.Services.AddSingleton<TrackingViewModel>();
            builder.Services.AddSingleton<DiagnosticsViewModel>();

            // ── Pages ─────────────────────────────────────────────────────────────
            builder.Services.AddTransient<Views.TrackingPage>();
            builder.Services.AddTransient<Views.DiagnosticsPage>();
            builder.Services.AddTransient<Views.SettingsPage>();

            return builder.Build();
        }
    }
}
