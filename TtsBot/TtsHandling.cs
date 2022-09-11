using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Channels;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext;
using Kevsoft.Ssml;
using Serilog;

namespace TtsBot
{
    public class TtsHandling
    {
        private readonly AzureSpeechService _synthesizer;

        private static readonly ImmutableDictionary<string, string> DefaultLangs = ImmutableDictionary
            .Create<string, string>()
            .Add("en", "en-US-JennyNeural")
            .Add("fil", "Microsoft Server Speech Text to Speech Voice (fil-PH, BlessicaNeural)")
            .Add("fr", "fr-FR-DeniseNeural")
            .Add("de", "de-DE-KatjaNeural")
            .Add("el", "el-GR-AthinaNeural")
            .Add("hu", "hu-HU-NoemiNeural")
            .Add("it", "it-IT-ElsaNeural")
            .Add("jp", "ja-JP-NanamiNeural")
            .Add("ko", "ko-KR-SunHiNeural")
            .Add("pl", "pl-PL-AgnieszkaNeural")
            .Add("pt", "pt-PT-FernandaNeural")
            .Add("ro", "ro-RO-AlinaNeural")
            .Add("ru", "ru-RU-DariyaNeural")
            .Add("es", "es-CU-BelkysNeural")
            .Add("sv", "sv-SE-SofieNeural")
            .Add("ur", "ur-PK-UzmaNeural")
            .Add("cy", "cy-GB-NiaNeural")
            .Add("hr", "hr-HR-GabrijelaNeural")
            .Add("da", "da-DK-ChristelNeural")
            .Add("nl", "nl-NL-ColetteNeural");

        private class TtsRegistration
        {
            public TtsRegistration(ulong commandChannel,
                ulong audioChannel, string voice, ConcurrentDictionary<ulong, string?> speakers) {
                Voice = voice;
                CommandChannel = commandChannel;
                AudioChannel = audioChannel;
                Speakers = speakers;
                ReadCancellationTokenSource = new CancellationTokenSource();
                Messages = Channel.CreateBounded<string>(new BoundedChannelOptions(16) {
                    FullMode = BoundedChannelFullMode.DropWrite,
                    SingleReader = true
                });
            }

            public string Voice { get; set; }

            public ulong CommandChannel { get; set; }
            public ulong AudioChannel { get; set; }
            public VoiceNextConnection? Client { get; set; }
            public ConcurrentDictionary<ulong, string?> Speakers { get; }

            public Channel<string> Messages { get; }

            public CancellationTokenSource? ReadCancellationTokenSource { get; private set; }

            public Task? ReadingTask { get; private set; }

            public VoiceTransmitSink? Stream { get; private set; }

            public bool Connected { get; private set; }

            public void Run(Func<string, VoiceTransmitSink, Task> synthesizer, DiscordChannel chan) {
                ReadCancellationTokenSource = new CancellationTokenSource();
                ReadingTask = Task.Run(() => ReadMessagesAsync(synthesizer, chan, ReadCancellationTokenSource.Token),
                    ReadCancellationTokenSource.Token);
                ReadingTask.ContinueWith(_ => { ReadingTask = null; });
            }

            private async Task ReadMessagesAsync(Func<string, VoiceTransmitSink, Task> synthesizer, DiscordChannel chan,
                CancellationToken cancellationToken = default) {
                try {
                    Connected = true;
                    Client = await chan.ConnectAsync();
                    Log.Debug("Client created");
                    Stream = Client.GetTransmitSink();
                    while (await Messages.Reader.WaitToReadAsync(cancellationToken)) {
                        if (!Messages.Reader.TryRead(out string? ssml)) return;
                        await synthesizer(ssml, Stream);
                    }
                }
                catch (OperationCanceledException) {
                    Log.Information("Canceled audio");
                }
                catch (Exception ex) {
                    Log.Fatal(ex, "Error in voice handler");
                }
                finally {
                    Connected = false;
                    Stream?.Dispose();
                    Stream = null;
                    Client?.Dispose();
                    Client = null;
                }
            }
        }

        public TtsHandling(DiscordClient client,
            AzureSpeechService synthesizer) {
            Client = client;
            _synthesizer = synthesizer;
            _registrations = new ConcurrentDictionary<ulong, TtsRegistration>();
        }

        private readonly ConcurrentDictionary<ulong, TtsRegistration> _registrations;
        public static    TtsHandling                                  Handling = null!;
        public DiscordClient Client { get; }

        public void Start() {
            LoadConfig();

            Client.MessageCreated += ClientOnMessageReceived;
            Client.VoiceStateUpdated += ClientUserVoiceStateUpdated;
        }

        public async Task CloseAsync() {
            List<Task> readTasks = new();
            foreach (TtsRegistration value in _registrations.Values) {
                value.ReadCancellationTokenSource?.Cancel();
                if (value.ReadingTask != null)
                    readTasks.Add(value.ReadingTask);
            }

            await Task.WhenAny(Task.WhenAll(readTasks), Task.Delay(500));
        }


        private async Task ClientOnMessageReceived(DiscordClient sender, MessageCreateEventArgs e) {
            int argPos;
            if (e is not {
                    Author: DiscordMember { IsBot: false } author,
                    Channel: {
                        Type: ChannelType.Text or ChannelType.Voice,
                        Guild: { } guild
                    } channel,
                    Message: { } message
                }) return;
            if ((argPos = message.GetStringPrefixLength(".")) != -1) {
                CommandsNextExtension commandsNext = Client.GetCommandsNext();
                string raw = message.Content[argPos..];
                Command? cmd = commandsNext.FindCommand(raw, out string? args);
                CommandContext? ctx = commandsNext.CreateContext(message, ".", cmd, args);
                _ = Task.Run(async () => await commandsNext.ExecuteCommandAsync(ctx));
            } else {
                string content = await Utils.ResolveMentionsAndEmoji(Client, message);
                await DoSay(guild, channel, author, content);
            }
        }

        private async Task ClientUserVoiceStateUpdated(DiscordClient sender, VoiceStateUpdateEventArgs e) {
            if (!_registrations.TryGetValue(e.Guild.Id, out TtsRegistration? registration)) return;
            DiscordChannel channel = e.Guild.GetChannel(registration.AudioChannel);
            if (channel.Type != ChannelType.Voice) return;
            int count = channel.Users.Count();
            switch (count) {
                case > 0 when !registration.Connected:
                    await JoinAsync(e.Guild.Id, registration);
                    break;
                case <= 1 when registration.Connected:
                    await DisconnectAsync(registration);
                    break;
            }
        }

        private static async Task DisconnectAsync(TtsRegistration registration) {
            registration.ReadCancellationTokenSource?.Cancel();
            if (registration.ReadingTask != null)
                await registration.ReadingTask;
        }


        public async Task PrintInfoAsync(DiscordGuild guild) {
            if (!_registrations.TryGetValue(guild.Id, out TtsRegistration? registration)) return;
            if (guild.GetChannel(registration.CommandChannel) is not { Type: ChannelType.Text } channel) return;

            DiscordMember botUser = await guild.GetMemberAsync(Client.CurrentUser.Id);
            string botName = string.IsNullOrWhiteSpace(botUser.Nickname) ? botUser.Username : botUser.Nickname;

            DiscordEmbed embed = new DiscordEmbedBuilder()
                .WithTitle($"{botName} Usage")
                .WithColor(DiscordColor.Purple)
                .WithDescription(new StringBuilder()
                    .AppendFormat("TTS Services for {0}\n", guild.GetChannel(registration.AudioChannel).Mention)
                    .AppendLine("`.connect` - Make the bot connect to voice.")
                    .AppendLine("`.disconnect` - Make the bot disconnect from voice.")
                    .AppendLine("`.say <text>` - Make the bot say a single message aloud in voice.")
                    .AppendLine("`.speak` - The bot will read aloud all messages you send in this channel")
                    .AppendLine("until you use `.quiet`").ToString())
                .Build();
            await channel.SendMessageAsync("TTS Services operate from this channel.", embed);
        }

        public async Task DoSay(DiscordGuild guild, DiscordChannel channel, DiscordMember author, string content,
            bool once = false, string? voice = null, string? lang = null) {
            if (!_registrations.TryGetValue(guild.Id, out TtsRegistration? registration)) return;
            if (!registration.Connected) return;
            if (channel.Id != registration.CommandChannel) return;
            if (!once && !registration.Speakers.ContainsKey(author.Id)) return;
            if (!once && author.VoiceState?.Channel?.Id != registration.AudioChannel) return;

            const int limit = 210;

            // lang ??= registration.Speakers.GetValueOrDefault(author.Id);
            if (lang != null && DefaultLangs.ContainsKey(lang))
                voice ??= DefaultLangs[lang];
            
            voice ??= registration.Voice;

            string nick = string.IsNullOrWhiteSpace(author.Nickname) ? author.Username : author.Nickname;
            // Checking for links in the message and replacing them if necessary
            List<string> toFilter = content.Split().ToList();
            for (int i = 0; i < toFilter.Count; i++)
            {
                if (Uri.IsWellFormedUriString(toFilter[i], UriKind.Absolute))
                    toFilter[i] = "link";
            }
            content = string.Join(" ", toFilter);
            if (content.Length > limit)
                content = content[..limit];
            string nameSpeakable = EmojiOne.EmojiOne.ToShort($"{nick} says");
            
            string textToSpeak = EmojiOne.EmojiOne.ToShort(content);
            IFluentSay fluentSay = new Ssml().Say(nameSpeakable).AsVoice("en-US-JennyNeural").Say(textToSpeak).AsVoice(voice);
            string ssml = await fluentSay
                .ToStringAsync();
            Log.Debug("{Ssml}", ssml);
            await registration.Messages.Writer.WriteAsync(ssml);
        }

        public async Task AddOrChangeChannelsAsync(TtsGuildConfig config) {
            TtsBotConfig botConfig = TtsBotConfig.Config;
            botConfig.Guilds.Remove(config.Guild);
            botConfig.Guilds.Add(config);
            await TtsBotConfig.SaveAsync();
            if (_registrations.ContainsKey(config.Guild)) {
                TtsRegistration registration = _registrations[config.Guild];
                registration.CommandChannel = config.TextChannel;
                if (registration.AudioChannel != config.VoiceChannel) {
                    registration.AudioChannel = config.VoiceChannel;
                }
            } else {
                _registrations[config.Guild] = new TtsRegistration(config.TextChannel, config.VoiceChannel,
                    "en-US-JennyNeural",
                    new ConcurrentDictionary<ulong, string?>());
            }
        }

        private void LoadConfig() {
            _registrations.Clear();
            KeyedCollection<ulong, TtsGuildConfig> guildConfigs = TtsBotConfig.Config.Guilds;
            foreach ((ulong guild, ulong audioChannel, ulong commandChannel, string voice) in guildConfigs) {
                Log.Debug("Adding registration for {Guild}, {Text}, {Audio}", guild, commandChannel,
                    audioChannel);
                _registrations[guild] = new TtsRegistration(commandChannel, audioChannel, voice,
                    new ConcurrentDictionary<ulong, string?>());
            }
        }

        public void StartSpeaking(ulong guildId, ulong userId) {
            Log.Debug("Beginning tts for {UserId} in {GuildId}", userId, guildId);
            _registrations[guildId].Speakers[userId] = null;
        }

        public void StopSpeaking(ulong guildId, ulong userId) {
            Log.Debug("Ending tts for {UserId} in {GuildId}", userId, guildId);
            _registrations[guildId].Speakers.TryRemove(userId, out _);
        }

        public async Task QuitAsync(ulong guildId) {
            if (!_registrations.TryGetValue(guildId, out TtsRegistration? registration)) return;
            registration.ReadCancellationTokenSource?.Cancel();
            if (registration.ReadingTask != null)
                await registration.ReadingTask;
        }

        public async Task JoinAsync(ulong guildId) {
            Log.Debug("Join request for {Guild}", guildId);
            if (!_registrations.TryGetValue(guildId, out TtsRegistration? registration)) return;
            await JoinAsync(guildId, registration);
        }

        private async Task JoinAsync(ulong guildId, TtsRegistration registration) {
            Log.Debug("Joining {Channel}", registration.AudioChannel);
            registration.Run(_synthesizer.SynthesizeAndWriteAsync,
                (await Client.GetGuildAsync(guildId)).GetChannel(registration.AudioChannel));
        }

        public async Task RemoveGuildAsync(ulong guildId) {
            if (!_registrations.TryGetValue(guildId, out TtsRegistration? registration)) return;
            if (registration.Connected) {
                registration.ReadCancellationTokenSource?.Cancel();
                if (registration.ReadingTask != null)
                    await registration.ReadingTask;
            } else {
                _registrations.TryRemove(guildId, out registration);
                TtsBotConfig botConfig = TtsBotConfig.Config;
                botConfig.Guilds.Remove(guildId);
                await TtsBotConfig.SaveAsync();
            }
        }

        public async Task SetTextChan(ulong guildId, ulong commandChannelId) {
            if (!_registrations.TryGetValue(guildId, out TtsRegistration? registration)) return;
            registration.CommandChannel = commandChannelId;
            TtsBotConfig botConfig = TtsBotConfig.Config;
            if (!botConfig.Guilds.TryGetValue(guildId, out TtsGuildConfig? guildConfig)) return;
            botConfig.Guilds.Remove(guildId);
            botConfig.Guilds.Add(guildConfig with { TextChannel = commandChannelId });
            await TtsBotConfig.SaveAsync();
        }

        public async Task SetVoiceAsync(ulong guildId, string voice) {
            if (!_registrations.TryGetValue(guildId, out TtsRegistration? registration)) return;
            registration.Voice = voice;
            TtsBotConfig botConfig = TtsBotConfig.Config;
            if (!botConfig.Guilds.TryGetValue(guildId, out TtsGuildConfig? guildConfig)) return;
            botConfig.Guilds.Remove(guildId);
            botConfig.Guilds.Add(guildConfig with { FallbackVoice = voice });
            await TtsBotConfig.SaveAsync();
        }
    }
}