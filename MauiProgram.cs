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
            builder.Logging.AddDebug();  // Fix #10: structured logging wired up
#endif

            // ── Platform sensor service ───────────────────────────────────────────
            // Fix #9: simulator detection for testing without a physical device
#if ANDROID
            if (Microsoft.Maui.Devices.DeviceInfo.DeviceType == Microsoft.Maui.Devices.DeviceType.Virtual)
                builder.Services.AddSingleton<ISensorService, MockSensorService>();
            else
                builder.Services.AddSingleton<ISensorService, AndroidSensorService>();
#elif IOS
            if (Microsoft.Maui.Devices.DeviceInfo.DeviceType == Microsoft.Maui.Devices.DeviceType.Virtual)
                builder.Services.AddSingleton<ISensorService, MockSensorService>();
            else
                builder.Services.AddSingleton<ISensorService, iOSSensorService>();
#else
            builder.Services.AddSingleton<ISensorService, MockSensorService>();
#endif

            // ── Core infrastructure ───────────────────────────────────────────────
            builder.Services.AddSingleton<ISensorFusionEngine, SensorFusionEngine>();
            builder.Services.AddSingleton<ISensorLifecycleService, SensorLifecycleService>();

            // ── Fix #3: split preferences registered as all three interfaces ──────
            builder.Services.AddSingleton<AppPreferences>();
            builder.Services.AddSingleton<IAppPreferences>(sp => sp.GetRequiredService<AppPreferences>());
            builder.Services.AddSingleton<ISessionState>(sp => sp.GetRequiredService<AppPreferences>());
            builder.Services.AddSingleton<IUserPreferences>(sp => sp.GetRequiredService<AppPreferences>());
            builder.Services.AddSingleton<ISensorConfiguration>(sp => sp.GetRequiredService<AppPreferences>());

            // ── Repositories ──────────────────────────────────────────────────────
            builder.Services.AddSingleton<ISessionRepository, JsonSessionRepository>();
            builder.Services.AddSingleton<ITempleRepository, JsonTempleRepository>();

            // ── Domain services ───────────────────────────────────────────────────
            // Fix #4: VibrationService is IDisposable — registered as singleton so
            // it can subscribe to session events once and live for app lifetime.
            builder.Services.AddSingleton<IVibrationService, VibrationService>();

            // Session service — single source of truth for count and target
            builder.Services.AddSingleton<IPradhakshinaSessionService, PradhakshinaSessionService>();

            // Fix #5: SensorPipeline owns the sensor→fusion→session loop
            builder.Services.AddSingleton<ISensorPipeline, SensorPipeline>();

            // ── ViewModels ────────────────────────────────────────────────────────
            // Fix #10: ILogger automatically resolved by the DI container
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
