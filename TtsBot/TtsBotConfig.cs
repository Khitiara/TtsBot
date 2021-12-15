using System.ComponentModel;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace TtsBot;

[XmlRoot("TtsBotOptions", Namespace = "http://khitiara.github.io/TtsBotOptions", IsNullable = false)]
public record TtsBotConfig([property: XmlAttribute("azure")] string AzureKey,
    [property: XmlAttribute("discord")] string DiscordKey,
    [property: XmlElement("Guild")] GuildDict Guilds)
{
    public TtsBotConfig() : this(null!, null!, new GuildDict()) { }

    public static readonly XmlSerializer Serializer =
        new(typeof(TtsBotConfig), new[] { typeof(TtsGuildConfig), typeof(GuildDict) });

    public static TtsBotConfig Config { get; set; } = null!;

    public const string FileName = "Options.xml";

    private const string Xmlns = "http://khitiara.github.io/TtsBotOptions";

    public static async Task LoadAsync() {
        await using FileStream stream = File.OpenRead(FileName);

        XmlSchemaSet schemas = new();
        schemas.Add(Xmlns,
            XmlReader.Create(typeof(TtsBotConfig).Assembly.GetManifestResourceStream("TtsBot.Options.xsd")!));
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings {
            Async = true,
            IgnoreWhitespace = true,
            IgnoreComments = true,
            Schemas = schemas,
            ValidationType = ValidationType.Schema
        });
        Config = (await Task.Run(() => (TtsBotConfig?)Serializer.Deserialize(reader)))!;
    }

    public static async Task SaveAsync() {
        await using FileStream stream = File.OpenWrite(FileName);

        await using XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings {
            Indent = true,
            IndentChars = "\t",
            Async = true
        });
        await Task.Run(() => Serializer.Serialize(writer, Config));
    }
}

public record TtsGuildConfig(
    [property: XmlAttribute("id")] ulong Guild,
    [property: XmlAttribute("voiceChannel")]
    ulong VoiceChannel,
    [property: XmlAttribute("textChannel")]
    ulong TextChannel,
    [property: XmlAttribute("fallbackVoice")] [DefaultValue("en-US-JennyNeural")]
    string FallbackVoice = "en-US-JennyNeural"
)
{
    public TtsGuildConfig() : this(0, 0, 0) { }
};