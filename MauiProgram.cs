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

            // ── Preferences — one instance, three interfaces ──────────────────────
            builder.Services.AddSingleton<AppPreferences>();
            builder.Services.AddSingleton<IAppPreferences>(sp    => sp.GetRequiredService<AppPreferences>());
            builder.Services.AddSingleton<ISessionState>(sp      => sp.GetRequiredService<AppPreferences>());
            builder.Services.AddSingleton<IUserPreferences>(sp   => sp.GetRequiredService<AppPreferences>());
            builder.Services.AddSingleton<ISensorConfiguration>(sp => sp.GetRequiredService<AppPreferences>());

            // ── Repositories ──────────────────────────────────────────────────────
            builder.Services.AddSingleton<ISessionRepository, JsonSessionRepository>();
            builder.Services.AddSingleton<ITempleRepository, JsonTempleRepository>();

            // ── Domain services ───────────────────────────────────────────────────
            builder.Services.AddSingleton<IPradhakshinaSessionService, PradhakshinaSessionService>();
            builder.Services.AddSingleton<ISensorPipeline, SensorPipeline>();

            // VibrationService subscribes to session events in its constructor;
            // resolve eagerly in App.xaml.cs so subscriptions exist before tracking.
            builder.Services.AddSingleton<IVibrationService, VibrationService>();

            // ── ViewModels ────────────────────────────────────────────────────────
            builder.Services.AddSingleton<TrackingViewModel>();
            builder.Services.AddSingleton<DiagnosticsViewModel>();
            builder.Services.AddSingleton<SettingsViewModel>();
            builder.Services.AddSingleton<TempleViewModel>();

            // ── Pages ─────────────────────────────────────────────────────────────
            builder.Services.AddTransient<Views.TrackingPage>();
            builder.Services.AddTransient<Views.DiagnosticsPage>();
            builder.Services.AddTransient<Views.SettingsPage>();
            builder.Services.AddTransient<Views.TemplePage>();

            return builder.Build();
        }
    }
}
