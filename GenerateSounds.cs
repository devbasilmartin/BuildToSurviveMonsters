using System;
using System.IO;

class SoundGenerator
{
    public static void Generate()
    {
        string assetDir = "Assets";
        Directory.CreateDirectory(assetDir);

        // Gunshot: 150ms high-pitched pop
        GenerateTone(Path.Combine(assetDir, "gunshot.wav"), frequency: 800, duration: 0.15f, volume: 0.7f);

        // Melee swing: brief whoosh
        GenerateTone(Path.Combine(assetDir, "melee.wav"), frequency: 400, duration: 0.1f, volume: 0.5f);

        // Zombie groan: low frequency growl
        GenerateTone(Path.Combine(assetDir, "zombie.wav"), frequency: 120, duration: 0.5f, volume: 0.4f);

        // Explosion: noise burst
        GenerateNoise(Path.Combine(assetDir, "explosion.wav"), duration: 0.4f, volume: 0.8f);

        // Boomer beep: short high tone
        GenerateTone(Path.Combine(assetDir, "beep.wav"), frequency: 1000, duration: 0.1f, volume: 0.6f);

        Console.WriteLine("Sounds generated!");
    }

    static void GenerateTone(string path, float frequency, float duration, float volume)
    {
        const int sampleRate = 44100;
        int samples = (int)(sampleRate * duration);
        byte[] pcm = new byte[samples * 2];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float fade = Math.Min(1f, Math.Min(t / 0.05f, (duration - t) / 0.05f)); // fade in/out
            float sample = MathF.Sin(2f * MathF.PI * frequency * t) * fade * volume;
            short val = (short)(sample * 32767);
            BitConverter.GetBytes(val).CopyTo(pcm, i * 2);
        }

        WriteWav(path, pcm, sampleRate);
    }

    static void GenerateNoise(string path, float duration, float volume)
    {
        const int sampleRate = 44100;
        int samples = (int)(sampleRate * duration);
        byte[] pcm = new byte[samples * 2];
        var rng = new Random();

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float fade = (duration - t) / 0.1f; // fade out
            fade = Math.Max(0, Math.Min(1, fade));
            float sample = ((rng.NextSingle() * 2) - 1) * fade * volume;
            short val = (short)(sample * 32767);
            BitConverter.GetBytes(val).CopyTo(pcm, i * 2);
        }

        WriteWav(path, pcm, sampleRate);
    }

    static void WriteWav(string path, byte[] pcm, int sampleRate)
    {
        using (var fs = File.Create(path))
        using (var bw = new BinaryWriter(fs))
        {
            int byteRate = sampleRate * 2;
            int blockAlign = 2;

            // RIFF header
            bw.Write(new char[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + pcm.Length);
            bw.Write(new char[] { 'W', 'A', 'V', 'E' });

            // fmt subchunk
            bw.Write(new char[] { 'f', 'm', 't', ' ' });
            bw.Write(16);           // subchunk1 size
            bw.Write((ushort)1);    // audio format (PCM)
            bw.Write((ushort)1);    // channels (mono)
            bw.Write(sampleRate);   // sample rate
            bw.Write(byteRate);     // byte rate
            bw.Write((ushort)blockAlign);
            bw.Write((ushort)16);   // bits per sample

            // data subchunk
            bw.Write(new char[] { 'd', 'a', 't', 'a' });
            bw.Write(pcm.Length);
            bw.Write(pcm);
        }
    }
}
