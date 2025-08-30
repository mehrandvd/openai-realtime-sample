using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging;
using RealtimeSample.BlazorHybrid.Services.Contracts;
#if WINDOWS
using RealtimeSample.BlazorHybrid.Platforms.Windows.Services;
#endif

namespace RealtimeSample.BlazorHybrid
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
                });

            builder.Services.AddMauiBlazorWebView();

#if WINDOWS
            // Register Windows-specific microphone service implementation
            builder.Services.AddSingleton<IMicrophoneService, WindowsMicrophoneService>();
            builder.Services.AddSingleton<ISpeakerService, WindowsSpeakerService>();
#endif

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
