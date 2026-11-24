// 风扇控制通道探测：fn13 带参数 / fn20 SET
using System;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class FanProbe
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

    static byte[] Call(byte op, byte fn, byte[] payload, out int hr)
    {
        hr = -1;
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
        int h = inInst.Put("InData", 0, varBuf, 0x2011);
        VariantClear(varBuf);
        Marshal.FreeCoTaskMem(varBuf);
        if (h != 0) return null;

        IntPtr pOutInst;
        h = svc.ExecMethod(InstPath, "MiInterface", 0, IntPtr.Zero, pInInst, out pOutInst, IntPtr.Zero);
        if (h != 0) return null;

        IWbemClassObject outInst = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pOutInst, typeof(IWbemClassObject));
        IntPtr varOut = Marshal.AllocCoTaskMem(64);
        h = outInst.Get("OutData", 0, varOut, IntPtr.Zero, IntPtr.Zero);
        byte[] result = null;
        if (h == 0)
        {
            object o = Marshal.GetObjectForNativeVariant(varOut);
            result = o as byte[];
        }
        VariantClear(varOut);
        Marshal.FreeCoTaskMem(varOut);
        return result;
    }

    static string Hex(byte[] b)
    {
        if (b == null) return "(null)";
        return BitConverter.ToString(b).Replace('-', ' ');
    }

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "fanprobe-result.txt";
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

            byte GET = 250, SET = 251;
            int hr;

            sb.AppendLine("=== fn13 带参数尝试 ===");
            sb.AppendLine("GET fn13 无参      : " + Hex(Call(GET, 13, null, out hr)));
            sb.AppendLine("GET fn13 [0]       : " + Hex(Call(GET, 13, new byte[] { 0 }, out hr)));
            sb.AppendLine("GET fn13 [1]       : " + Hex(Call(GET, 13, new byte[] { 1 }, out hr)));
            sb.AppendLine("GET fn13 [2]       : " + Hex(Call(GET, 13, new byte[] { 2 }, out hr)));

            sb.AppendLine("");
            sb.AppendLine("=== fn20 SET（风扇最大开关）===");
            sb.AppendLine("SET fn20 [0,0] 关  : " + Hex(Call(SET, 20, new byte[] { 0, 0 }, out hr)));
            Thread.Sleep(500);
            sb.AppendLine("GET fn13 间隔后    : " + Hex(Call(GET, 13, null, out hr)));
            sb.AppendLine("SET fn20 [0,1] 开  : " + Hex(Call(SET, 20, new byte[] { 0, 1 }, out hr)));
            Thread.Sleep(1500);
            sb.AppendLine("GET fn13 间隔后    : " + Hex(Call(GET, 13, null, out hr)));
            sb.AppendLine("GET fn21 间隔后    : " + Hex(Call(GET, 21, null, out hr)));
            Thread.Sleep(1500);
            sb.AppendLine("SET fn20 [0,0] 复原: " + Hex(Call(SET, 20, new byte[] { 0, 0 }, out hr)));
            Thread.Sleep(500);
            sb.AppendLine("GET fn13 复原后    : " + Hex(Call(GET, 13, null, out hr)));

            sb.AppendLine("");
            sb.AppendLine("=== fn20 变体参数 ===");
            sb.AppendLine("SET fn20 [1,1]     : " + Hex(Call(SET, 20, new byte[] { 1, 1 }, out hr)));
            sb.AppendLine("SET fn20 [1,0]     : " + Hex(Call(SET, 20, new byte[] { 1, 0 }, out hr)));
        }
        catch (Exception ex)
        {
            sb.AppendLine("FATAL: " + ex.ToString());
        }
        File.WriteAllText(outFile, sb.ToString());
        return 0;
    }
}
