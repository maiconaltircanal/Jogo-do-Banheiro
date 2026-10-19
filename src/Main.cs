using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace JogoDoBanheiro
{
    // Textos e parâmetros do jogo. Lidos de config.ini (criado automaticamente ao lado do .exe).
    public class Config
    {
        public string Titulo = "JOGO DO BANHEIRO";
        public string Subtitulo = "Entre no banheiro e escolha onde você vai mijar.";
        public string MsgAprovado = "MACHO!";
        public string DetalheAprovado = "Respeitou o código sagrado do mictório em todas as cenas.";
        public string MsgReprovado = "VOCÊ É GAY!";
        public string DetalheReprovado = "Escolheu mictório colado em outro cara.";
        public int Cenas = 5;
        public int MinMictorios = 2;
        public int MaxMictorios = 10;

        public static Config Load(string path)
        {
            Config c = new Config();
            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, c.ToIni(), new UTF8Encoding(true));
                    return c;
                }
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
                    switch (k)
                    {
                        case "titulo": c.Titulo = v; break;
                        case "subtitulo": c.Subtitulo = v; break;
                        case "msg_aprovado": c.MsgAprovado = v; break;
                        case "detalhe_aprovado": c.DetalheAprovado = v; break;
                        case "msg_reprovado": c.MsgReprovado = v; break;
                        case "detalhe_reprovado": c.DetalheReprovado = v; break;
                        case "cenas": c.Cenas = ParseInt(v, c.Cenas, 1, 10); break;
                        case "min_mictorios": c.MinMictorios = ParseInt(v, c.MinMictorios, 2, 12); break;
                        case "max_mictorios": c.MaxMictorios = ParseInt(v, c.MaxMictorios, 2, 12); break;
                    }
                }
                if (c.MaxMictorios < c.MinMictorios) c.MaxMictorios = c.MinMictorios;
            }
            catch { }
            return c;
        }

        static int ParseInt(string s, int def, int min, int max)
        {
            int r;
            if (!int.TryParse(s, out r)) return def;
            return Math.Max(min, Math.Min(max, r));
        }

        public string ToIni()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Configuração do Jogo do Banheiro");
            sb.AppendLine("# Edite os textos à vontade. Use \\n para quebrar linha.");
            sb.AppendLine("titulo=" + Titulo);
            sb.AppendLine("subtitulo=" + Subtitulo);
            sb.AppendLine("msg_aprovado=" + MsgAprovado);
            sb.AppendLine("detalhe_aprovado=" + DetalheAprovado);
            sb.AppendLine("msg_reprovado=" + MsgReprovado);
            sb.AppendLine("detalhe_reprovado=" + DetalheReprovado);
            sb.AppendLine("# Quantidade de cenas (1 a 10)");
            sb.AppendLine("cenas=" + Cenas);
            sb.AppendLine("# Faixa de mictórios por cena (2 a 12)");
            sb.AppendLine("min_mictorios=" + MinMictorios);
            sb.AppendLine("max_mictorios=" + MaxMictorios);
            return sb.ToString();
        }
    }

    public class Scene
    {
        public int N;
        public bool[] Occupied;
        public Color[] Shirts;
        public int Chosen = -1;

        // Regra principal: nunca escolher um mictório ao lado de um que está sendo usado.
        public bool IsAdjacentToOccupied(int i)
        {
            return (i > 0 && Occupied[i - 1]) || (i < N - 1 && Occupied[i + 1]);
        }

        public bool Correct
        {
            get { return Chosen >= 0 && !Occupied[Chosen] && !IsAdjacentToOccupied(Chosen); }
        }

        public int OccupiedCount
        {
            get { int c = 0; foreach (bool o in Occupied) if (o) c++; return c; }
        }
    }

    public static class Generator
    {
        static readonly Color[] Palette = {
            Color.FromArgb(200, 60, 60), Color.FromArgb(60, 120, 200), Color.FromArgb(70, 160, 90),
            Color.FromArgb(150, 80, 170), Color.FromArgb(230, 130, 40), Color.FromArgb(60, 60, 70),
            Color.FromArgb(40, 150, 160), Color.FromArgb(170, 110, 70)
        };

        public static List<Scene> MakeGame(Random rnd, int count, int min, int max)
        {
            // Tamanhos variados (sem repetir enquanto der), em ordem crescente de dificuldade.
            List<int> sizes = new List<int>();
            List<int> pool = new List<int>();
            while (sizes.Count < count)
            {
                if (pool.Count == 0) for (int n = min; n <= max; n++) pool.Add(n);
                int idx = rnd.Next(pool.Count);
                sizes.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            sizes.Sort();
            List<Scene> scenes = new List<Scene>();
            foreach (int n in sizes) scenes.Add(Make(rnd, n));
            return scenes;
        }

        // Sorteia ocupados garantindo que exista pelo menos uma escolha certa
        // e (quando possível) pelo menos uma armadilha colada em alguém.
        public static Scene Make(Random rnd, int n)
        {
            while (true)
            {
                Scene s = new Scene();
                s.N = n;
                s.Occupied = new bool[n];
                double p = 0.25 + rnd.NextDouble() * 0.35;
                for (int i = 0; i < n; i++) s.Occupied[i] = rnd.NextDouble() < p;

                int safe = 0, trap = 0;
                for (int i = 0; i < n; i++)
                {
                    if (s.Occupied[i]) continue;
                    if (s.IsAdjacentToOccupied(i)) trap++; else safe++;
                }
                if (safe == 0) continue;
                if (n >= 3 && (s.OccupiedCount == 0 || trap == 0)) continue;

                s.Shirts = new Color[n];
                for (int i = 0; i < n; i++) s.Shirts[i] = Palette[rnd.Next(Palette.Length)];
                return s;
            }
        }
    }

    public class GameForm : Form
    {
        enum State { Intro, Playing, Chosen, Result }

        const int W = 1100, H = 700, FLOOR_Y = 560;

        readonly Config cfg;
        Random rnd = new Random();
        List<Scene> scenes = new List<Scene>();
        Scene introScene;
        int current;
        State state = State.Intro;
        int hover = -1;
        Point mouse;
        readonly Timer timer = new Timer();

        Rectangle btnStart = new Rectangle(W / 2 - 130, 520, 260, 70);
        Rectangle btnAgain = new Rectangle(W / 2 - 280, 600, 260, 64);
        Rectangle btnExit = new Rectangle(W / 2 + 20, 600, 260, 64);

        readonly Font fHuge = new Font("Segoe UI", 64, FontStyle.Bold);
        readonly Font fTitle = new Font("Segoe UI", 44, FontStyle.Bold);
        readonly Font fBig = new Font("Segoe UI", 22, FontStyle.Bold);
        readonly Font fMed = new Font("Segoe UI", 16);
        readonly Font fMedB = new Font("Segoe UI", 16, FontStyle.Bold);
        readonly Font fSmall = new Font("Segoe UI", 12, FontStyle.Bold);

        public GameForm(Config c)
        {
            cfg = c;
            Text = c.Titulo;
            ClientSize = new Size(W, H);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = Color.Black;
            KeyPreview = true;
            introScene = Generator.Make(rnd, 6);
            timer.Interval = 1100;
            timer.Tick += OnTimer;
        }

        void StartGame()
        {
            scenes = Generator.MakeGame(rnd, cfg.Cenas, cfg.MinMictorios, cfg.MaxMictorios);
            current = 0;
            hover = -1;
            state = State.Playing;
            Invalidate();
        }

        void OnTimer(object sender, EventArgs e)
        {
            timer.Stop();
            current++;
            hover = -1;
            if (current >= scenes.Count)
            {
                state = State.Result;
                if (Approved()) System.Media.SystemSounds.Asterisk.Play();
                else System.Media.SystemSounds.Exclamation.Play();
            }
            else state = State.Playing;
            Invalidate();
        }

        bool Approved()
        {
            foreach (Scene s in scenes) if (!s.Correct) return false;
            return true;
        }

        // ---------- Entrada ----------

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            mouse = e.Location;
            int h = -1;
            bool overButton = false;
            if (state == State.Playing) h = HitUrinal(e.Location, scenes[current].N);
            else if (state == State.Intro) overButton = btnStart.Contains(e.Location);
            else if (state == State.Result) overButton = btnAgain.Contains(e.Location) || btnExit.Contains(e.Location);

            bool clickable = overButton || (h >= 0 && !scenes[current].Occupied[h]);
            Cursor = clickable ? Cursors.Hand : Cursors.Default;
            hover = h;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (state == State.Intro && btnStart.Contains(e.Location)) StartGame();
            else if (state == State.Result)
            {
                if (btnAgain.Contains(e.Location)) StartGame();
                else if (btnExit.Contains(e.Location)) Close();
            }
            else if (state == State.Playing)
            {
                Scene s = scenes[current];
                int i = HitUrinal(e.Location, s.N);
                if (i < 0) return;
                if (s.Occupied[i]) { System.Media.SystemSounds.Beep.Play(); return; }
                s.Chosen = i;
                state = State.Chosen;
                Cursor = Cursors.Default;
                timer.Start();
                Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
            else if (e.KeyCode == Keys.Enter && (state == State.Intro || state == State.Result)) StartGame();
        }

        // ---------- Geometria ----------

        static RectangleF SlotRect(int i, int n)
        {
            float margin = 60;
            float slot = Math.Min(140f, (W - 2 * margin) / n);
            float x0 = (W - slot * n) / 2f;
            return new RectangleF(x0 + i * slot, 170, slot, 450);
        }

        static float UrinalWidth(int n)
        {
            return Math.Min(74f, SlotRect(0, n).Width * 0.62f);
        }

        static int HitUrinal(Point p, int n)
        {
            for (int i = 0; i < n; i++)
                if (SlotRect(i, n).Contains(p)) return i;
            return -1;
        }

        // ---------- Atualização ----------

        public bool VerificarAtualizacao = true;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!VerificarAtualizacao) return;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                ReleaseInfo r = null;
                try { r = Updater.BuscarUltima(); }
                catch { return; } // sem internet ou sem Release publicada: segue o jogo normal
                if (!Updater.EhMaisNova(r)) return;
                try { BeginInvoke(new Action(delegate { OferecerAtualizacao(r); })); }
                catch (InvalidOperationException) { } // janela já fechada
            });
        }

        void OferecerAtualizacao(ReleaseInfo r)
        {
            string notas = Updater.NotasLimpas(r);
            string msg = "Nova versão disponível: " + r.Versao.ToString(3) +
                         "\nVocê tem a versão " + Updater.AtualTexto + "." +
                         (notas.Length > 0 ? "\n\nNovidades:\n" + notas : "") +
                         "\n\nAtualizar agora?";
            if (MessageBox.Show(this, msg, cfg.Titulo, MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;

            Cursor = Cursors.WaitCursor;
            Text = cfg.Titulo + " - baixando atualização...";
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string setup = null;
                string erro = null;
                try { setup = Updater.Baixar(r); }
                catch (Exception ex) { erro = ex.Message; }
                BeginInvoke(new Action(delegate
                {
                    Cursor = Cursors.Default;
                    Text = cfg.Titulo;
                    if (erro != null)
                    {
                        MessageBox.Show(this, "Não foi possível atualizar:\n" + erro, cfg.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    Updater.Instalar(setup);
                    Close(); // o instalador substitui o jogo e abre a versão nova
                }));
            });
        }

        // ---------- Desenho ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            Render(e.Graphics);
        }

        public void Render(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            DrawRoom(g);
            if (state == State.Intro) DrawIntro(g);
            else if (state == State.Result) DrawResult(g);
            else DrawScene(g);
        }

        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static void DrawText(Graphics g, string s, Font f, Color c, RectangleF r)
        {
            using (StringFormat sf = new StringFormat())
            using (SolidBrush b = new SolidBrush(c))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(s, f, b, r, sf);
            }
        }

        static void ShadowText(Graphics g, string s, Font f, Color c, RectangleF r)
        {
            RectangleF sh = r;
            sh.Offset(3, 3);
            DrawText(g, s, f, Color.FromArgb(160, 0, 0, 0), sh);
            DrawText(g, s, f, c, r);
        }

        void DrawRoom(Graphics g)
        {
            using (SolidBrush wall = new SolidBrush(Color.FromArgb(214, 230, 226)))
                g.FillRectangle(wall, 0, 0, W, FLOOR_Y);
            using (Pen grout = new Pen(Color.FromArgb(190, 210, 206), 2))
            {
                for (int x = 0; x < W; x += 46) g.DrawLine(grout, x, 0, x, FLOOR_Y);
                for (int y = 0; y < FLOOR_Y; y += 46) g.DrawLine(grout, 0, y, W, y);
            }
            using (SolidBrush stripe = new SolidBrush(Color.FromArgb(70, 130, 140)))
                g.FillRectangle(stripe, 0, 128, W, 14);

            const int tile = 50;
            using (SolidBrush a = new SolidBrush(Color.FromArgb(70, 72, 80)))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(105, 108, 118)))
            {
                for (int y = FLOOR_Y, row = 0; y < H; y += tile, row++)
                    for (int x = 0, col = 0; x < W; x += tile, col++)
                        g.FillRectangle(((row + col) % 2 == 0) ? a : b, x, y, tile, tile);
            }
            using (SolidBrush baseboard = new SolidBrush(Color.FromArgb(40, 60, 65)))
                g.FillRectangle(baseboard, 0, FLOOR_Y - 8, W, 10);
        }

        void DrawUrinal(Graphics g, float cx, float uw, int number)
        {
            using (SolidBrush pipe = new SolidBrush(Color.FromArgb(165, 170, 175)))
                g.FillRectangle(pipe, cx - 4, 228, 8, 62);
            using (SolidBrush btn = new SolidBrush(Color.FromArgb(200, 205, 210)))
                g.FillEllipse(btn, cx - 9, 232, 18, 18);

            RectangleF plate = new RectangleF(cx - uw / 2, 285, uw, 178);
            using (GraphicsPath path = RoundRect(plate, uw * 0.45f))
            {
                using (LinearGradientBrush lg = new LinearGradientBrush(plate, Color.White, Color.FromArgb(215, 222, 226), LinearGradientMode.Horizontal))
                    g.FillPath(lg, path);
                using (Pen border = new Pen(Color.FromArgb(150, 160, 165), 2))
                    g.DrawPath(border, path);
            }
            using (SolidBrush bowl = new SolidBrush(Color.FromArgb(205, 218, 224)))
                g.FillEllipse(bowl, cx - uw * 0.32f, 352, uw * 0.64f, 92);
            using (SolidBrush drain = new SolidBrush(Color.FromArgb(150, 160, 165)))
                g.FillEllipse(drain, cx - 6, 428, 12, 7);

            RectangleF tag = new RectangleF(cx - 17, 182, 34, 34);
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(40, 60, 65)))
                g.FillEllipse(tb, tag);
            DrawText(g, number.ToString(), fSmall, Color.White, tag);
        }

        void DrawPerson(Graphics g, float cx, float uw, Color shirt, bool player)
        {
            float s = uw / 70f;
            using (SolidBrush jeans = new SolidBrush(player ? Color.FromArgb(30, 30, 35) : Color.FromArgb(45, 65, 110)))
            {
                g.FillRectangle(jeans, cx - 16 * s, 495, 14 * s, FLOOR_Y + 30 - 495);
                g.FillRectangle(jeans, cx + 2 * s, 495, 14 * s, FLOOR_Y + 30 - 495);
            }
            using (SolidBrush shoe = new SolidBrush(Color.FromArgb(25, 25, 25)))
            {
                g.FillEllipse(shoe, cx - 20 * s, FLOOR_Y + 22, 20 * s, 12);
                g.FillEllipse(shoe, cx, FLOOR_Y + 22, 20 * s, 12);
            }
            RectangleF torso = new RectangleF(cx - 28 * s, 382, 56 * s, 128);
            using (GraphicsPath p = RoundRect(torso, 16 * s))
            using (SolidBrush sb = new SolidBrush(shirt))
                g.FillPath(sb, p);
            Color dark = Color.FromArgb(Math.Max(0, shirt.R - 40), Math.Max(0, shirt.G - 40), Math.Max(0, shirt.B - 40));
            using (SolidBrush arm = new SolidBrush(dark))
            {
                g.FillRectangle(arm, cx - 30 * s, 396, 9 * s, 70);
                g.FillRectangle(arm, cx + 21 * s, 396, 9 * s, 70);
            }
            using (SolidBrush skin = new SolidBrush(Color.FromArgb(232, 192, 158)))
            {
                g.FillRectangle(skin, cx - 7 * s, 370, 14 * s, 16);
                g.FillEllipse(skin, cx - 20 * s, 334, 40 * s, 42);
            }
            using (SolidBrush hair = new SolidBrush(Color.FromArgb(70, 45, 30)))
                g.FillPie(hair, cx - 21 * s, 331, 42 * s, 44, 180, 180);

            if (player)
            {
                RectangleF label = new RectangleF(cx - 36, 296, 72, 28);
                using (GraphicsPath lp = RoundRect(label, 8))
                using (SolidBrush lb = new SolidBrush(Color.FromArgb(240, 190, 30)))
                    g.FillPath(lb, lp);
                DrawText(g, "VOCÊ", fSmall, Color.Black, label);
            }
        }

        void DrawUrinalRow(Graphics g, Scene s, bool interactive)
        {
            float uw = UrinalWidth(s.N);
            for (int i = 0; i < s.N; i++)
            {
                RectangleF slot = SlotRect(i, s.N);
                float cx = slot.X + slot.Width / 2;
                if (interactive && state == State.Playing && i == hover && !s.Occupied[i])
                {
                    RectangleF hl = new RectangleF(slot.X + 4, 176, slot.Width - 8, 440);
                    using (GraphicsPath p = RoundRect(hl, 16))
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 80, 160, 255)))
                            g.FillPath(b, p);
                        using (Pen pen = new Pen(Color.FromArgb(200, 60, 130, 230), 3))
                            g.DrawPath(pen, p);
                    }
                }
                DrawUrinal(g, cx, uw, i + 1);
                if (s.Occupied[i]) DrawPerson(g, cx, uw, s.Shirts[i], false);
                else if (i == s.Chosen) DrawPerson(g, cx, uw, Color.FromArgb(240, 190, 30), true);
            }
        }

        void DrawHeader(Graphics g, string left, string center)
        {
            using (SolidBrush bar = new SolidBrush(Color.FromArgb(220, 25, 35, 40)))
                g.FillRectangle(bar, 0, 0, W, 90);
            using (StringFormat sf = new StringFormat())
            using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 190, 30)))
            {
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(left, fBig, b, new RectangleF(30, 0, 300, 90), sf);
            }
            DrawText(g, center, fMed, Color.White, new RectangleF(250, 0, W - 500, 90));

            // progresso
            int n = scenes.Count;
            float dot = 18, gap = 10;
            float x = W - 30 - n * (dot + gap) + gap;
            for (int i = 0; i < n; i++)
            {
                Color c = i < current ? Color.FromArgb(240, 190, 30)
                        : i == current ? Color.White : Color.FromArgb(90, 255, 255, 255);
                using (SolidBrush b = new SolidBrush(c))
                    g.FillEllipse(b, x + i * (dot + gap), 45 - dot / 2, dot, dot);
            }
        }

        void DrawScene(Graphics g)
        {
            Scene s = scenes[current];
            DrawUrinalRow(g, s, true);
            DrawHeader(g, "CENA " + (current + 1) + " DE " + scenes.Count, "Clique no mictório que você vai usar");

            if (state == State.Chosen)
            {
                RectangleF r = new RectangleF(W / 2 - 220, 104, 440, 56);
                using (GraphicsPath p = RoundRect(r, 14))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 25, 35, 40)))
                    g.FillPath(b, p);
                DrawText(g, "Escolha registrada...", fMedB, Color.White, r);
            }
            else if (hover >= 0 && s.Occupied[hover])
            {
                RectangleF r = new RectangleF(mouse.X - 60, mouse.Y - 50, 120, 34);
                using (GraphicsPath p = RoundRect(r, 10))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 180, 40, 40)))
                    g.FillPath(b, p);
                DrawText(g, "Ocupado!", fSmall, Color.White, r);
            }
        }

        void Overlay(Graphics g, int alpha)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 15, 20, 25)))
                g.FillRectangle(b, 0, 0, W, H);
        }

        void DrawButton(Graphics g, Rectangle r, string label, Color baseColor)
        {
            bool hot = r.Contains(mouse);
            Color c = hot ? ControlPaint.Light(baseColor, 0.3f) : baseColor;
            using (GraphicsPath p = RoundRect(r, 16))
            {
                using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(120, 0, 0, 0), 2)) g.DrawPath(pen, p);
            }
            DrawText(g, label, fBig, Color.Black, r);
        }

        void DrawIntro(Graphics g)
        {
            DrawUrinalRow(g, introScene, false);
            Overlay(g, 190);
            ShadowText(g, cfg.Titulo, fTitle, Color.FromArgb(240, 190, 30), new RectangleF(0, 70, W, 90));
            DrawText(g, cfg.Subtitulo, fMed, Color.White, new RectangleF(0, 160, W, 40));

            RectangleF box = new RectangleF(W / 2 - 330, 230, 660, 240);
            using (GraphicsPath p = RoundRect(box, 18))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 35, 45, 50))) g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(100, 255, 255, 255), 2)) g.DrawPath(pen, p);
            }
            string rules =
                "Você vai passar por " + cfg.Cenas + " cenas de banheiro.\n\n" +
                "Em cada uma, alguns mictórios já estão ocupados.\n" +
                "Clique no mictório que você escolheria.\n\n" +
                "No final, o veredito.";
            DrawText(g, rules, fMed, Color.White, box);

            DrawButton(g, btnStart, "COMEÇAR", Color.FromArgb(240, 190, 30));
            DrawText(g, "v" + Updater.AtualTexto, fSmall, Color.FromArgb(150, 255, 255, 255), new RectangleF(W - 110, H - 40, 100, 30));
        }

        void DrawResult(Graphics g)
        {
            Overlay(g, 215);
            bool ok = Approved();
            Color main = ok ? Color.FromArgb(70, 210, 100) : Color.FromArgb(255, 90, 170);
            ShadowText(g, ok ? cfg.MsgAprovado : cfg.MsgReprovado, fHuge, main, new RectangleF(0, 40, W, 120));
            DrawText(g, ok ? cfg.DetalheAprovado : cfg.DetalheReprovado, fMed, Color.White, new RectangleF(0, 165, W, 40));

            int hits = 0;
            foreach (Scene s in scenes) if (s.Correct) hits++;
            DrawText(g, "Acertos: " + hits + " de " + scenes.Count, fMedB, Color.FromArgb(240, 190, 30), new RectangleF(0, 210, W, 36));

            float top = 262, rowH = Math.Min(60f, 315f / scenes.Count);
            for (int i = 0; i < scenes.Count; i++)
            {
                Scene s = scenes[i];
                float y = top + i * rowH;
                RectangleF row = new RectangleF(140, y + 4, W - 280, rowH - 8);
                using (GraphicsPath p = RoundRect(row, 10))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 40, 50, 58)))
                    g.FillPath(b, p);

                using (StringFormat sf = new StringFormat())
                using (SolidBrush wb = new SolidBrush(Color.White))
                {
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("Cena " + (i + 1), fMedB, wb, new RectangleF(160, row.Y, 110, row.Height), sf);
                }

                // mini mapa: cinza = livre, azul = ocupado, amarelo = sua escolha
                float sq = Math.Min(28f, row.Height - 14), gap = 6;
                float mx = 280;
                for (int k = 0; k < s.N; k++)
                {
                    Color c = s.Occupied[k] ? Color.FromArgb(70, 120, 200)
                            : k == s.Chosen ? Color.FromArgb(240, 190, 30)
                            : Color.FromArgb(200, 205, 210);
                    RectangleF r = new RectangleF(mx + k * (sq + gap), row.Y + (row.Height - sq) / 2, sq, sq);
                    using (GraphicsPath p = RoundRect(r, 5))
                    using (SolidBrush b = new SolidBrush(c))
                        g.FillPath(b, p);
                }

                string verdict = s.Correct ? "✔  Manteve distância" : "✘  Colou do lado de alguém";
                Color vc = s.Correct ? Color.FromArgb(90, 220, 120) : Color.FromArgb(255, 110, 180);
                using (StringFormat sf = new StringFormat())
                using (SolidBrush vb = new SolidBrush(vc))
                {
                    sf.Alignment = StringAlignment.Far;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(verdict, fMedB, vb, new RectangleF(row.X, row.Y, row.Width - 20, row.Height), sf);
                }
            }

            DrawButton(g, btnAgain, "JOGAR DE NOVO", Color.FromArgb(240, 190, 30));
            DrawButton(g, btnExit, "SAIR", Color.FromArgb(200, 205, 210));
        }

        // Usado só para gerar imagens de prévia durante o desenvolvimento.
        public static void SavePreview(string path, string mode, int seed)
        {
            GameForm f = new GameForm(new Config());
            f.rnd = new Random(seed);
            f.introScene = Generator.Make(f.rnd, 6);
            if (mode != "intro")
            {
                f.StartGame();
                if (mode == "scene" || mode == "chosen")
                {
                    f.current = 3;
                    Scene s = f.scenes[3];
                    if (mode == "chosen")
                    {
                        for (int i = 0; i < s.N; i++) if (!s.Occupied[i]) { s.Chosen = i; break; }
                        f.state = State.Chosen;
                    }
                }
                else
                {
                    foreach (Scene s in f.scenes)
                    {
                        List<int> free = new List<int>();
                        for (int i = 0; i < s.N; i++) if (!s.Occupied[i]) free.Add(i);
                        s.Chosen = free[f.rnd.Next(free.Count)];
                    }
                    f.state = State.Result;
                }
            }
            using (Bitmap bmp = new Bitmap(W, H))
            {
                using (Graphics g = Graphics.FromImage(bmp)) f.Render(g);
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            f.Dispose();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Config c = Config.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini"));
            GameForm form = new GameForm(c);
            form.VerificarAtualizacao = Array.IndexOf(args, "--sem-atualizacao") < 0;
            Application.Run(form);
        }
    }
}
