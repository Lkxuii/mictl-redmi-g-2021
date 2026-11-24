// 全量语义探测：所有 GET 功能码 + 风扇转速连续采样
using System;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class SemProbe
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

    [DllImport("oleaut32.dll")] static extern int VariantClear(IntPtr pv);

    static StringBuilder sb = new StringBuilder();
    static IWbemClassObject inDef;
    static IWbemServices svc;
    const string InstPath = "MICommonInterface.InstanceName='ACPI\\PNP0C14\\MIFS_0'";

    // 一次 MIFS 调用，返回 OutData（30 字节）+ Reserved
    static byte[] Call(byte op, byte fn, byte[] payload, out int reserved)
    {
        reserved = -1;
        IntPtr pInInst;
        inDef.SpawnInstance(0, out pInInst);
        IWbemClassObject inInst = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pInInst, typeof(IWbemClassObject));
        byte[] buf = new byte[32];
        buf[1] = op;
        buf[3] = fn;
        if (payload != null)
            Array.Copy(payload, 0, buf, 4, Math.Min(payload.Length, 28));
        IntPtr varBuf = Marshal.AllocCoTaskMem(64);
        Marshal.GetNativeVariantForObject((object)buf, varBuf);
        int hr = inInst.Put("InData", 0, varBuf, 0x2011);
        VariantClear(varBuf);
        Marshal.FreeCoTaskMem(varBuf);
        if (hr != 0) return null;

        IntPtr pOutInst;
        hr = svc.ExecMethod(InstPath, "MiInterface", 0, IntPtr.Zero, pInInst, out pOutInst, IntPtr.Zero);
        if (hr != 0) return null;

        IWbemClassObject outInst = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pOutInst, typeof(IWbemClassObject));
        IntPtr varOut = Marshal.AllocCoTaskMem(64);
        hr = outInst.Get("OutData", 0, varOut, IntPtr.Zero, IntPtr.Zero);
        byte[] result = null;
        if (hr == 0)
        {
            object o = Marshal.GetObjectForNativeVariant(varOut);
            result = o as byte[];
        }
        VariantClear(varOut);
        Marshal.FreeCoTaskMem(varOut);

        IntPtr varRes = Marshal.AllocCoTaskMem(64);
        hr = outInst.Get("Reserved", 0, varRes, IntPtr.Zero, IntPtr.Zero);
        if (hr == 0)
        {
            object o = Marshal.GetObjectForNativeVariant(varRes);
            if (o != null) int.TryParse(o.ToString(), out reserved);
        }
        VariantClear(varRes);
        Marshal.FreeCoTaskMem(varRes);
        return result;
    }

    static string Hex(byte[] b)
    {
        if (b == null) return "(null)";
        return BitConverter.ToString(b).Replace('-', ' ');
    }

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "semprobe-result.txt";
        try
        {
            ManagementScope scope = new ManagementScope(@"root\WMI");
            scope.Connect();
            FieldInfo fi = typeof(ManagementScope).GetField("wbemServices", BindingFlags.NonPublic | BindingFlags.Instance);
            svc = (IWbemServices)fi.GetValue(scope);

            IntPtr pClass;
            svc.GetObject("MICommonInterface", 0, IntPtr.Zero, out pClass, IntPtr.Zero);
            IWbemClassObject cls = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pClass, typeof(IWbemClassObject));
            IntPtr pInDef, pOutDefTmp;
            cls.GetMethod("MiInterface", 0, out pInDef, out pOutDefTmp);
            inDef = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pInDef, typeof(IWbemClassObject));

            byte GET = 250;
            int reserved;

            sb.AppendLine("=== 全量 GET 探测 ===");
            sb.AppendLine("fn13 FAN_SPEEDS   : " + Hex(Call(GET, 13, null, out reserved)));
            sb.AppendLine("fn22 CPU_THERM    : " + Hex(Call(GET, 22, null, out reserved)));
            sb.AppendLine("fn8  PER_MODE     : " + Hex(Call(GET, 8, null, out reserved)));
            sb.AppendLine("fn19 AC_TYPE      : " + Hex(Call(GET, 19, null, out reserved)));
            sb.AppendLine("fn23 CPU_POWER    : " + Hex(Call(GET, 23, null, out reserved)));
            sb.AppendLine("fn21 MAX_FAN_SPEED: " + Hex(Call(GET, 21, null, out reserved)));
            sb.AppendLine("fn20 MAX_FAN_SWITCH: " + Hex(Call(GET, 20, null, out reserved)));
            sb.AppendLine("fn9  GPU_MODE     : " + Hex(Call(GET, 9, null, out reserved)));
            sb.AppendLine("fn11 FN_LOCK      : " + Hex(Call(GET, 11, null, out reserved)));
            sb.AppendLine("fn12 TP_LOCK      : " + Hex(Call(GET, 12, null, out reserved)));
            sb.AppendLine("fn10 KBD_TYPE     : " + Hex(Call(GET, 10, null, out reserved)));
            sb.AppendLine("fn18 RGB_BRIGHT   : " + Hex(Call(GET, 18, null, out reserved)));
            sb.AppendLine("fn16 RGB_MODE     : " + Hex(Call(GET, 16, null, out reserved)));

            sb.AppendLine("");
            sb.AppendLine("=== fn13 风扇转速 5 秒采样 ===");
            for (int i = 0; i < 10; i++)
            {
                byte[] o = Call(GET, 13, null, out reserved);
                if (o != null)
                {
                    int cpuLe = o[0] + 256 * o[1];
                    int gpuLe = o[2] + 256 * o[3];
                    int cpuBe = 256 * o[0] + o[1];
                    int gpuBe = 256 * o[2] + o[3];
                    sb.AppendLine(string.Format("t+{0,2}s  raw: {1}  | CPU LE={2} BE={3}  GPU LE={4} BE={5}",
                        i * 5, Hex(o), cpuLe, cpuBe, gpuLe, gpuBe));
                }
                Thread.Sleep(5000);
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("FATAL: " + ex.ToString());
        }
        File.WriteAllText(outFile, sb.ToString());
        return 0;
    }
}
