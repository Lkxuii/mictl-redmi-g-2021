# HID_EVENT20 事件监听（60 秒，后台）——结果写 evwatch-result.txt
Add-Type -ReferencedAssemblies @('System.Management', 'System.ComponentModel.Primitives') -TypeDefinition @'
using System;
using System.Management;

public static class EW
{
    public static void Run()
    {
        var sb = new System.Text.StringBuilder();
        var scope = new ManagementScope(@"root\WMI");
        scope.Connect();
        var w = new ManagementEventWatcher(scope, new WqlEventQuery("SELECT * FROM HID_EVENT20"));
        w.Options.Timeout = TimeSpan.FromMilliseconds(500);
        w.EventArrived += (s, e) =>
        {
            try
            {
                foreach (PropertyData pd in e.NewEvent.Properties)
                {
                    var b = pd.Value as byte[];
                    sb.Append(DateTime.Now.ToString("HH:mm:ss.fff") + " [" + pd.Name + "] ");
                    sb.Append(b == null ? Convert.ToString(pd.Value) : BitConverter.ToString(b).Replace('-', ' '));
                    sb.Append("\r\n");
                }
            }
            catch (Exception ex) { sb.Append("ERR " + ex.Message + "\r\n"); }
        };
        w.Start();
        sb.Append("监听开始 " + DateTime.Now.ToString("HH:mm:ss") + "\r\n");
        System.Threading.Thread.Sleep(60000);
        w.Stop();
        sb.Append("监听结束\r\n");
        System.IO.File.WriteAllText(@"D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\probe\evwatch-result.txt", sb.ToString());
    }
}
'@
[EW]::Run()
