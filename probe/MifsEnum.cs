// MIFS 功能码全枚举：GET 0-255，记录固件支持的所有功能码及其响应
using System;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

class MifsEnum
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

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "mifs-enum.txt";
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

            sb.AppendLine("=== MIFS GET 功能码全枚举（GET=250，只读） ===");
            sb.AppendLine("已知: 8=档位 9=GPU模式 10=KbdType 11=Fn锁 12=触控板 13=风扇转速 16=背光模式 18=背光亮度 19=AC类型 20=风扇最大开关 21=最大转速 22=CPU温度 23=CPU功耗");
            sb.AppendLine("");
            int supported = 0;
            for (int fn = 0; fn < 256; fn++)
            {
                int hr;
                byte[] d = Call(250, (byte)fn, null, out hr);
                if (d != null && d[1] == 0x80)
                {
                    supported++;
                    string hex = BitConverter.ToString(d, 0, 16).Replace('-', ' ');
                    sb.AppendLine("fn" + fn.ToString().PadLeft(3) + " (0x" + fn.ToString("X2") + "): SUPPORTED  data=" + hex);
                }
                // 控制台进度
                if (fn % 64 == 0) sb.AppendLine("... 扫描到 " + fn);
            }
            sb.AppendLine("");
            sb.AppendLine("支持的功能码总数: " + supported);
        }
        catch (Exception ex)
        {
            sb.AppendLine("FATAL: " + ex.ToString());
        }
        File.WriteAllText(outFile, sb.ToString());
        return 0;
    }
}
