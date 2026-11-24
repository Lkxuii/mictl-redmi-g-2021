// 提取 WMIBinaryMofResource 全量 MofData（带全局异常捕获 + 属性自适应）
using System;
using System.IO;
using System.Management;
using System.Text;

class MofDump
{
    static int Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : ".";
        StringBuilder sb = new StringBuilder();
        try
        {
            Directory.CreateDirectory(outDir);
            ManagementScope scope = new ManagementScope(@"root\wmi");
            scope.Connect();
            ManagementClass mc = new ManagementClass(scope, new ManagementPath("WMIBinaryMofResource"), null);

            sb.AppendLine("Class properties: ");
            foreach (PropertyData pd in mc.Properties)
                sb.AppendLine("  " + pd.Name + " : " + pd.Type + (pd.IsArray ? "[]" : ""));

            ManagementClass sc = new ManagementClass(scope, new ManagementPath("WMIBinaryMofResource"), null);
            int idx = 0;
            foreach (ManagementObject m in sc.GetInstances())
            {
                sb.AppendLine("--- instance " + idx + " path=" + m.Path.Path);
                foreach (PropertyData pd in m.Properties)
                {
                    object v = pd.Value;
                    if (v is byte[])
                    {
                        byte[] data = (byte[])v;
                        sb.AppendLine("  prop " + pd.Name + " byte[] len=" + data.Length);
                        string text = Encoding.Unicode.GetString(data);
                        if (!text.Contains("MiInterface") && !text.Contains("MICommonInterface"))
                        {
                            string text8 = Encoding.UTF8.GetString(data);
                            if (text8.Contains("MiInterface") || text8.Contains("MICommonInterface"))
                                text = text8;
                            else
                                text = null;
                        }
                        if (text != null)
                        {
                            string rn = (m.Properties["MofResourceName"] != null ? m.Properties["MofResourceName"].Value as string : null) ?? ("mof" + idx);
                            string safe = rn.Replace('\\', '_').Replace('/', '_').Replace(':', '_');
                            if (safe.Length > 80) safe = safe.Substring(0, 80);
                            File.WriteAllText(Path.Combine(outDir, safe + ".mof.txt"), text);
                            sb.AppendLine("  MATCH -> " + safe + ".mof.txt");
                        }
                    }
                    else
                    {
                        sb.AppendLine("  prop " + pd.Name + " = " + (v == null ? "(null)" : v.ToString()));
                    }
                }
                idx++;
            }
            sb.AppendLine("total=" + idx);
        }
        catch (Exception ex)
        {
            sb.AppendLine("FATAL: " + ex.GetType().FullName + " " + ex.Message);
            sb.AppendLine(ex.StackTrace);
        }
        File.WriteAllText(Path.Combine(outDir, "mofdump-result.txt"), sb.ToString());
        return 0;
    }
}
