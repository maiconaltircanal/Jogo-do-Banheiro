using System;
using System.IO;
using System.Media;
using System.Text;

namespace JogoDoBanheiro
{
    // Tic-tac do cronômetro, gerado na memória (sem arquivos de áudio).
    public static class Sons
    {
        static SoundPlayer tic, tac;

        static SoundPlayer Criar(double freq)
        {
            try
            {
                SoundPlayer p = new SoundPlayer(new MemoryStream(Tom(freq, 0.04)));
                p.Load();
                return p;
            }
            catch { return null; }
        }

        public static void TicTac(bool alterna)
        {
            try
            {
                if (tic == null) { tic = Criar(1400); tac = Criar(900); }
                SoundPlayer p = alterna ? tac : tic;
                if (p != null) p.Play();
            }
            catch { }
        }

        static byte[] Tom(double freq, double dur)
        {
            const int rate = 22050;
            int n = (int)(rate * dur);
            MemoryStream ms = new MemoryStream();
            BinaryWriter w = new BinaryWriter(ms);
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2);
            w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2);
            w.Write((short)2); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
            for (int i = 0; i < n; i++)
            {
                double env = 1.0 - (double)i / n;
                w.Write((short)(Math.Sin(2 * Math.PI * freq * i / rate) * env * 0.45 * short.MaxValue));
            }
            w.Flush();
            return ms.ToArray();
        }
    }
}
