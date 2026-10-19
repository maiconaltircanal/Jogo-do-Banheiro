using System;
using System.Collections.Generic;
using System.Drawing;

namespace JogoDoBanheiro
{
    public enum SceneKind { Normal, Vazio, Divisorias, SoCabine }

    public class Scene
    {
        public const int StallChoice = 1000;
        public const int TimeoutChoice = -2;

        public SceneKind Kind;
        public int N;
        public bool[] Occupied;
        public bool[] Divider;   // Divider[i] = divisória entre o mictório i e o i+1
        public bool HasStall;
        public Color[] Shirts;
        public double TimeLimit = 6;

        public int Chosen = -1;
        public double ElapsedSeconds;

        // Resultado (preenchido por Evaluate)
        public int Points;
        public bool Bonus;
        public bool Grave;
        public string Comment = "";

        public bool StallChosen { get { return Chosen == StallChoice; } }
        public bool TimedOut { get { return Chosen == TimeoutChoice; } }

        public int OccupiedCount
        {
            get { int c = 0; foreach (bool o in Occupied) if (o) c++; return c; }
        }

        public bool NextToOccupied(int i)
        {
            return (i > 0 && Occupied[i - 1]) || (i < N - 1 && Occupied[i + 1]);
        }

        // Regra principal: colado em alguém sem divisória no meio.
        public bool Exposed(int i)
        {
            return (i > 0 && Occupied[i - 1] && !Divider[i - 1]) ||
                   (i < N - 1 && Occupied[i + 1] && !Divider[i]);
        }

        public bool IsSafe(int i)
        {
            return !Occupied[i] && !Exposed(i);
        }

        public bool HasSafeUrinal
        {
            get { for (int i = 0; i < N; i++) if (IsSafe(i)) return true; return false; }
        }

        // Colou em alguém numa cena em que a única saída era a cabine.
        public bool IgnorouCabine
        {
            get { return Grave && HasStall && !HasSafeUrinal; }
        }

        // Distância até o ocupado mais próximo; as pontas ganham meio ponto (parede de um lado).
        public double Safety(int i)
        {
            int d = int.MaxValue;
            for (int j = 0; j < N; j++) if (Occupied[j]) d = Math.Min(d, Math.Abs(i - j));
            double v = d == int.MaxValue ? 100 : d;
            if (i == 0 || i == N - 1) v += 0.5;
            return v;
        }

        public double BestSafety()
        {
            double best = -1;
            for (int i = 0; i < N; i++) if (IsSafe(i)) best = Math.Max(best, Safety(i));
            return best;
        }

        bool IsMiddle(int i)
        {
            return N % 2 == 1 ? i == N / 2 : (i == N / 2 - 1 || i == N / 2);
        }

        public void Evaluate()
        {
            Points = 0;
            Bonus = false;
            Grave = false;

            if (TimedOut) { Comment = "Não aguentou e mijou na calça"; return; }

            if (StallChosen)
            {
                if (!HasSafeUrinal) { Points = 100; Comment = "Saída honrosa"; }
                else { Points = 40; Comment = "Fugiu da responsabilidade"; }
            }
            else if (Exposed(Chosen))
            {
                Grave = true;
                Comment = "Colou no parceiro";
                return;
            }
            else if (OccupiedCount == 0)
            {
                if (Chosen == 0 || Chosen == N - 1) { Points = 100; Comment = "Conhece o código"; }
                else if (IsMiddle(Chosen)) { Points = 30; Comment = "Se achando o dono do banheiro"; }
                else { Points = 60; Comment = "Sem estratégia nenhuma"; }
            }
            else if (Safety(Chosen) >= BestSafety() - 1e-9) { Points = 100; Comment = "Distância regulamentar"; }
            else if (NextToOccupied(Chosen)) { Points = 70; Comment = "Salvo pela divisória"; }
            else { Points = 70; Comment = "Passou raspando"; }

            if (Points > 0 && ElapsedSeconds <= TimeLimit / 2)
            {
                Bonus = true;
                Points += 20;
            }
        }
    }

    public static class Generator
    {
        static readonly Color[] Palette = {
            Color.FromArgb(200, 60, 60), Color.FromArgb(60, 120, 200), Color.FromArgb(70, 160, 90),
            Color.FromArgb(150, 80, 170), Color.FromArgb(230, 130, 40), Color.FromArgb(60, 60, 70),
            Color.FromArgb(40, 150, 160), Color.FromArgb(170, 110, 70)
        };

        // Sempre 1 banheiro vazio, 1 com divisórias, 1 onde só a cabine salva e o resto normal, em ordem sorteada.
        public static List<Scene> MakeGame(Random rnd, Config cfg)
        {
            List<SceneKind> kinds = new List<SceneKind>();
            kinds.Add(SceneKind.Vazio);
            kinds.Add(SceneKind.Divisorias);
            kinds.Add(SceneKind.SoCabine);
            while (kinds.Count < cfg.Cenas) kinds.Add(SceneKind.Normal);
            for (int i = kinds.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                SceneKind t = kinds[i]; kinds[i] = kinds[j]; kinds[j] = t;
            }
            if (kinds.Count > cfg.Cenas) kinds.RemoveRange(cfg.Cenas, kinds.Count - cfg.Cenas);

            List<Scene> scenes = new List<Scene>();
            for (int i = 0; i < kinds.Count; i++)
            {
                Scene s = Make(rnd, kinds[i], cfg.MinMictorios, cfg.MaxMictorios);
                s.TimeLimit = Math.Max(cfg.TempoMinimo, cfg.TempoInicial - i);
                scenes.Add(s);
            }
            return scenes;
        }

        public static Scene Make(Random rnd, SceneKind kind, int min, int max)
        {
            int lo = min;
            if (kind == SceneKind.Normal) lo = Math.Max(lo, 3);
            if (kind == SceneKind.Vazio || kind == SceneKind.Divisorias) lo = Math.Max(lo, 4);
            int hi = Math.Max(lo, max);

            while (true)
            {
                int n = rnd.Next(lo, hi + 1);
                Scene s = new Scene();
                s.Kind = kind;
                s.N = n;
                s.Occupied = new bool[n];
                s.Divider = new bool[Math.Max(0, n - 1)];

                if (kind == SceneKind.Vazio)
                {
                    s.HasStall = rnd.Next(2) == 0;
                }
                else if (kind == SceneKind.Normal)
                {
                    Fill(rnd, s, 0.25, 0.6);
                    if (s.OccupiedCount == 0 || !s.HasSafeUrinal || CountExposedFree(s) == 0) continue;
                    s.HasStall = rnd.Next(2) == 0;
                }
                else if (kind == SceneKind.Divisorias)
                {
                    Fill(rnd, s, 0.3, 0.6);
                    for (int i = 0; i < n - 1; i++) s.Divider[i] = rnd.NextDouble() < 0.5;
                    // precisa ter: um lugar seguro, um lugar salvo pela divisória e uma armadilha sem divisória
                    bool protegido = false;
                    for (int i = 0; i < n; i++) if (!s.Occupied[i] && s.NextToOccupied(i) && !s.Exposed(i)) protegido = true;
                    if (!protegido || !s.HasSafeUrinal || CountExposedFree(s) == 0) continue;
                    s.HasStall = rnd.Next(2) == 0;
                }
                else // SoCabine
                {
                    Fill(rnd, s, 0.45, 0.85);
                    if (s.OccupiedCount == s.N || s.HasSafeUrinal) continue;
                    s.HasStall = true;
                }

                s.Shirts = new Color[n];
                for (int i = 0; i < n; i++) s.Shirts[i] = Palette[rnd.Next(Palette.Length)];
                return s;
            }
        }

        static void Fill(Random rnd, Scene s, double pMin, double pMax)
        {
            double p = pMin + rnd.NextDouble() * (pMax - pMin);
            for (int i = 0; i < s.N; i++) s.Occupied[i] = rnd.NextDouble() < p;
        }

        static int CountExposedFree(Scene s)
        {
            int c = 0;
            for (int i = 0; i < s.N; i++) if (!s.Occupied[i] && s.Exposed(i)) c++;
            return c;
        }
    }

    public class Resultado
    {
        public int Total;
        public int Maximo;
        public bool Grave;
        public int CenaGrave = -1;
        public bool DicaCabine;    // reprovou por colar em alguém quando a cabine era a saída
        public int Nivel;          // 0..3 = ranking; 4 = reprovado
        public string Titulo;
        public string Mensagem;
    }

    public static class Ranking
    {
        public const int PontosPorCena = 120;

        public static Resultado Calcular(List<Scene> scenes, Config cfg)
        {
            Resultado r = new Resultado();
            r.Maximo = PontosPorCena * scenes.Count;
            for (int i = 0; i < scenes.Count; i++)
            {
                r.Total += scenes[i].Points;
                if (scenes[i].Grave && !r.Grave) { r.Grave = true; r.CenaGrave = i; }
                if (scenes[i].IgnorouCabine) r.DicaCabine = true;
            }

            r.Nivel = 4;
            if (!r.Grave)
            {
                for (int i = 0; i < 4; i++)
                {
                    int minimo = (int)Math.Ceiling(cfg.RankPontos[i] * scenes.Count / 5.0);
                    if (r.Total >= minimo) { r.Nivel = i; break; }
                }
            }

            if (r.Nivel < 4)
            {
                r.Titulo = cfg.RankTitulo[r.Nivel];
                r.Mensagem = cfg.RankMsg[r.Nivel];
            }
            else
            {
                r.Titulo = cfg.MsgReprovado;
                r.Mensagem = r.Grave ? cfg.DetalheReprovado : cfg.DetalheReprovadoPontos;
            }
            return r;
        }
    }
}
