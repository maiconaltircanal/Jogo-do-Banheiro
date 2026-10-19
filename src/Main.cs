using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace JogoDoBanheiro
{
    public class GameForm : Form
    {
        enum State { Intro, Playing, Chosen, Result }

        const int W = 1100, H = 700, FLOOR_Y = 560;
        const int NONE = -1;

        static readonly Color Gold = Color.FromArgb(240, 190, 30);

        readonly Config cfg;
        Random rnd = new Random();
        List<Scene> scenes = new List<Scene>();
        Scene introScene;
        Resultado resultado;
        int current;
        State state = State.Intro;
        int hover = NONE;
        Point mouse;

        readonly Timer pauseTimer = new Timer();   // pausa depois de escolher
        readonly Timer clockTimer = new Timer();   // cronômetro da cena
        readonly Stopwatch clock = new Stopwatch();
        double previewElapsed = -1;
        int lastTick = int.MaxValue;

        Rectangle btnStart = new Rectangle(W / 2 - 130, 520, 260, 70);
        Rectangle btnAgain = new Rectangle(W / 2 - 280, 610, 260, 64);
        Rectangle btnExit = new Rectangle(W / 2 + 20, 610, 260, 64);

        readonly Font fHuge = new Font("Segoe UI", 60, FontStyle.Bold);
        readonly Font fTitle = new Font("Segoe UI", 44, FontStyle.Bold);
        readonly Font fBig = new Font("Segoe UI", 22, FontStyle.Bold);
        readonly Font fMed = new Font("Segoe UI", 16);
        readonly Font fMedB = new Font("Segoe UI", 16, FontStyle.Bold);
        readonly Font fSmall = new Font("Segoe UI", 12, FontStyle.Bold);
        readonly Font fTiny = new Font("Segoe UI", 10, FontStyle.Bold);

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
            try
            {
                using (Stream s = typeof(GameForm).Assembly.GetManifestResourceStream("icone.ico"))
                    if (s != null) Icon = new Icon(s);
            }
            catch { }
            introScene = MakeIntroScene(rnd);
            pauseTimer.Tick += OnPauseEnd;
            clockTimer.Interval = 40;
            clockTimer.Tick += OnClock;
        }

        static Scene MakeIntroScene(Random r)
        {
            Scene s = Generator.Make(r, SceneKind.Normal, 6, 6);
            s.HasStall = true;
            return s;
        }

        // ---------- Fluxo ----------

        void StartGame()
        {
            scenes = Generator.MakeGame(rnd, cfg);
            current = 0;
            BeginScene();
        }

        void BeginScene()
        {
            hover = NONE;
            lastTick = int.MaxValue;
            state = State.Playing;
            clock.Reset();
            clock.Start();
            clockTimer.Start();
            Invalidate();
        }

        double Elapsed
        {
            get { return previewElapsed >= 0 ? previewElapsed : clock.Elapsed.TotalSeconds; }
        }

        void OnClock(object sender, EventArgs e)
        {
            if (state != State.Playing) { clockTimer.Stop(); return; }
            Scene s = scenes[current];
            double remaining = s.TimeLimit - Elapsed;
            if (remaining <= 0) { Choose(Scene.TimeoutChoice); return; }
            // tic-tac a cada meio segundo nos últimos 2 segundos
            int half = (int)Math.Ceiling(remaining * 2);
            if (remaining <= 2 && half < lastTick)
            {
                lastTick = half;
                Sons.TicTac(half % 2 == 0);
            }
            Invalidate();
        }

        void Choose(int choice)
        {
            Scene s = scenes[current];
            clockTimer.Stop();
            clock.Stop();
            s.Chosen = choice;
            s.ElapsedSeconds = choice == Scene.TimeoutChoice ? s.TimeLimit : clock.Elapsed.TotalSeconds;
            s.Evaluate();
            if (s.TimedOut) System.Media.SystemSounds.Hand.Play();
            state = State.Chosen;
            Cursor = Cursors.Default;
            pauseTimer.Interval = s.TimedOut ? 1800 : 1100;
            pauseTimer.Start();
            Invalidate();
        }

        void OnPauseEnd(object sender, EventArgs e)
        {
            pauseTimer.Stop();
            current++;
            if (current < scenes.Count) { BeginScene(); return; }
            resultado = Ranking.Calcular(scenes, cfg);
            state = State.Result;
            hover = NONE;
            if (resultado.Nivel <= 1) System.Media.SystemSounds.Asterisk.Play();
            else System.Media.SystemSounds.Exclamation.Play();
            Invalidate();
        }

        // ---------- Entrada ----------

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            mouse = e.Location;
            bool clickable = false;
            hover = NONE;
            if (state == State.Playing)
            {
                Scene s = scenes[current];
                hover = HitTest(e.Location, s);
                clickable = hover == Scene.StallChoice || (hover >= 0 && !s.Occupied[hover]);
            }
            else if (state == State.Intro) clickable = btnStart.Contains(e.Location);
            else if (state == State.Result) clickable = btnAgain.Contains(e.Location) || btnExit.Contains(e.Location);
            Cursor = clickable ? Cursors.Hand : Cursors.Default;
            if (state != State.Playing) Invalidate(); // em jogo o cronômetro já redesenha
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
                int i = HitTest(e.Location, s);
                if (i == NONE) return;
                if (i != Scene.StallChoice && s.Occupied[i]) { System.Media.SystemSounds.Beep.Play(); return; }
                Choose(i);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
            else if (e.KeyCode == Keys.Enter && (state == State.Intro || state == State.Result)) StartGame();
        }

        // ---------- Geometria ----------

        static RectangleF SlotRect(int i, Scene s)
        {
            float left = 60, right = s.HasStall ? W - 215 : W - 60;
            float slot = Math.Min(140f, (right - left) / s.N);
            float x0 = left + (right - left - slot * s.N) / 2f;
            return new RectangleF(x0 + i * slot, 170, slot, 450);
        }

        static readonly RectangleF StallRect = new RectangleF(W - 195, 175, 150, FLOOR_Y + 30 - 175);

        static float UrinalWidth(Scene s)
        {
            return Math.Min(74f, SlotRect(0, s).Width * 0.62f);
        }

        static int HitTest(Point p, Scene s)
        {
            if (s.HasStall && StallRect.Contains(p)) return Scene.StallChoice;
            for (int i = 0; i < s.N; i++)
                if (SlotRect(i, s).Contains(p)) return i;
            return NONE;
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
            // não interrompe uma partida em andamento: espera voltar para a tela inicial ou final
            if (state == State.Playing || state == State.Chosen)
            {
                Timer espera = new Timer();
                espera.Interval = 1000;
                espera.Tick += delegate
                {
                    if (state == State.Playing || state == State.Chosen) return;
                    espera.Stop();
                    espera.Dispose();
                    OferecerAtualizacao(r);
                };
                espera.Start();
                return;
            }

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

        // ---------- Desenho: utilitários ----------

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

        static void FillRound(Graphics g, RectangleF r, float rad, Color c)
        {
            using (GraphicsPath p = RoundRect(r, rad))
            using (SolidBrush b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        static void DrawText(Graphics g, string s, Font f, Color c, RectangleF r)
        {
            DrawText(g, s, f, c, r, StringAlignment.Center);
        }

        static void DrawText(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment align)
        {
            using (StringFormat sf = new StringFormat())
            using (SolidBrush b = new SolidBrush(c))
            {
                sf.Alignment = align;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
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

        void Banner(Graphics g, string text, Color bg)
        {
            SizeF size = g.MeasureString(text, fMedB);
            RectangleF r = new RectangleF(W / 2 - size.Width / 2 - 30, 112, size.Width + 60, 52);
            FillRound(g, r, 14, bg);
            DrawText(g, text, fMedB, Color.White, r);
        }

        // ---------- Desenho: cenário ----------

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

        void DrawDivider(Graphics g, float x)
        {
            RectangleF r = new RectangleF(x - 6, 262, 12, 250);
            using (GraphicsPath p = RoundRect(r, 5))
            {
                using (LinearGradientBrush lg = new LinearGradientBrush(r, Color.FromArgb(235, 240, 242), Color.FromArgb(170, 182, 188), LinearGradientMode.Horizontal))
                    g.FillPath(lg, p);
                using (Pen pen = new Pen(Color.FromArgb(110, 125, 130), 2))
                    g.DrawPath(pen, p);
            }
            using (SolidBrush bracket = new SolidBrush(Color.FromArgb(110, 125, 130)))
                g.FillRectangle(bracket, x - 9, 270, 18, 8);
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

            if (player) YouTag(g, cx, 296);
        }

        void YouTag(Graphics g, float cx, float y)
        {
            RectangleF label = new RectangleF(cx - 36, y, 72, 28);
            FillRound(g, label, 8, Gold);
            DrawText(g, "VOCÊ", fSmall, Color.Black, label);
        }

        void DrawStall(Graphics g, bool occupiedByPlayer)
        {
            RectangleF r = StallRect;
            // paredes laterais e porta
            using (SolidBrush wall = new SolidBrush(Color.FromArgb(70, 95, 105)))
                g.FillRectangle(wall, r.X, r.Y, r.Width, r.Height - 24);
            RectangleF door = new RectangleF(r.X + 14, r.Y + 14, r.Width - 28, r.Height - 44);

            // vão da porta: escuro por dentro (aparece na fresta quando a porta está entreaberta)
            using (LinearGradientBrush inside = new LinearGradientBrush(door, Color.FromArgb(18, 24, 28), Color.FromArgb(48, 60, 66), LinearGradientMode.Vertical))
                g.FillRectangle(inside, door);

            // porta: fechada quando você entra; entreaberta (girando para dentro) quando está livre
            float gapW = occupiedByPlayer ? 0 : 30;
            float inset = occupiedByPlayer ? 0 : 7;
            PointF[] panel = {
                new PointF(door.X, door.Y),
                new PointF(door.Right - gapW, door.Y + inset),
                new PointF(door.Right - gapW, door.Bottom - inset),
                new PointF(door.X, door.Bottom)
            };
            using (LinearGradientBrush lg = new LinearGradientBrush(door, Color.FromArgb(125, 160, 175), Color.FromArgb(80, 110, 122), LinearGradientMode.Horizontal))
                g.FillPolygon(lg, panel);
            using (Pen pen = new Pen(Color.FromArgb(45, 65, 72), 3))
            {
                g.DrawPolygon(pen, panel);
                g.DrawRectangle(pen, door.X, door.Y, door.Width, door.Height);
            }
            // vão embaixo da porta
            using (SolidBrush legs = new SolidBrush(Color.FromArgb(45, 65, 72)))
            {
                g.FillRectangle(legs, r.X, r.Bottom - 24, 10, 24);
                g.FillRectangle(legs, r.Right - 10, r.Bottom - 24, 10, 24);
            }

            float panelW = door.Width - gapW;
            float pcx = door.X + panelW / 2;
            RectangleF sign = new RectangleF(pcx - 24, door.Y + 26, 48, 28);
            FillRound(g, sign, 6, Color.White);
            DrawText(g, "WC", fSmall, Color.FromArgb(45, 65, 72), sign);
            using (SolidBrush handle = new SolidBrush(Color.FromArgb(210, 215, 220)))
                g.FillRectangle(handle, door.X + panelW - 20, door.Y + door.Height / 2 + 62, 12, 8);

            // placa grande de LIVRE / OCUPADO
            RectangleF status = new RectangleF(door.X + 8, door.Y + door.Height / 2 - 40, panelW - 16, 36);
            FillRound(g, status, 8, occupiedByPlayer ? Color.FromArgb(205, 45, 45) : Color.FromArgb(45, 175, 85));
            using (Pen sp = new Pen(Color.FromArgb(160, 255, 255, 255), 2))
            using (GraphicsPath spp = RoundRect(status, 8))
                g.DrawPath(sp, spp);
            DrawText(g, occupiedByPlayer ? "OCUPADO" : "LIVRE", occupiedByPlayer ? fSmall : fMedB, Color.White, status);

            DrawText(g, "CABINE", fTiny, Color.White, new RectangleF(r.X, r.Y - 2, r.Width, 18));
            if (occupiedByPlayer)
            {
                YouTag(g, r.X + r.Width / 2, door.Y + door.Height / 2 + 12);
                // pés aparecendo embaixo da porta
                using (SolidBrush shoe = new SolidBrush(Color.FromArgb(25, 25, 25)))
                {
                    g.FillEllipse(shoe, r.X + r.Width / 2 - 24, r.Bottom - 14, 22, 12);
                    g.FillEllipse(shoe, r.X + r.Width / 2 + 2, r.Bottom - 14, 22, 12);
                }
            }
        }

        void Highlight(Graphics g, RectangleF r)
        {
            using (GraphicsPath p = RoundRect(r, 16))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 80, 160, 255)))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(200, 60, 130, 230), 3))
                    g.DrawPath(pen, p);
            }
        }

        void DrawBathroom(Graphics g, Scene s, bool interactive)
        {
            bool hovering = interactive && state == State.Playing;
            float uw = UrinalWidth(s);

            if (hovering && hover >= 0 && hover < s.N && !s.Occupied[hover])
            {
                RectangleF slot = SlotRect(hover, s);
                Highlight(g, new RectangleF(slot.X + 4, 176, slot.Width - 8, 440));
            }
            if (s.HasStall && hovering && hover == Scene.StallChoice)
            {
                RectangleF st = StallRect;
                st.Inflate(8, 8);
                Highlight(g, st);
            }

            for (int i = 0; i < s.N; i++)
            {
                RectangleF slot = SlotRect(i, s);
                DrawUrinal(g, slot.X + slot.Width / 2, uw, i + 1);
            }
            for (int i = 0; i < s.N - 1; i++)
                if (s.Divider[i]) DrawDivider(g, SlotRect(i + 1, s).X);
            for (int i = 0; i < s.N; i++)
            {
                RectangleF slot = SlotRect(i, s);
                float cx = slot.X + slot.Width / 2;
                if (s.Occupied[i]) DrawPerson(g, cx, uw, s.Shirts[i], false);
                else if (i == s.Chosen) DrawPerson(g, cx, uw, Gold, true);
            }
            if (s.HasStall) DrawStall(g, s.StallChosen);
        }

        void DrawHeader(Graphics g, string left, string center)
        {
            using (SolidBrush bar = new SolidBrush(Color.FromArgb(225, 25, 35, 40)))
                g.FillRectangle(bar, 0, 0, W, 90);
            DrawText(g, left, fBig, Gold, new RectangleF(30, 0, 300, 90), StringAlignment.Near);
            DrawText(g, center, fMed, Color.White, new RectangleF(250, 0, W - 500, 90));

            int n = scenes.Count;
            float dot = 18, gap = 10;
            float x = W - 30 - n * (dot + gap) + gap;
            for (int i = 0; i < n; i++)
            {
                Color c = i < current ? Gold : i == current ? Color.White : Color.FromArgb(90, 255, 255, 255);
                using (SolidBrush b = new SolidBrush(c))
                    g.FillEllipse(b, x + i * (dot + gap), 45 - dot / 2, dot, dot);
            }
        }

        void DrawClock(Graphics g, Scene s)
        {
            double remaining = state == State.Playing ? Math.Max(0, s.TimeLimit - Elapsed)
                             : Math.Max(0, s.TimeLimit - s.ElapsedSeconds);
            float frac = (float)(remaining / s.TimeLimit);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(200, 20, 28, 32)))
                g.FillRectangle(bg, 0, 90, W, 16);
            Color c = frac > 0.5f ? Color.FromArgb(70, 200, 100)
                    : frac > 0.25f ? Color.FromArgb(240, 190, 30)
                    : Color.FromArgb(230, 60, 60);
            using (SolidBrush b = new SolidBrush(c))
                g.FillRectangle(b, 0, 92, W * frac, 12);
            RectangleF tr = new RectangleF(W - 84, 104, 74, 30);
            FillRound(g, tr, 8, Color.FromArgb(200, 20, 28, 32));
            DrawText(g, remaining.ToString("0.0") + " s", fSmall, c, tr);
        }

        void DrawScene(Graphics g)
        {
            Scene s = scenes[current];
            DrawBathroom(g, s, true);

            if (s.TimedOut)
            {
                using (SolidBrush puddle = new SolidBrush(Color.FromArgb(190, 240, 205, 40)))
                    g.FillEllipse(puddle, W / 2 - 150, FLOOR_Y + 60, 300, 46);
            }

            DrawHeader(g, "CENA " + (current + 1) + " DE " + scenes.Count, "Clique onde você vai mijar");
            DrawClock(g, s);

            if (state == State.Chosen)
            {
                if (s.TimedOut) Banner(g, "Tempo esgotado! Não aguentou e mijou na calça...", Color.FromArgb(230, 190, 40, 40));
                else Banner(g, "Escolha registrada...", Color.FromArgb(225, 25, 35, 40));
            }
            else if (hover >= 0 && hover < s.N && s.Occupied[hover])
            {
                RectangleF r = new RectangleF(mouse.X - 60, mouse.Y - 50, 120, 34);
                FillRound(g, r, 10, Color.FromArgb(230, 180, 40, 40));
                DrawText(g, "Ocupado!", fSmall, Color.White, r);
            }
        }

        // ---------- Desenho: telas ----------

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
            DrawBathroom(g, introScene, false);
            Overlay(g, 190);
            ShadowText(g, cfg.Titulo, fTitle, Gold, new RectangleF(0, 70, W, 90));
            DrawText(g, cfg.Subtitulo, fMed, Color.White, new RectangleF(0, 160, W, 40));

            RectangleF box = new RectangleF(W / 2 - 340, 225, 680, 255);
            using (GraphicsPath p = RoundRect(box, 18))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 35, 45, 50))) g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(100, 255, 255, 255), 2)) g.DrawPath(pen, p);
            }
            string rules =
                "Você vai passar por " + cfg.Cenas + " cenas de banheiro.\n\n" +
                "Em cada uma, clique no mictório onde você vai mijar.\n" +
                "Às vezes a cabine também é uma opção.\n\n" +
                "Decida rápido: o tempo está correndo.\n" +
                "No final, o seu ranking.";
            DrawText(g, rules, fMed, Color.White, box);

            DrawButton(g, btnStart, "COMEÇAR", Gold);
            DrawText(g, "v" + Updater.AtualTexto, fSmall, Color.FromArgb(150, 255, 255, 255), new RectangleF(W - 110, H - 40, 100, 30));
        }

        static readonly Color[] TierColors = {
            Color.FromArgb(245, 200, 40),   // macho alfa
            Color.FromArgb(70, 210, 100),   // macho
            Color.FromArgb(90, 170, 240),   // em treinamento
            Color.FromArgb(240, 140, 50),   // suspeito
            Color.FromArgb(255, 90, 170)    // reprovado
        };

        void DrawResult(Graphics g)
        {
            Overlay(g, 220);
            Resultado r = resultado;
            Color main = TierColors[r.Nivel];

            Font titleFont = g.MeasureString(r.Titulo, fHuge).Width < W - 80 ? fHuge : fTitle;
            ShadowText(g, r.Titulo, titleFont, main, new RectangleF(0, 22, W, 110));
            DrawText(g, r.Mensagem, fMed, Color.White, new RectangleF(0, 128, W, 36));
            string placar = "Pontuação: " + r.Total + " de " + r.Maximo;
            if (r.Grave) placar += "   •   Falta grave na cena " + (r.CenaGrave + 1);
            DrawText(g, placar, fMedB, Gold, new RectangleF(0, 164, W, 34));

            float top = 210;
            if (r.DicaCabine)
            {
                RectangleF tip = new RectangleF(W / 2 - 400, 202, 800, 38);
                FillRound(g, tip, 10, Color.FromArgb(220, 45, 140, 75));
                DrawText(g, "Dica: quando todo mictório livre está colado em alguém, a CABINE é a saída honrosa.", fSmall, Color.White, tip);
                top = 248;
            }
            float rowH = Math.Min(64f, (595f - top) / scenes.Count);
            for (int i = 0; i < scenes.Count; i++)
                DrawResultRow(g, scenes[i], i, new RectangleF(90, top + i * rowH + 3, W - 180, rowH - 6));

            DrawButton(g, btnAgain, "JOGAR DE NOVO", Gold);
            DrawButton(g, btnExit, "SAIR", Color.FromArgb(200, 205, 210));
        }

        void DrawResultRow(Graphics g, Scene s, int index, RectangleF row)
        {
            FillRound(g, row, 10, s.Grave ? Color.FromArgb(170, 110, 30, 60) : Color.FromArgb(150, 40, 50, 58));
            DrawText(g, "Cena " + (index + 1), fMedB, Color.White, new RectangleF(row.X + 16, row.Y, 100, row.Height), StringAlignment.Near);

            // mini mapa: cinza = livre, azul = ocupado, amarelo = você; traço branco = divisória; porta = cabine
            float sq = Math.Min(24f, row.Height - 16), gap = 5;
            float mx = row.X + 116, my = row.Y + (row.Height - sq) / 2;
            for (int k = 0; k < s.N; k++)
            {
                Color c = s.Occupied[k] ? Color.FromArgb(70, 120, 200)
                        : k == s.Chosen ? Gold
                        : Color.FromArgb(200, 205, 210);
                FillRound(g, new RectangleF(mx + k * (sq + gap), my, sq, sq), 5, c);
                if (k < s.N - 1 && s.Divider[k])
                    using (SolidBrush d = new SolidBrush(Color.White))
                        g.FillRectangle(d, mx + k * (sq + gap) + sq + 1, my - 4, 3, sq + 8);
            }
            if (s.HasStall)
            {
                RectangleF st = new RectangleF(mx + s.N * (sq + gap) + 8, my - 3, sq * 0.8f, sq + 6);
                FillRound(g, st, 3, s.StallChosen ? Gold : Color.FromArgb(120, 155, 170));
                DrawText(g, "C", fTiny, Color.FromArgb(30, 40, 45), st);
            }

            string comment = s.IgnorouCabine ? "Colou no parceiro — a cabine estava livre!"
                           : s.Comment + (s.Bonus ? "  + Decidido" : "");
            Color cc = s.Grave ? Color.FromArgb(255, 110, 180)
                     : s.Points >= 100 ? Color.FromArgb(90, 220, 120)
                     : s.Points > 0 ? Color.FromArgb(240, 200, 90)
                     : Color.FromArgb(255, 130, 110);
            DrawText(g, comment, fSmall, cc, new RectangleF(row.X + 470, row.Y, row.Width - 470 - 80, row.Height), StringAlignment.Near);
            DrawText(g, s.Points.ToString(), fBig, Color.White, new RectangleF(row.Right - 90, row.Y, 74, row.Height), StringAlignment.Far);
        }

        // ---------- Prévia (só para desenvolvimento) ----------

        // mode: intro | normal | vazio | divisorias | socabine | timeout | result | result-bom
        public static void SavePreview(string path, string mode, int seed)
        {
            GameForm f = new GameForm(new Config());
            f.rnd = new Random(seed);
            f.introScene = MakeIntroScene(f.rnd);
            if (mode != "intro")
            {
                f.scenes = Generator.MakeGame(f.rnd, f.cfg);
                if (mode.StartsWith("result"))
                {
                    foreach (Scene s in f.scenes)
                    {
                        if (mode == "result-bom") s.Chosen = BestChoice(s);
                        else if (mode == "result-cabine")
                            s.Chosen = s.Kind == SceneKind.SoCabine ? Array.IndexOf(s.Occupied, false) : BestChoice(s);
                        else
                        {
                            List<int> options = new List<int>();
                            for (int i = 0; i < s.N; i++) if (!s.Occupied[i]) options.Add(i);
                            if (s.HasStall) options.Add(Scene.StallChoice);
                            s.Chosen = options[f.rnd.Next(options.Count)];
                        }
                        s.ElapsedSeconds = f.rnd.NextDouble() * s.TimeLimit;
                        s.Evaluate();
                    }
                    f.resultado = Ranking.Calcular(f.scenes, f.cfg);
                    f.current = f.scenes.Count;
                    f.state = State.Result;
                }
                else
                {
                    SceneKind kind = mode == "vazio" ? SceneKind.Vazio : mode == "divisorias" ? SceneKind.Divisorias
                                   : (mode == "socabine" || mode == "cabine-ocupada") ? SceneKind.SoCabine : SceneKind.Normal;
                    f.current = Math.Max(0, f.scenes.FindIndex(delegate(Scene s) { return s.Kind == kind; }));
                    f.state = State.Playing;
                    f.previewElapsed = f.scenes[f.current].TimeLimit * 0.6;
                    if (mode == "timeout" || mode == "cabine-ocupada")
                    {
                        Scene s = f.scenes[f.current];
                        s.Chosen = mode == "timeout" ? Scene.TimeoutChoice : Scene.StallChoice;
                        s.ElapsedSeconds = s.TimeLimit;
                        s.Evaluate();
                        f.state = State.Chosen;
                    }
                }
            }
            using (Bitmap bmp = new Bitmap(W, H))
            {
                using (Graphics g = Graphics.FromImage(bmp)) f.Render(g);
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            f.Dispose();
        }

        public static int BestChoice(Scene s)
        {
            if (!s.HasSafeUrinal) return s.HasStall ? Scene.StallChoice : 0;
            if (s.OccupiedCount == 0) return 0;
            int best = -1;
            for (int i = 0; i < s.N; i++)
                if (s.IsSafe(i) && (best < 0 || s.Safety(i) > s.Safety(best))) best = i;
            return best;
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
