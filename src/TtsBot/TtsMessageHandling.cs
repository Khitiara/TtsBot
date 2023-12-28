using System.Diagnostics.CodeAnalysis;
using Disqord.Bot;
using Disqord.Bot.Commands.Application;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Microsoft.Extensions.Logging;

namespace TtsBot;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public sealed class TtsMessageHandling(ILogger logger, DiscordBotBase bot) : DiscordBotService(logger, bot)
{
    protected override async ValueTask OnNonCommandReceived(BotMessageReceivedEventArgs e) {

    }

    protected override async ValueTask OnVoiceStateUpdated(VoiceStateUpdatedEventArgs e) {
        await base.OnVoiceStateUpdated(e);
    }

    protected override async ValueTask OnVoiceServerUpdated(VoiceServerUpdatedEventArgs e) {
        await base.OnVoiceServerUpdated(e);
    }
}

public sealed class TtsControlHandling : DiscordApplicationGuildModuleBase
{

}