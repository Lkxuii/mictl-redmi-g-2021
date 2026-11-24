// SET/GET 档位往返验证
using System;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class SetProbe
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

    static byte[] Call(byte op, byte fn, byte[] payload, out int execHr)
    {
        execHr = -1;
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
        return result;
    }

    static string Hex(byte[] b)
    {
        if (b == null) return "(null)";
        return BitConverter.ToString(b).Replace('-', ' ');
    }

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "setprobe-result.txt";
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

            sb.AppendLine("=== 档位往返测试 ===");
            sb.AppendLine("1. GET  fn8  初始     : " + Hex(Call(GET, 8, null, out hr)));
            Thread.Sleep(300);

            sb.AppendLine("2. SET  fn8  -> 2 静谧: " + Hex(Call(SET, 8, new byte[] { 2 }, out hr)));
            Thread.Sleep(800);
            sb.AppendLine("3. GET  fn8  回读     : " + Hex(Call(GET, 8, null, out hr)));
            Thread.Sleep(300);

            sb.AppendLine("4. SET  fn8  -> 1 性能: " + Hex(Call(SET, 8, new byte[] { 1 }, out hr)));
            Thread.Sleep(800);
            sb.AppendLine("5. GET  fn8  回读     : " + Hex(Call(GET, 8, null, out hr)));
            Thread.Sleep(300);

            sb.AppendLine("6. SET  fn8  -> 3 极速: " + Hex(Call(SET, 8, new byte[] { 3 }, out hr)));
            Thread.Sleep(800);
            sb.AppendLine("7. GET  fn8  回读     : " + Hex(Call(GET, 8, null, out hr)));

            sb.AppendLine("");
            sb.AppendLine("=== SET 后 fn13/fn22 复查 ===");
            sb.AppendLine("GET fn13 风扇: " + Hex(Call(GET, 13, null, out hr)));
            sb.AppendLine("GET fn22 温度: " + Hex(Call(GET, 22, null, out hr)));
        }
        catch (Exception ex)
        {
            sb.AppendLine("FATAL: " + ex.ToString());
        }
        File.WriteAllText(outFile, sb.ToString());
        return 0;
    }
}
