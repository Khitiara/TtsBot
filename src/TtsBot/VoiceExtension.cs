using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Disqord;
using Disqord.Gateway;
using Disqord.Gateway.Api;
using Disqord.Hosting;
using Disqord.Voice;
using Microsoft.Extensions.Logging;

namespace TtsBot;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public class VoiceExtension : DiscordClientService {
    private readonly IVoiceConnectionFactory                           _connectionFactory;
    private readonly ConcurrentDictionary<Snowflake, IVoiceConnection> _connections;
    private readonly SetVoiceStateDelegate                             _setVoiceStateDelegateAsyncDelegate;

    public IReadOnlyDictionary<Snowflake, IVoiceConnection> Connections => _connections.AsReadOnly();

    public VoiceExtension(ILogger<VoiceExtension> logger, IVoiceConnectionFactory connectionFactory,
        DiscordClientBase client)
        : base(logger, client) {
        _connectionFactory = connectionFactory;
        _connections =
            new ConcurrentDictionary<Snowflake, IVoiceConnection>();
        _setVoiceStateDelegateAsyncDelegate = SetVoiceStateDelegateAsync;
    }

    protected override ValueTask OnVoiceServerUpdated(VoiceServerUpdatedEventArgs e) {
        ArgumentNullException.ThrowIfNull(e);
        if (_connections.TryGetValue(e.GuildId, out IVoiceConnection? voiceConnection))
            voiceConnection.OnVoiceServerUpdate(e.Token, e.Endpoint);
        return new ValueTask();
    }

    protected override ValueTask OnVoiceStateUpdated(VoiceStateUpdatedEventArgs e) {
        ArgumentNullException.ThrowIfNull(e);
        if (Client.CurrentUser.Id != e.NewVoiceState.MemberId)
            return new ValueTask();
        if (_connections.TryGetValue(e.GuildId, out IVoiceConnection? voiceConnection)) {
            IVoiceState newVoiceState = e.NewVoiceState;
            voiceConnection.OnVoiceStateUpdate(newVoiceState.ChannelId, newVoiceState.SessionId);
        }

        return new ValueTask();
    }

    public async ValueTask<IVoiceConnection> ConnectAsync(
        Snowflake guildId,
        Snowflake channelId,
        CancellationToken cancellationToken = default) {
        VoiceExtension voiceExtension = this;
        IVoiceConnection connection = voiceExtension._connectionFactory.Create(guildId, channelId,
            voiceExtension.Client.CurrentUser.Id, _setVoiceStateDelegateAsyncDelegate);
        voiceExtension._connections[guildId] = connection;
        Task readyTask = connection.WaitUntilReadyAsync(cancellationToken);
        _ = connection.RunAsync(voiceExtension.Client.StoppingToken);
        await readyTask.ConfigureAwait(false);
        return connection;
    }

    private ValueTask SetVoiceStateDelegateAsync(Snowflake g, Snowflake? c, CancellationToken token) {
        IShard? shard = Client.ApiClient.GetShard(g);
        if (shard == null)
            throw new InvalidOperationException("The guild ID is not handled by any of the shards of the client");

        Logs.LogSetVoiceState(Logger, g, c);
        return new ValueTask(shard.SetVoiceStateAsync(g, c, false, true, token));
    }
}
