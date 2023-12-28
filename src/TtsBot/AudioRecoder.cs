using System.Buffers;
using Concentus.Enums;
using Concentus.Structs;
using Disqord.Voice;

namespace TtsBot;

public sealed class AudioRecoder
{
    public const int BitRate              = 48000;
    public const int FrameSize            = 960;
    public const int PacketBufferSizeHint = 2048;

    private readonly OpusEncoder _encoder = new(BitRate, 2, OpusApplication.OPUS_APPLICATION_VOIP) {
        ExpertFrameDuration = OpusFramesize.OPUS_FRAMESIZE_20_MS,
        SignalType = OpusSignal.OPUS_SIGNAL_VOICE,
    };

    public int RecodePcmMonoToOpus(ReadOnlySpan<short> inputMonoPcm, ReadOnlySpanAction<byte, object?> packetConsumer) {
        int samplesConsumed = 0;

        short[]? monoPcmBuffer = null;
        try {
            using (ArrayPool<short>.Shared.Lease(FrameSize * 2, out short[] interleaveBuffer))
            using (ArrayPool<byte>.Shared.Lease(PacketBufferSizeHint, out byte[] packetBuffer)) {
                while (!inputMonoPcm.IsEmpty) {
                    ReadOnlySpan<short> b;
                    if (inputMonoPcm.Length >= FrameSize) {
                        // consume frame
                        samplesConsumed += FrameSize;
                        b = inputMonoPcm[..FrameSize];
                        inputMonoPcm = inputMonoPcm[FrameSize..];
                    } else {
                        // pad with silence to full frame
                        monoPcmBuffer ??= ArrayPool<short>.Shared.Rent(FrameSize);
                        Array.Clear(monoPcmBuffer);
                        samplesConsumed += inputMonoPcm.Length;
                        inputMonoPcm.CopyTo(monoPcmBuffer);
                        inputMonoPcm = ReadOnlySpan<short>.Empty;
                        b = monoPcmBuffer;
                    }

                    InterleavingNonsense.Interleave(interleaveBuffer, b);
                    int len = _encoder.Encode(interleaveBuffer.AsSpan(0, FrameSize * 2), FrameSize, packetBuffer,
                        packetBuffer.Length);
                    packetConsumer(packetBuffer.AsSpan(0, len), null);
                }
            }
        }
        finally {
            if (monoPcmBuffer != null)
                ArrayPool<short>.Shared.Return(monoPcmBuffer);
        }

        return samplesConsumed;
    }

    public void FinishAudioBlock(ReadOnlySpanAction<byte, object?> packetConsumer) {
        packetConsumer(VoiceConstants.SilencePacket.Span, null);
        packetConsumer(VoiceConstants.SilencePacket.Span, null);
        packetConsumer(VoiceConstants.SilencePacket.Span, null);
        packetConsumer(VoiceConstants.SilencePacket.Span, null);
        packetConsumer(VoiceConstants.SilencePacket.Span, null);
    }
}