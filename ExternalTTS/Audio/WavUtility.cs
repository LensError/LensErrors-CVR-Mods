using System;
using System.Text;

namespace ExternalTTS.Audio
{
    public struct WavData
    {
        public short[] Samples;
        public int Channels;
        public int SampleRate;

        public WavData(short[] samples, int channels, int sampleRate)
        {
            Samples = samples;
            Channels = channels;
            SampleRate = sampleRate;
        }
    }

    public static class WavUtility
    {
        public static bool TryParseWav(byte[] wavBytes, out WavData result)
        {
            result = default;
            if (wavBytes == null || wavBytes.Length < 44)
            {
                return false;
            }

            try
            {
                // Check RIFF header
                string riff = Encoding.ASCII.GetString(wavBytes, 0, 4);
                string wave = Encoding.ASCII.GetString(wavBytes, 8, 4);
                if (riff != "RIFF" || wave != "WAVE")
                {
                    return false;
                }

                int offset = 12;
                int format = 0;
                int channels = 1;
                int sampleRate = 48000;
                int bitsPerSample = 16;
                int dataOffset = -1;
                int dataLength = 0;

                while (offset + 8 <= wavBytes.Length)
                {
                    string chunkId = Encoding.ASCII.GetString(wavBytes, offset, 4);
                    int chunkSize = BitConverter.ToInt32(wavBytes, offset + 4);
                    offset += 8;

                    if (chunkSize < 0 || offset + chunkSize > wavBytes.Length)
                    {
                        chunkSize = wavBytes.Length - offset;
                    }

                    if (chunkId == "fmt ")
                    {
                        if (chunkSize >= 16)
                        {
                            format = BitConverter.ToInt16(wavBytes, offset);
                            channels = BitConverter.ToInt16(wavBytes, offset + 2);
                            sampleRate = BitConverter.ToInt32(wavBytes, offset + 4);
                            bitsPerSample = BitConverter.ToInt16(wavBytes, offset + 14);
                        }
                    }
                    else if (chunkId == "data")
                    {
                        dataOffset = offset;
                        dataLength = chunkSize;
                        break;
                    }

                    offset += chunkSize;
                }

                if (dataOffset < 0 || dataLength <= 0)
                {
                    return false;
                }

                short[] samples;

                if (format == 1) // PCM
                {
                    if (bitsPerSample == 16)
                    {
                        int sampleCount = dataLength / 2;
                        samples = new short[sampleCount];
                        Buffer.BlockCopy(wavBytes, dataOffset, samples, 0, sampleCount * 2);
                    }
                    else if (bitsPerSample == 24)
                    {
                        int sampleCount = dataLength / 3;
                        samples = new short[sampleCount];
                        for (int i = 0; i < sampleCount; i++)
                        {
                            int byteIdx = dataOffset + (i * 3);
                            int val = (wavBytes[byteIdx] << 8) | (wavBytes[byteIdx + 1] << 16) | (wavBytes[byteIdx + 2] << 24);
                            samples[i] = (short)(val >> 16);
                        }
                    }
                    else if (bitsPerSample == 8)
                    {
                        int sampleCount = dataLength;
                        samples = new short[sampleCount];
                        for (int i = 0; i < sampleCount; i++)
                        {
                            int b = wavBytes[dataOffset + i] - 128;
                            samples[i] = (short)(b * 256);
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (format == 3) // IEEE Float
                {
                    if (bitsPerSample == 32)
                    {
                        int sampleCount = dataLength / 4;
                        samples = new short[sampleCount];
                        for (int i = 0; i < sampleCount; i++)
                        {
                            float sample = BitConverter.ToSingle(wavBytes, dataOffset + (i * 4));
                            sample = Math.Clamp(sample, -1f, 1f);
                            samples[i] = (short)(sample * 32767f);
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                result = new WavData(samples, channels, sampleRate);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

