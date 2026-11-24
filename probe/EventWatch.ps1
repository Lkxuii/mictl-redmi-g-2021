# 可见的 WMI 事件监听（60 秒）—— 请配合按键测试
Add-Type -ReferencedAssemblies @('System.Management', 'System.ComponentModel.Primitives') -TypeDefinition @'
using System;
using System.Management;

public static class EvWatch
{
    public static System.Collections.Generic.List<string> Lines = new System.Collections.Generic.List<string>();
    public static System.Threading.CountdownEvent Done = new System.Threading.CountdownEvent(1);

    public static void Run(int seconds)
    {
        ManagementScope scope = new ManagementScope(@"root\WMI");
        scope.Connect();
        ManagementEventWatcher w = new ManagementEventWatcher(scope, new WqlEventQuery("SELECT * FROM HID_EVENT20"));
        w.Options.Timeout = TimeSpan.FromMilliseconds(500);
        w.EventArrived += delegate(object s, EventArrivedEventArgs e)
        {
            try
            {
                string vals = "";
                foreach (PropertyData pd in e.NewEvent.Properties)
                {
                    byte[] b = pd.Value as byte[];
                    if (b != null)
                        vals += pd.Name + "=" + BitConverter.ToString(b).Replace('-', ' ') + " ";
                    else
                        vals += pd.Name + "=" + (pd.Value == null ? "null" : pd.Value.ToString()) + " ";
                }
                lock (Lines)
                {
                    Lines.Add(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + vals);
                    Lines.Add("");
                }
            }
            catch (Exception ex) { lock (Lines) { Lines.Add("ERR " + ex.Message); } }
        };
        w.Start();
        Done.Wait(TimeSpan.FromSeconds(seconds));
        w.Stop();
        Lines.Add("=== 监听结束 ===");
    }
}
'@

[EvWatch]::Run(60) | Out-Null
