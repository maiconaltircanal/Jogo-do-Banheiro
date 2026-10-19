# Gera assets\icone.ico (16 a 256 px) e assets\icone.png desenhando o icone por codigo.
# Uso: powershell -ExecutionPolicy Bypass -File tools\gerar-icone.ps1

$ErrorActionPreference = "Stop"
$raiz = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force "$raiz\assets" | Out-Null

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class IconeJogo
{
    static GraphicsPath Round(float x, float y, float w, float h, float r)
    {
        GraphicsPath p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // Desenha em coordenadas 0..256 e escala para o tamanho pedido.
    public static Bitmap Desenhar(int s)
    {
        Bitmap bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            g.ScaleTransform(s / 256f, s / 256f);
            bool pequeno = s < 32;

            // fundo: azulejo verde-azulado, como a parede do jogo
            using (GraphicsPath bg = Round(6, 6, 244, 244, pequeno ? 40 : 52))
            {
                using (LinearGradientBrush lg = new LinearGradientBrush(new RectangleF(0, 0, 256, 256),
                    Color.FromArgb(80, 160, 170), Color.FromArgb(30, 60, 68), LinearGradientMode.Vertical))
                    g.FillPath(lg, bg);
                if (!pequeno)
                {
                    g.SetClip(bg);
                    using (Pen grout = new Pen(Color.FromArgb(45, 255, 255, 255), 3))
                        for (int i = 6 + 49; i < 250; i += 49)
                        {
                            g.DrawLine(grout, i, 0, i, 256);
                            g.DrawLine(grout, 0, i, 256, i);
                        }
                    g.ResetClip();
                }
            }

            // cano e botao da descarga
            if (!pequeno)
            {
                using (SolidBrush pipe = new SolidBrush(Color.FromArgb(200, 210, 215)))
                {
                    g.FillRectangle(pipe, 120, 22, 16, 44);
                    g.FillEllipse(pipe, 112, 22, 32, 22);
                }
            }

            // mictorio
            RectangleF plate = pequeno ? new RectangleF(60, 36, 136, 190) : new RectangleF(70, 58, 116, 164);
            using (GraphicsPath p = Round(plate.X, plate.Y, plate.Width, plate.Height, plate.Width * 0.46f))
            {
                using (LinearGradientBrush lg = new LinearGradientBrush(plate, Color.White, Color.FromArgb(205, 215, 222), LinearGradientMode.Horizontal))
                    g.FillPath(lg, p);
                using (Pen border = new Pen(Color.FromArgb(25, 45, 50), pequeno ? 12 : 6))
                    g.DrawPath(border, p);
            }
            float bw = plate.Width * 0.62f;
            using (SolidBrush bowl = new SolidBrush(Color.FromArgb(185, 205, 215)))
                g.FillEllipse(bowl, 128 - bw / 2, plate.Y + plate.Height * 0.42f, bw, plate.Height * 0.48f);

            // gota amarela (a cor de destaque do jogo)
            if (!pequeno)
            {
                using (GraphicsPath drop = new GraphicsPath())
                {
                    drop.AddBezier(196, 150, 178, 178, 170, 192, 170, 204);
                    drop.AddArc(170, 178, 52, 52, 180, -180);
                    drop.AddBezier(222, 204, 222, 192, 214, 178, 196, 150);
                    drop.CloseFigure();
                    using (SolidBrush gold = new SolidBrush(Color.FromArgb(245, 195, 35)))
                        g.FillPath(gold, drop);
                    using (Pen outline = new Pen(Color.FromArgb(25, 45, 50), 6))
                        g.DrawPath(outline, drop);
                }
                using (SolidBrush shine = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                    g.FillEllipse(shine, 182, 192, 10, 14);
            }
        }
        return bmp;
    }

    static void EscreverDib(Bitmap b, Stream s)
    {
        int n = b.Width;
        int maskRow = ((n + 31) / 32) * 4;
        BinaryWriter w = new BinaryWriter(s);
        w.Write(40); w.Write(n); w.Write(n * 2);
        w.Write((short)1); w.Write((short)32);
        w.Write(0); w.Write(n * n * 4 + maskRow * n);
        w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        for (int y = n - 1; y >= 0; y--)
            for (int x = 0; x < n; x++)
            {
                Color c = b.GetPixel(x, y);
                w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A);
            }
        for (int y = n - 1; y >= 0; y--)
        {
            byte[] row = new byte[maskRow];
            for (int x = 0; x < n; x++)
                if (b.GetPixel(x, y).A == 0) row[x / 8] |= (byte)(0x80 >> (x % 8));
            w.Write(row);
        }
        w.Flush();
    }

    public static void SalvarIco(string path, int[] tamanhos)
    {
        // 256 px vai como PNG; os menores como DIB 32 bits (o System.Drawing.Icon so le PNG no 256).
        byte[][] imgs = new byte[tamanhos.Length][];
        for (int i = 0; i < tamanhos.Length; i++)
            using (Bitmap b = Desenhar(tamanhos[i]))
            using (MemoryStream ms = new MemoryStream())
            {
                if (tamanhos[i] >= 256) b.Save(ms, ImageFormat.Png);
                else EscreverDib(b, ms);
                imgs[i] = ms.ToArray();
            }

        using (BinaryWriter w = new BinaryWriter(File.Create(path)))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)tamanhos.Length);
            int offset = 6 + 16 * tamanhos.Length;
            for (int i = 0; i < tamanhos.Length; i++)
            {
                int s = tamanhos[i];
                w.Write((byte)(s >= 256 ? 0 : s)); w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(imgs[i].Length); w.Write(offset);
                offset += imgs[i].Length;
            }
            foreach (byte[] img in imgs) w.Write(img);
        }
    }
}
"@

[IconeJogo]::SalvarIco("$raiz\assets\icone.ico", [int[]](16, 24, 32, 48, 64, 128, 256))
$png = [IconeJogo]::Desenhar(256); $png.Save("$raiz\assets\icone.png", [Drawing.Imaging.ImageFormat]::Png); $png.Dispose()
Write-Host "Gerado: assets\icone.ico e assets\icone.png"
