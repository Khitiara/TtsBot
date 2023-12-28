using Disqord;
using Microsoft.Extensions.Logging;

namespace TtsBot;

public static partial class Logs {
    [LoggerMessage(LogLevel.Debug, "Setting voice state for guild ID: {GuildId} to channel ID: {ChannelId}")]
    public static partial void LogSetVoiceState(ILogger logger, Snowflake guildId, Snowflake? channelId);
}
