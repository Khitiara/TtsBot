using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.VoiceNext;
using Microsoft.Extensions.Logging;
using Serilog;
using TtsBot;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

await TtsBotConfig.LoadAsync();
Log.Debug("{Config}", TtsBotConfig.Config);
using DiscordClient discordClient = new(new DiscordConfiguration {
    Token = TtsBotConfig.Config.DiscordKey,
    TokenType = TokenType.Bot,
    MinimumLogLevel = LogLevel.Debug,
    Intents = DiscordIntents.AllUnprivileged | DiscordIntents.GuildVoiceStates | DiscordIntents.GuildMembers |
              DiscordIntents.GuildMessages,
    LoggerFactory = new LoggerFactory().AddSerilog()
});
discordClient.UseVoiceNext(new VoiceNextConfiguration {
    AudioFormat = new AudioFormat(48000, 2, VoiceApplication.Voice)
});
CommandsNextExtension commandsNext = discordClient.UseCommandsNext(new CommandsNextConfiguration {
    UseDefaultCommandHandler = false
});

commandsNext.RegisterCommands<TtsCommandModule>();

TtsHandling.Handling = new(discordClient, new AzureSpeechService());
TtsHandling.Handling.Start();

await discordClient.ConnectAsync();
TaskCompletionSource tcs = new();

Console.CancelKeyPress += (_, eventArgs) => {
    if (tcs.TrySetResult())
        eventArgs.Cancel = true;
};

await tcs.Task;
await TtsHandling.Handling.CloseAsync();
await discordClient.DisconnectAsync();