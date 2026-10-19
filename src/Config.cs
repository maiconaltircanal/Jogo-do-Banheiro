using System;
using System.IO;
using System.Text;

namespace JogoDoBanheiro
{
    // Textos e parâmetros do jogo. Lidos de config.ini (criado automaticamente ao lado do .exe).
    public class Config
    {
        public string Titulo = "JOGO DO BANHEIRO";
        public string Subtitulo = "Entre no banheiro e escolha onde você vai mijar.";
        public string MsgReprovado = "VOCÊ É GAY!";
        public string DetalheReprovado = "Escolheu mictório colado em outro cara.";
        public string DetalheReprovadoPontos = "Pontuação abaixo do mínimo aceitável no banheiro.";
        public int Cenas = 5;
        public int MinMictorios = 2;
        public int MaxMictorios = 10;
        public int TempoInicial = 6;
        public int TempoMinimo = 3;

        // Ranking, do melhor para o pior. Pontos mínimos valem para 5 cenas (máximo 600)
        // e são ajustados proporcionalmente quando o número de cenas muda.
        public int[] RankPontos = { 540, 420, 300, 150 };
        public string[] RankTitulo = { "MACHO ALFA", "MACHO", "MACHO EM TREINAMENTO", "SUSPEITO" };
        public string[] RankMsg = {
            "Código do mictório executado com perfeição.",
            "Respeitou o espaço sagrado de cada um.",
            "Tá no caminho, mas ainda erra a mira.",
            "Os caras do banheiro estão de olho em você."
        };

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
                        case "msg_reprovado": c.MsgReprovado = v; break;
                        case "detalhe_reprovado": c.DetalheReprovado = v; break;
                        case "detalhe_reprovado_pontos": c.DetalheReprovadoPontos = v; break;
                        case "cenas": c.Cenas = ParseInt(v, c.Cenas, 1, 10); break;
                        case "min_mictorios": c.MinMictorios = ParseInt(v, c.MinMictorios, 2, 12); break;
                        case "max_mictorios": c.MaxMictorios = ParseInt(v, c.MaxMictorios, 2, 12); break;
                        case "tempo_inicial": c.TempoInicial = ParseInt(v, c.TempoInicial, 1, 60); break;
                        case "tempo_minimo": c.TempoMinimo = ParseInt(v, c.TempoMinimo, 1, 60); break;
                        default:
                            // rank1_pontos, rank1_titulo, rank1_msg ... rank4_*
                            if (k.Length > 6 && k.StartsWith("rank") && k[5] == '_' && k[4] >= '1' && k[4] <= '4')
                            {
                                int r = k[4] - '1';
                                string campo = k.Substring(6);
                                if (campo == "pontos") c.RankPontos[r] = ParseInt(v, c.RankPontos[r], 0, 10000);
                                else if (campo == "titulo") c.RankTitulo[r] = v;
                                else if (campo == "msg") c.RankMsg[r] = v;
                            }
                            break;
                    }
                }
                if (c.MaxMictorios < c.MinMictorios) c.MaxMictorios = c.MinMictorios;
                if (c.TempoMinimo > c.TempoInicial) c.TempoMinimo = c.TempoInicial;
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
            sb.AppendLine();
            sb.AppendLine("# Ranking (pontos mínimos para 5 cenas; máximo 600)");
            for (int i = 0; i < 4; i++)
            {
                sb.AppendLine("rank" + (i + 1) + "_pontos=" + RankPontos[i]);
                sb.AppendLine("rank" + (i + 1) + "_titulo=" + RankTitulo[i]);
                sb.AppendLine("rank" + (i + 1) + "_msg=" + RankMsg[i]);
            }
            sb.AppendLine();
            sb.AppendLine("# Reprovado: falta grave (colou em alguém) ou pontos abaixo do rank 4");
            sb.AppendLine("msg_reprovado=" + MsgReprovado);
            sb.AppendLine("detalhe_reprovado=" + DetalheReprovado);
            sb.AppendLine("detalhe_reprovado_pontos=" + DetalheReprovadoPontos);
            sb.AppendLine();
            sb.AppendLine("# Quantidade de cenas (1 a 10)");
            sb.AppendLine("cenas=" + Cenas);
            sb.AppendLine("# Faixa de mictórios por cena (2 a 12)");
            sb.AppendLine("min_mictorios=" + MinMictorios);
            sb.AppendLine("max_mictorios=" + MaxMictorios);
            sb.AppendLine("# Cronômetro em segundos: começa em tempo_inicial e cai 1 por cena até tempo_minimo");
            sb.AppendLine("tempo_inicial=" + TempoInicial);
            sb.AppendLine("tempo_minimo=" + TempoMinimo);
            return sb.ToString();
        }
    }
}
