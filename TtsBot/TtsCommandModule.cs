using System.Diagnostics.CodeAnalysis;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;

namespace TtsBot
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public class TtsCommandModule : BaseCommandModule
    {
        [Command("setup")]
        [RequireOwnerOrPermission(Permissions.Administrator)]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public async Task SetupAsync(CommandContext context, DiscordChannel commandChannel,
            DiscordChannel voiceChannel) {
            await TtsHandling.Handling.AddOrChangeChannelsAsync(
                new TtsGuildConfig(context.Guild.Id, commandChannel.Id, voiceChannel.Id));
            await context.RespondAsync("TTS Services configured");
        }

        [Command("yeet")]
        [RequireOwnerOrPermission(Permissions.Administrator)]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public async Task YeetAsync(CommandContext context) {
            await TtsHandling.Handling.RemoveGuildAsync(context.Guild.Id);
        }

        [Command("speak")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public async Task StartSpeakingAsync(CommandContext context) {
            TtsHandling.Handling.StartSpeaking(context.Guild.Id, context.User.Id);
            await context.RespondAsync("Your messages will now be read aloud");
        }

        [Command("quiet")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public async Task StopSpeakingAsync(CommandContext context) {
            TtsHandling.Handling.StopSpeaking(context.Guild.Id, context.User.Id);
            await context.RespondAsync("Your messages will no longer be read aloud");
        }

        [Command("disconnect")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public async Task DisconnectAsync(CommandContext context) {
            await TtsHandling.Handling.QuitAsync(context.Guild.Id);
        }

        [Command("connect")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public async Task ConnectAsync(CommandContext context) {
            await TtsHandling.Handling.JoinAsync(context.Guild.Id);
        }

        [Command("set-text")]
        [RequireOwnerOrPermission(Permissions.Administrator)]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public async Task SetTextChannelAsync(CommandContext context, DiscordChannel commandChannel) {
            await TtsHandling.Handling.SetTextChan(context.Guild.Id, commandChannel.Id);
            await Task.WhenAll(
                context.RespondAsync(
                    $"Successfully set {commandChannel.Mention} as text/command channel."),
                TtsHandling.Handling.PrintInfoAsync(context.Guild));
        }

        [Command("set-voice")]
        [RequireOwnerOrPermission(Permissions.Administrator)]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public async Task SetVoiceAsync(CommandContext context, string voice) {
            await TtsHandling.Handling.SetVoiceAsync(context.Guild.Id, voice);
        }


        [Command("say")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        [SuppressMessage("ReSharper", "UnusedParameter.Global")]
        public async Task SayAsync(CommandContext context, [RemainingText] string text) {
            if (context is not {
                    Channel: { Type: ChannelType.Text } chan,
                    Guild: { } guild,
                    User: DiscordMember user,
                    Message: { } message,
                    Client: { } client
                }) return;

            int argPos;
            if ((argPos = message.GetStringPrefixLength(".")) == -1) return;

            await TtsHandling.Handling.DoSay(guild, chan, user, await Utils.ResolveMentionsAndEmoji(client, message, argPos + 4),
                true);
        }

        [Command("say-as")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        [SuppressMessage("ReSharper", "UnusedParameter.Global")]
        public async Task SayAsAsync(CommandContext context, string voice, [RemainingText] string text) {
            if (context is not {
                    Channel: { Type: ChannelType.Text } chan,
                    Guild: { } guild,
                    User: DiscordMember user,
                    Message: { } message,
                    Client: { } client
                }) return;

            int argPos;
            if ((argPos = message.GetStringPrefixLength(".")) == -1) return;

            await TtsHandling.Handling.DoSay(guild, chan, user, await Utils.ResolveMentionsAndEmoji(client, message, argPos + 8 + voice.Length),
                true, voice: voice);
        }

        [Command("say-in")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public async Task SayInAsync(CommandContext context, string lang, [RemainingText] string text) {
            if (context is not {
                    Channel: { Type: ChannelType.Text } chan,
                    Guild: { } guild,
                    User: DiscordMember user,
                    Message: { } message,
                    Client: { } client
                }) return;

            int argPos;
            if ((argPos = message.GetStringPrefixLength(".")) == -1) return;

            await TtsHandling.Handling.DoSay(guild, chan, user, await Utils.ResolveMentionsAndEmoji(client, message, argPos + 8 + lang.Length),
                true, lang: lang);
        }

        [Command("info")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public Task SayInfoAsync(CommandContext context) => TtsHandling.Handling.PrintInfoAsync(context.Guild);
    }
}