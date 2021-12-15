using DSharpPlus.VoiceNext;
using Microsoft.CognitiveServices.Speech;
using NAudio.Wave;
using Serilog;

namespace TtsBot
{
    public sealed class AzureSpeechService : IDisposable
    {
        private class AudioDataStreamWrapper : Stream
        {
            private readonly AudioDataStream _dataStream;

            public AudioDataStreamWrapper(AudioDataStream dataStream) {
                _dataStream = dataStream;
            }

            public override void Flush() {
                throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count) {
                byte[] buf1 = new byte[count];
                uint readData = _dataStream.ReadData(buf1);
                // Log.Debug("Read from azure {Read}", readData);
                Array.Copy(buf1, 0, buffer, offset, readData);
                return (int)readData;
            }

            public override long Seek(long offset, SeekOrigin origin) {
                throw new NotSupportedException();
            }

            public override void SetLength(long value) {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count) {
                throw new NotSupportedException();
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position {
                get => _dataStream.GetPosition();
                set => throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing) {
                _dataStream.Dispose();
            }
        }

        private class WaveProviderWrapperStream : Stream
        {
            private readonly IWaveProvider _dataStream;

            public WaveProviderWrapperStream(IWaveProvider dataStream) {
                _dataStream = dataStream;
            }

            public override void Flush() {
                throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count) {
                int read = _dataStream.Read(buffer, offset, count);
                // Log.Debug("Read from resample {Read}", read);
                return read;
            }

            public override long Seek(long offset, SeekOrigin origin) {
                throw new NotSupportedException();
            }

            public override void SetLength(long value) {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count) {
                throw new NotSupportedException();
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing) {
            }
        }

        private readonly SpeechSynthesizer _synthesizer;

        public AzureSpeechService() {
            if (TtsBotConfig.Config == null) throw new InvalidOperationException("Azure key required");
            SpeechConfig speechConfig = SpeechConfig.FromSubscription(TtsBotConfig.Config.AzureKey, "eastus");
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw48Khz16BitMonoPcm);
            _synthesizer = new SpeechSynthesizer(speechConfig, null);
        }

        public async Task SynthesizeAndWriteAsync(string ssml, VoiceTransmitSink stream) {
            try {
                SpeechSynthesisResult result = await _synthesizer.StartSpeakingSsmlAsync(ssml);
                switch (result.Reason) {
                    case ResultReason.Canceled:
                        throw new Exception(
                            $"{result.Reason}, {result.ResultId} ({SpeechSynthesisCancellationDetails.FromResult(result)})");
                    case ResultReason.SynthesizingAudioCompleted:
                    case ResultReason.SynthesizingAudioStarted:
                    case ResultReason.SynthesizingAudio:
                        break;
                    default:
                        throw new Exception($"{result.Reason}, {result.ResultId}");
                }
                Log.Debug("Synthesis started {ResultReason} {ResultId}", result.Reason, result.ResultId);
                using AudioDataStream dataStream = AudioDataStream.FromResult(result);
                await using AudioDataStreamWrapper wrapper = new(dataStream);
                RawSourceWaveStream raw = new(wrapper, new WaveFormat(48000, 16, 1));
                MonoToStereoProvider16 stereo = new(raw);
                WaveProviderWrapperStream waveStream = new(stereo);
                await waveStream.CopyToAsync(stream);
            }
            catch (Exception e) {
                Log.Fatal(e, "error in speech synth");
                throw;
            }
        }

        public void Dispose() {
            _synthesizer.Dispose();
        }
    }
}