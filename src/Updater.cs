using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace JogoDoBanheiro
{
    public class ReleaseInfo
    {
        public string Tag;
        public Version Versao;
        public string Notas = "";
        public string SetupUrl;
        public string Sha256;
    }

    // Busca a última Release publicada no GitHub e, se for mais nova, baixa e roda o instalador.
    public static class Updater
    {
        public const string Repo = "maiconaltircanal/Jogo-do-Banheiro";
        public const string SetupAsset = "JogoDoBanheiro-Setup.exe";

        public static Version Atual
        {
            get { return Normalizar(Assembly.GetExecutingAssembly().GetName().Version); }
        }

        public static string AtualTexto
        {
            get { return Atual.ToString(3); }
        }

        static Version Normalizar(Version v)
        {
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }

        public static Version ParseVersao(string tag)
        {
            if (tag == null) return null;
            Version v;
            if (!Version.TryParse(tag.Trim().TrimStart('v', 'V'), out v)) return null;
            return Normalizar(v);
        }

        static string UserAgent
        {
            get { return "JogoDoBanheiro/" + AtualTexto; }
        }

        public static ReleaseInfo BuscarUltima()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/" + Repo + "/releases/latest");
            req.UserAgent = UserAgent;
            req.Accept = "application/vnd.github+json";
            req.Timeout = 8000;
            req.ReadWriteTimeout = 8000;
            using (WebResponse resp = req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return Parse(sr.ReadToEnd());
        }

        public static ReleaseInfo Parse(string json)
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            IDictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(json);
            ReleaseInfo r = new ReleaseInfo();
            r.Tag = d["tag_name"] as string;
            r.Versao = ParseVersao(r.Tag);
            if (d.ContainsKey("body") && d["body"] != null) r.Notas = (string)d["body"];

            Match m = Regex.Match(r.Notas, @"sha256:\s*([0-9a-fA-F]{64})");
            if (m.Success) r.Sha256 = m.Groups[1].Value.ToLowerInvariant();

            string prefixo = "https://github.com/" + Repo + "/releases/download/";
            IEnumerable assets = d.ContainsKey("assets") ? d["assets"] as IEnumerable : null;
            if (assets != null)
            {
                foreach (object o in assets)
                {
                    IDictionary<string, object> a = o as IDictionary<string, object>;
                    if (a == null || (a["name"] as string) != SetupAsset) continue;
                    string url = a["browser_download_url"] as string;
                    if (url != null && url.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)) r.SetupUrl = url;
                }
            }
            return r;
        }

        public static bool EhMaisNova(ReleaseInfo r)
        {
            return r != null && r.Versao != null && r.SetupUrl != null && r.Sha256 != null && r.Versao > Atual;
        }

        // Notas sem a linha técnica do sha256, para mostrar ao jogador.
        public static string NotasLimpas(ReleaseInfo r)
        {
            string t = Regex.Replace(r.Notas, @"(?im)^\s*sha256:.*$", "").Trim();
            return t.Length > 500 ? t.Substring(0, 500) + "..." : t;
        }

        public static string Sha256Arquivo(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
            {
                StringBuilder sb = new StringBuilder();
                foreach (byte b in sha.ComputeHash(fs)) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // Baixa o instalador e confere o sha256 publicado na Release. Devolve o caminho do arquivo.
        public static string Baixar(ReleaseInfo r)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            string destino = Path.Combine(Path.GetTempPath(), "JogoDoBanheiro-Setup-" + r.Versao.ToString(3) + ".exe");
            using (WebClient wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", UserAgent);
                wc.DownloadFile(r.SetupUrl, destino);
            }
            if (Sha256Arquivo(destino) != r.Sha256)
            {
                File.Delete(destino);
                throw new Exception("O arquivo baixado não confere com a versão publicada. A atualização foi cancelada.");
            }
            return destino;
        }

        public static void Instalar(string setupPath)
        {
            Process.Start(setupPath, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS");
        }
    }
}
