using System.Buffers.Binary;
using System.Text;

namespace MitaAI.Plugin;

internal sealed record VoiceAudio(float[] Samples, int SampleRate, byte[] WaveBytes)
{
    internal float Seconds => Samples.Length / (float)SampleRate;
    internal const int MaximumWaveBytes = 3_000_000;

    internal static VoiceAudio Decode(byte[] wave)
    {
        if (wave.Length < 44 || wave.Length > MaximumWaveBytes
            || Encoding.ASCII.GetString(wave, 0, 4) != "RIFF"
            || Encoding.ASCII.GetString(wave, 8, 4) != "WAVE"
            || BinaryPrimitives.ReadUInt32LittleEndian(wave.AsSpan(4)) != wave.Length - 8)
            throw new InvalidDataException("Invalid voice WAV framing.");
        int rate = 0;
        bool format = false;
        int dataOffset = -1, dataLength = 0;
        for (int offset = 12; offset <= wave.Length - 8;)
        {
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(wave.AsSpan(offset + 4));
            if (length > wave.Length - offset - 8) throw new InvalidDataException("Truncated voice WAV.");
            int size = (int)length;
            string kind = Encoding.ASCII.GetString(wave, offset, 4);
            int start = offset + 8;
            if (kind == "fmt ")
            {
                if (format || size < 16 || BinaryPrimitives.ReadUInt16LittleEndian(wave.AsSpan(start)) != 1
                    || BinaryPrimitives.ReadUInt16LittleEndian(wave.AsSpan(start + 2)) != 1
                    || BinaryPrimitives.ReadUInt16LittleEndian(wave.AsSpan(start + 14)) != 16)
                    throw new InvalidDataException("Voice must be mono PCM16.");
                rate = BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(start + 4));
                if (rate != 24000 || BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(start + 8)) != rate * 2
                    || BinaryPrimitives.ReadUInt16LittleEndian(wave.AsSpan(start + 12)) != 2)
                    throw new InvalidDataException("Invalid voice WAV rate.");
                format = true;
            }
            else if (kind == "data")
            {
                if (dataOffset >= 0) throw new InvalidDataException("Duplicate voice data.");
                dataOffset = start;
                dataLength = size;
            }
            offset = checked(start + size + (size & 1));
        }
        if (!format || dataOffset < 0 || dataLength == 0 || dataLength % 2 != 0 || dataLength > rate * 60 * 2)
            throw new InvalidDataException("Missing or oversized voice data.");
        var samples = new float[dataLength / 2];
        bool nonzero = false;
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(dataOffset + i * 2));
            nonzero |= sample != 0;
            samples[i] = sample / 32768f;
        }
        if (!nonzero) throw new InvalidDataException("Silent voice audio.");
        return new VoiceAudio(samples, rate, wave);
    }
}

internal readonly record struct VoiceStamp(long RequestId, string Scene, int TargetId)
{
    internal bool Matches(long request, string scene, int target) => RequestId == request && Scene == scene
        && TargetId != 0 && TargetId == target;
}
