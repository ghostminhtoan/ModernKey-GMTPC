using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ModernKey.Core
{
    /// <summary>
    /// Bộ phát hiệu ứng âm thanh phím cơ Cyberpunk độ trễ siêu thấp (&lt;2ms) sử dụng native Windows Multimedia API (winmm.dll)
    /// Tự động sinh âm thanh PCM trong bộ nhớ mà không cần tệp tin WAV bên ngoài.
    /// </summary>
    public static class SoundManager
    {
        [DllImport("winmm.dll", SetLastError = true)]
        private static extern bool PlaySound(byte[] ptrToSound, IntPtr hmod, uint fdwSound);

        private const uint SND_ASYNC = 0x0001;
        private const uint SND_NODEFAULT = 0x0002;
        private const uint SND_MEMORY = 0x0004;

        private static byte[] _switchClickWav;
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;
            lock (_lock)
            {
                if (_initialized) return;
                _switchClickWav = GenerateCyberpunkClickWav();
                _initialized = true;
            }
        }

        public static void PlayKeyClick()
        {
            try
            {
                if (!_initialized) Initialize();
                if (_switchClickWav != null)
                {
                    PlaySound(_switchClickWav, IntPtr.Zero, SND_ASYNC | SND_MEMORY | SND_NODEFAULT);
                }
            }
            catch { }
        }

        public static void Cleanup()
        {
            _switchClickWav = null;
            _initialized = false;
        }

        /// <summary>
        /// Tạo âm thanh click cơ học Cyberpunk ngắn gọn (khoảng 22ms) dưới định dạng WAV 16-bit Mono 44.1kHz chuẩn.
        /// </summary>
        private static byte[] GenerateCyberpunkClickWav()
        {
            int sampleRate = 44100;
            int durationMs = 22;
            int numSamples = (sampleRate * durationMs) / 1000;

            short[] samples = new short[numSamples];
            Random rand = new Random(1337);

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;
                // Decay envelope dạng hàm mũ giảm dần cực nhanh
                double env = Math.Exp(-t * 220.0);

                // Tần số cơ sở âm thanh click: 2400Hz kết hợp sóng hài 4800Hz và nhiễu trắng nhẹ tạo tiếng switch cơ đanh
                double freq1 = 2400.0;
                double freq2 = 4800.0;
                double wave = Math.Sin(2.0 * Math.PI * freq1 * t) * 0.6 +
                              Math.Sin(2.0 * Math.PI * freq2 * t) * 0.3 +
                              (rand.NextDouble() * 2.0 - 1.0) * 0.1;

                short val = (short)(wave * env * 22000.0);
                samples[i] = val;
            }

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                int subChunk2Size = numSamples * 2; // 16-bit mono = 2 bytes per sample
                int chunkSize = 36 + subChunk2Size;

                // RIFF header
                bw.Write(new char[] { 'R', 'I', 'F', 'F' });
                bw.Write(chunkSize);
                bw.Write(new char[] { 'W', 'A', 'V', 'E' });

                // fmt subchunk
                bw.Write(new char[] { 'f', 'm', 't', ' ' });
                bw.Write(16); // Subchunk1Size for PCM
                bw.Write((short)1); // AudioFormat PCM = 1
                bw.Write((short)1); // NumChannels = 1
                bw.Write(sampleRate);
                bw.Write(sampleRate * 2); // ByteRate = SampleRate * NumChannels * BitsPerSample/8
                bw.Write((short)2); // BlockAlign = NumChannels * BitsPerSample/8
                bw.Write((short)16); // BitsPerSample = 16

                // data subchunk
                bw.Write(new char[] { 'd', 'a', 't', 'a' });
                bw.Write(subChunk2Size);

                for (int i = 0; i < numSamples; i++)
                {
                    bw.Write(samples[i]);
                }

                bw.Flush();
                return ms.ToArray();
            }
        }
    }
}
