using System.Globalization;
using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.Entities;

namespace TtsBot;

public static class Utils
{
    public static async Task<bool> WaitOneAsync(this WaitHandle handle, int millisecondsTimeout,
        CancellationToken cancellationToken) {
        RegisteredWaitHandle? registeredHandle = null;
        CancellationTokenRegistration tokenRegistration = default;
        try {
            TaskCompletionSource<bool> tcs = new();
            registeredHandle = ThreadPool.RegisterWaitForSingleObject(handle,
                (state, timedOut) => ((TaskCompletionSource<bool>)state!).TrySetResult(!timedOut), tcs,
                millisecondsTimeout, true);
            tokenRegistration =
                cancellationToken.Register(state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(), tcs);
            return await tcs.Task;
        }
        finally {
            registeredHandle?.Unregister(null);
            await tokenRegistration.DisposeAsync();
        }
    }

    public static async Task<string> ResolveMentionsAndEmoji(DiscordClient client, DiscordMessage message, int startAt = 0) {
        string str = message.Content.Substring(startAt);
        DiscordGuild guild = message.Channel.Guild;
        foreach (Match match in new Regex("<@!?(\\d+)>", RegexOptions.ECMAScript).Matches(str).ToList()) {
            ulong userId = ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            DiscordMember member = await guild.GetMemberAsync(userId);
            str = str.Replace(match.Value, member.DisplayName);
        }

        foreach (Match match in new Regex("<@&(\\d+)>", RegexOptions.ECMAScript).Matches(str).ToList()) {
            ulong roleId = ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            str = str.Replace(match.Value, guild.GetRole(roleId).Name);
        }
        foreach (Match match in new Regex("<#(\\d+)>", RegexOptions.ECMAScript).Matches(str)) {
            ulong channelId = ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            str = str.Replace(match.Value, guild.GetChannel(channelId).Name);
        }
        
        foreach (Match match in new Regex("<a?:([a-zA-Z0-9_]+):(\\d+)>", RegexOptions.ECMAScript).Matches(str)) {
            ulong emojiId = ulong.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            DiscordEmoji emoji = DiscordEmoji.FromGuildEmote(client, emojiId);
            str = str.Replace(match.Value, emoji.Name.Replace(":", ""));
        }

        return str;
    }
}