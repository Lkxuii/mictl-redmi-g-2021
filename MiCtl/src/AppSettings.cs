// 配置读写 —— 手写轻量 JSON（无第三方依赖）
using System;
using System.IO;
using System.Text;

namespace MiCtl
{
    public sealed class AppSettings
    {
        public bool AutoEnabled { get; set; }
        public double LowThreshold { get; set; }    // 低于 → 静谧
        public double HighThreshold { get; set; }   // 高于 → 极速（中间 → 性能/均衡）

        public AppSettings()
        {
            AutoEnabled = false;
            LowThreshold = 65;
            HighThreshold = 78;
        }

        private static string GetPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MiCtl");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "config.json");
        }

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            try
            {
                string path = GetPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    s.AutoEnabled = json.Contains("\"auto\":true");
                    s.LowThreshold = GetNum(json, "low", s.LowThreshold);
                    s.HighThreshold = GetNum(json, "high", s.HighThreshold);
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("{\r\n");
                sb.Append("  \"auto\": ").Append(AutoEnabled ? "true" : "false").Append(",\r\n");
                sb.Append("  \"low\": ").Append(LowThreshold.ToString("0.#")).Append(",\r\n");
                sb.Append("  \"high\": ").Append(HighThreshold.ToString("0.#")).Append("\r\n");
                sb.Append("}\r\n");
                File.WriteAllText(GetPath(), sb.ToString());
            }
            catch { }
        }

        private static double GetNum(string json, string key, double def)
        {
            int i = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (i < 0) return def;
            int colon = json.IndexOf(':', i);
            if (colon < 0) return def;
            StringBuilder num = new StringBuilder();
            for (int j = colon + 1; j < json.Length; j++)
            {
                char c = json[j];
                if ((c >= '0' && c <= '9') || c == '.' || c == '-') num.Append(c);
                else if (num.Length > 0) break;
            }
            double v;
            if (double.TryParse(num.ToString(), out v)) return v;
            return def;
        }
    }
}
