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
//            builder.Services.AddSingleton<ISensorService, MockSensorService>(); // For testing
#endif

            // Register platform-specific sensor service
#if ANDROID
            builder.Services.AddSingleton<ISensorService, AndroidSensorService>();
#elif IOS
            builder.Services.AddSingleton<ISensorService, iOSSensorService>();
#endif

            // Register ViewModels
            builder.Services.AddSingleton<TrackingViewModel>();
            builder.Services.AddSingleton<DiagnosticsViewModel>();
            builder.Services.AddSingleton<SettingsViewModel>();

            // Register Pages
            builder.Services.AddTransient<Views.TrackingPage>();
            builder.Services.AddTransient<Views.DiagnosticsPage>();
            builder.Services.AddTransient<Views.SettingsPage>();
            builder.Services.AddSingleton<TempleProfileService>();

            return builder.Build();
        }
    }
}

