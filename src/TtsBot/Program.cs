using System.Runtime.CompilerServices;
using Disqord.Bot.Hosting;
using Disqord.Hosting;
using Disqord.Voice;
using Disqord.Voice.Default;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qmmands;
using Serilog;
using TtsBot;
using VoiceExtension = TtsBot.VoiceExtension;

Log.Logger = new LoggerConfiguration()
            .WriteTo.Async(s => s.Console())
            .CreateBootstrapLogger();

try {
    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    AsHostBuilder(builder)
       .UseSerilog((context, provider, conf) =>
            conf.ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(provider)
                .WriteTo.Async(s => s.Console())
                .Enrich.FromLogContext())
       .ConfigureDiscordBot((context, bot) => { bot.ServiceAssemblies.Clear(); });

    if (OperatingSystem.IsWindows())
        builder.Services.AddVoiceSynchronizer<MultimediaTimerVoiceSynchronizer>();
    else // AddVoice uses thread pool timer, i prefer periodictimer
        builder.Services.AddVoiceSynchronizer<PeriodicTimerVoiceSynchronizer>();

    builder.Services.AddVoice()
           .AddDiscordClientService<VoiceExtension>()
           .AddDiscordClientService<TtsMessageHandling>()
           .AddCommandService();
    builder.Services.AddSingleton<AudioRecoder>();

    await builder.Build().RunAsync();

    return 0;
}
catch (Exception e) {
    Log.Fatal(e, "Fatal error in application host");
    return e.HResult;
}
finally {
    await Log.CloseAndFlushAsync();
}

[UnsafeAccessor(UnsafeAccessorKind.Method)]
static extern IHostBuilder AsHostBuilder(HostApplicationBuilder builder);
