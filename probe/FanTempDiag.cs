// 风扇/温度字段语义验证：fn9 (MIFS) 原始字节 vs LHM vs nvidia-smi
// 流程：空闲采样 5 次 → CPU 满载 12 秒期间采样 → 空闲采样 5 次（每秒一次）
// 用 CPU 负载让温度产生变化，观察 fn9 哪个字节跟随 CPU 温度
using System;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using LibreHardwareMonitor.Hardware;

class FanTempDiag
{
    [ComImport, Guid("9556DC99-828C-11CF-A37E-00AA003240C7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IWbemServices
    {
        int OpenNamespace(string strNamespace, int lFlags, IntPtr pCtx, out IntPtr ppWorking, IntPtr ppCallResult);
        int CancelAsyncCall(IntPtr pSink);
        int QueryObjectSink(int lFlags, out IntPtr ppResponseHandler);
        int GetObject(string strObjectPath, int lFlags, IntPtr pCtx, out IntPtr ppObject, IntPtr ppCallResult);
        int GetObjectAsync(string strObjectPath, int lFlags, IntPtr pCtx, IntPtr pResponseHandler);
        int PutClass(IntPtr pObject, int lFlags, IntPtr pCtx, IntPtr ppCallResult);
        int PutClassAsync(IntPtr pObject, int lFlags, IntPtr pCtx, IntPtr pResponseHandler);
        int DeleteClass(string strClass, int lFlags, IntPtr pCtx, IntPtr ppCallResult);
        int DeleteClassAsync(string strClass, int lFlags, IntPtr pCtx, IntPtr pResponseHandler);
        int CreateClassEnum(string strSuperclass, int lFlags, IntPtr pCtx, out IntPtr ppEnum);
        int CreateClassEnumAsync(string strSuperclass, int lFlags, IntPtr pCtx, IntPtr pResponseHandler);
        int PutInstance(IntPtr pInst, int lFlags, IntPtr pCtx, IntPtr ppCallResult);
        int PutInstanceAsync(IntPtr pInst, int lFlags, IntPtr pCtx, IntPtr pResponseHandler);
        int DeleteInstance(string strObjectPath, int lFlags, IntPtr pCtx, IntPtr ppCallResult);
        int DeleteInstanceAsync(string strObjectPath, int lFlags, IntPtr pCtx, IntPtr pResponseHandler);
        int CreateInstanceEnum(string strFilter, int lFlags, IntPtr pCtx, out IntPtr ppEnum);
        int CreateInstanceEnumAsync(string strFilter, int lFlags, IntPtr pCtx, IntPtr pResponseHandler);
        int ExecQuery(string strQueryLanguage, string strQuery, int lFlags, IntPtr pCtx, out IntPtr ppEnum);
        int ExecQueryAsync(string strQueryLanguage, string strQuery, int lFlags, IntPtr pCtx, IntPtr pResponseHandler);
        int ExecNotificationQuery(string strQueryLanguage, string strQuery, int lFlags, IntPtr pCtx, out IntPtr ppEnum);
        int ExecNotificationQueryAsync(string strQueryLanguage, string strQuery, int lFlags, IntPtr pCtx, IntPtr pResponseHandler);
        int ExecMethod(string strObjectPath, string strMethodName, int lFlags, IntPtr pCtx, IntPtr pInParams, out IntPtr ppOutParams, IntPtr ppCallResult);
    }

    [ComImport, Guid("DC12A681-737F-11CF-884D-00AA004B2E24"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IWbemClassObject
    {
        int GetQualifierSet(out IntPtr ppQualSet);
        int Get(string wszName, int lFlags, IntPtr pVal, IntPtr pType, IntPtr plFlavor);
        int Put(string wszName, int lFlags, IntPtr pVal, int cimType);
        int Delete(string wszName);
        int GetNames(string wszQualifierName, int lFlags, IntPtr pQualifierVal, out IntPtr pNames);
        int BeginEnumeration(int lEnumFlags);
        int Next(int lFlags, IntPtr strName, IntPtr pVal, IntPtr pType, IntPtr plFlavor);
        int EndEnumeration();
        int GetPropertyQualifierSet(string wszProperty, out IntPtr ppQualSet);
        int Clone(out IntPtr ppCopy);
        int GetObjectText(int lFlags, out IntPtr pstrObjectText);
        int SpawnDerivedClass(int lFlags, out IntPtr ppNewClass);
        int SpawnInstance(int lFlags, out IntPtr ppNewInstance);
        int CompareTo(int lFlags, IntPtr pCompareTo);
        int GetPropertyOrigin(string wszName, out IntPtr pstrClassName);
        int InheritsFrom(string strAncestor);
        int GetMethod(string wszName, int lFlags, out IntPtr ppInSignature, out IntPtr ppOutSignature);
        int PutMethod(string wszName, int lFlags, IntPtr pInSignature, IntPtr pOutSignature);
        int DeleteMethod(string wszName);
        int BeginMethodEnumeration(int lEnumFlags);
        int NextMethod(int lFlags, out IntPtr pstrName, out IntPtr ppInSignature, out IntPtr ppOutSignature);
        int EndMethodEnumeration();
        int GetMethodQualifierSet(string wszMethod, out IntPtr ppQualSet);
        int GetMethodOrigin(string wszMethodName, out IntPtr pstrClassName);
    }

    [DllImport("oleaut32.dll")]
    static extern int VariantClear(IntPtr pv);

    static IWbemClassObject inDef;
    static IWbemServices svc;
    static bool _load;
    const string InstPath = "MICommonInterface.InstanceName='ACPI\\PNP0C14\\MIFS_0'";

    // 返回完整 OutData（30 字节），不校验状态字节
    static byte[] Raw(byte op, byte fn)
    {
        IntPtr pInInst;
        inDef.SpawnInstance(0, out pInInst);
        IWbemClassObject inInst = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pInInst, typeof(IWbemClassObject));
        byte[] buf = new byte[32];
        buf[1] = op;
        buf[3] = fn;
        IntPtr v = Marshal.AllocCoTaskMem(64);
        Marshal.GetNativeVariantForObject((object)buf, v);
        int h = inInst.Put("InData", 0, v, 0x2011);
        VariantClear(v);
        Marshal.FreeCoTaskMem(v);
        if (h != 0) return null;

        IntPtr pOutInst;
        h = svc.ExecMethod(InstPath, "MiInterface", 0, IntPtr.Zero, pInInst, out pOutInst, IntPtr.Zero);
        if (h != 0) return null;

        IWbemClassObject o = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pOutInst, typeof(IWbemClassObject));
        IntPtr vo = Marshal.AllocCoTaskMem(64);
        h = o.Get("OutData", 0, vo, IntPtr.Zero, IntPtr.Zero);
        byte[] res = null;
        if (h == 0) res = Marshal.GetObjectForNativeVariant(vo) as byte[];
        VariantClear(vo);
        Marshal.FreeCoTaskMem(vo);
        return res;
    }

    static void LoadProc()
    {
        double x = 1.0;
        while (_load) { for (int i = 0; i < 2000000; i++) x = x * 1.0000001 + 0.1; }
        GC.KeepAlive(x);
    }

    static string Dump(IHardware hw, string tag)
    {
        StringBuilder s = new StringBuilder();
        s.Append(tag + "[");
        foreach (ISensor sen in hw.Sensors)
        {
            if (!sen.Value.HasValue) continue;
            bool take = sen.SensorType == SensorType.Temperature
                || (sen.SensorType == SensorType.Power && sen.Name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0)
                || (sen.SensorType == SensorType.Load && sen.Name == "GPU Core");
            if (take) s.Append(sen.Name + "=" + sen.Value.Value.ToString("0.#") + ";");
        }
        s.Append("] ");
        return s.ToString();
    }

    static string Nvsmi()
    {
        try
        {
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = @"C:\Windows\System32\nvidia-smi.exe";
            psi.Arguments = "--query-gpu=temperature.gpu,fan.speed,power.draw --format=csv,noheader";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
            {
                string s = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(3000);
                return s;
            }
        }
        catch (Exception ex) { return "ERR " + ex.Message; }
    }

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "fantemp-diag.txt";
        StringBuilder sb = new StringBuilder();
        try
        {
            ManagementScope scope = new ManagementScope(@"root\WMI");
            scope.Connect();
            FieldInfo fi = typeof(ManagementScope).GetField("wbemServices", BindingFlags.NonPublic | BindingFlags.Instance);
            svc = (IWbemServices)fi.GetValue(scope);

            IntPtr pClass;
            svc.GetObject("MICommonInterface", 0, IntPtr.Zero, out pClass, IntPtr.Zero);
            IWbemClassObject cls = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pClass, typeof(IWbemClassObject));
            IntPtr pInDef, pOutDef;
            cls.GetMethod("MiInterface", 0, out pInDef, out pOutDef);
            inDef = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pInDef, typeof(IWbemClassObject));

            Computer comp = new Computer();
            comp.IsCpuEnabled = true;
            comp.IsGpuEnabled = true;
            comp.Open();

            for (int i = 0; i < 22; i++)
            {
                if (i == 5)
                {
                    _load = true;
                    for (int k = 0; k < Environment.ProcessorCount; k++)
                    {
                        Thread t = new Thread(LoadProc);
                        t.IsBackground = true;
                        t.Start();
                    }
                }
                if (i == 17) _load = false;

                byte[] raw = Raw(250, 9);
                sb.AppendLine("[t=" + i + "s " + (_load ? "CPU-LOAD" : "idle") + "]");
                sb.AppendLine("  fn9 raw : " + (raw == null ? "(null)" : BitConverter.ToString(raw).Replace('-', ' ')));

                if (raw != null && raw.Length >= 30)
                {
                    byte[] d = new byte[26];
                    Array.Copy(raw, 4, d, 0, 26);
                    // d[] 索引与 MiCtl.MifsClient 返回的 data[] 一致
                    int r1 = d[0] | (d[1] << 8);
                    int r2 = d[2] | (d[3] << 8);
                    string nz = "";
                    for (int k = 0; k < 26; k++) if (d[k] != 0) nz += "d" + k + "=" + d[k] + " ";
                    sb.AppendLine("  status  : 0x" + raw[1].ToString("X2") + "  echo=" + raw[3]);
                    sb.AppendLine("  d[0..1] LE=" + r1 + "  d[2..3] LE=" + r2 + "   非零字节: " + nz);
                }

                string lhm = "";
                foreach (IHardware hw in comp.Hardware)
                {
                    try { hw.Update(); } catch { }
                    bool isCpu = hw.HardwareType == HardwareType.Cpu;
                    lhm += Dump(hw, isCpu ? "CPU" : "GPU") + (isCpu ? "" : hw.HardwareType + " ");
                    foreach (IHardware sub in hw.SubHardware)
                    {
                        try { sub.Update(); } catch { }
                        lhm += Dump(sub, "sub");
                    }
                }
                sb.AppendLine("  LHM     : " + lhm);
                sb.AppendLine("  nvidia  : temp,fan,power = " + Nvsmi());
                Thread.Sleep(1000);
            }

            comp.Close();
        }
        catch (Exception ex)
        {
            sb.AppendLine("FATAL: " + ex);
        }
        File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8);
        return 0;
    }
}
