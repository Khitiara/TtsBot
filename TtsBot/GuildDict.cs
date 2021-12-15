using System.Collections.ObjectModel;

namespace TtsBot;

public sealed class GuildDict : KeyedCollection<ulong, TtsGuildConfig>
{
    protected override ulong GetKeyForItem(TtsGuildConfig item) {
        return item.Guild;
    }

    public override string ToString() {
        return $"GuildDict {{{string.Join(", ", this)}}}";
    }
}