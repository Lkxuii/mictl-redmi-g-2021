// 原生 COM ExecMethod v3 —— 借用 System.Management 连接 + 权威 vtable 声明
using System;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

class ComProbe3
{
    // IWbemServices —— 按 System.Management 权威声明（同步/异步交错）
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

    // IWbemClassObject —— 24 方法权威顺序
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

    static StringBuilder sb = new StringBuilder();

    // 封装一次调用，返回输出缓冲 hex
    static void Run(IWbemServices svc, string label, string objectPath, byte op, byte fn, byte[] payload)
    {
        try
        {
            IntPtr pClass;
            int hr = svc.GetObject("MICommonInterface", 0, IntPtr.Zero, out pClass, IntPtr.Zero);
            sb.AppendLine(label + " GetObject hr=0x" + hr.ToString("X8"));
            if (hr != 0) return;

            IWbemClassObject cls = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pClass, typeof(IWbemClassObject));
            IntPtr pInDef, pOutDef;
            hr = cls.GetMethod("MiInterface", 0, out pInDef, out pOutDef);
            sb.AppendLine(label + " GetMethod hr=0x" + hr.ToString("X8"));
            if (hr != 0) return;

            IWbemClassObject inDef = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pInDef, typeof(IWbemClassObject));
            IntPtr pInInst;
            hr = inDef.SpawnInstance(0, out pInInst);
            sb.AppendLine(label + " SpawnInstance hr=0x" + hr.ToString("X8"));
            if (hr != 0) return;

            IWbemClassObject inInst = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pInInst, typeof(IWbemClassObject));
            byte[] buf = new byte[32];
            buf[1] = op;
            buf[3] = fn;
            if (payload != null)
                Array.Copy(payload, 0, buf, 4, Math.Min(payload.Length, 28));
            IntPtr varBuf = Marshal.AllocCoTaskMem(64);
            Marshal.GetNativeVariantForObject((object)buf, varBuf);
            hr = inInst.Put("InData", 0, varBuf, 0x2011); // CIM_UINT8 | CIM_FLAG_ARRAY
            sb.AppendLine(label + " Put(InData) hr=0x" + hr.ToString("X8"));
            VariantClear(varBuf);
            Marshal.FreeCoTaskMem(varBuf);
            if (hr != 0) return;

            IntPtr pOutInst;
            hr = svc.ExecMethod(objectPath, "MiInterface", 0, IntPtr.Zero, pInInst, out pOutInst, IntPtr.Zero);
            sb.AppendLine(label + " ExecMethod hr=0x" + hr.ToString("X8"));
            if (hr != 0) return;

            IWbemClassObject outInst = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pOutInst, typeof(IWbemClassObject));

            IntPtr varRet = Marshal.AllocCoTaskMem(64);
            hr = outInst.Get("ReturnValue", 0, varRet, IntPtr.Zero, IntPtr.Zero);
            if (hr == 0)
            {
                object o = Marshal.GetObjectForNativeVariant(varRet);
                sb.AppendLine(label + " ReturnValue=" + (o == null ? "(null)" : o.ToString()));
            }
            else sb.AppendLine(label + " Get(ReturnValue) hr=0x" + hr.ToString("X8"));
            VariantClear(varRet);
            Marshal.FreeCoTaskMem(varRet);

            IntPtr varOut = Marshal.AllocCoTaskMem(64);
            hr = outInst.Get("OutData", 0, varOut, IntPtr.Zero, IntPtr.Zero);
            if (hr == 0)
            {
                object o = Marshal.GetObjectForNativeVariant(varOut);
                byte[] arr = o as byte[];
                sb.AppendLine(label + " OutData=" + (arr == null ? "(not byte[])" : BitConverter.ToString(arr, 0, Math.Min(arr.Length, 32)).Replace('-', ' ')));
            }
            else sb.AppendLine(label + " Get(OutData) hr=0x" + hr.ToString("X8"));
            VariantClear(varOut);
            Marshal.FreeCoTaskMem(varOut);

            IntPtr varRes = Marshal.AllocCoTaskMem(64);
            hr = outInst.Get("Reserved", 0, varRes, IntPtr.Zero, IntPtr.Zero);
            if (hr == 0)
            {
                object o = Marshal.GetObjectForNativeVariant(varRes);
                sb.AppendLine(label + " Reserved=" + (o == null ? "(null)" : o.ToString()));
            }
            VariantClear(varRes);
            Marshal.FreeCoTaskMem(varRes);
        }
        catch (Exception ex)
        {
            sb.AppendLine(label + " EXCEPTION: " + ex.GetType().Name + " " + ex.Message);
        }
    }

    [DllImport("oleaut32.dll")]
    static extern int VariantClear(IntPtr pv);

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "comprobe3-result.txt";
        try
        {
            ManagementScope scope = new ManagementScope(@"root\WMI");
            scope.Connect();
            FieldInfo fi = typeof(ManagementScope).GetField("wbemServices", BindingFlags.NonPublic | BindingFlags.Instance);
            object svcRcw = fi.GetValue(scope);
            if (svcRcw == null)
            {
                File.WriteAllText(outFile, "wbemServices 字段为 null");
                return 1;
            }
            IWbemServices svc = (IWbemServices)svcRcw; // QI 9556DC99
            sb.AppendLine("拿到 IWbemServices RCW");

            string instPath = "MICommonInterface.InstanceName='ACPI\\PNP0C14\\MIFS_0'";
            Run(svc, "[1 实例路径 GET fn13]", instPath, 250, 13, null);
            Run(svc, "[2 类路径 GET fn13]", "MICommonInterface", 250, 13, null);
            Run(svc, "[3 实例路径 GET fn22]", instPath, 250, 22, null);
            Run(svc, "[4 实例路径 GET fn8]", instPath, 250, 8, null);
        }
        catch (Exception ex)
        {
            sb.AppendLine("FATAL: " + ex.ToString());
        }
        File.WriteAllText(outFile, sb.ToString());
        return 0;
    }
}
