// 枚举 ExecMethod 输出对象的全部属性
using System;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

class ComProbe4
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

    static StringBuilder sb = new StringBuilder();
    [DllImport("oleaut32.dll")] static extern int VariantClear(IntPtr pv);
    [DllImport("oleaut32.dll")] static extern void VariantInit(IntPtr pv);

    // 枚举对象所有属性
    static void DumpProps(string label, IWbemClassObject obj)
    {
        try
        {
            // GetObjectText 更全
            IntPtr pText;
            int hr = obj.GetObjectText(0, out pText);
            if (hr == 0)
            {
                string text = Marshal.PtrToStringBSTR(pText);
                sb.AppendLine(label + " ObjectText:\r\n" + text);
                Marshal.FreeBSTR(pText);
            }
            else sb.AppendLine(label + " GetObjectText hr=0x" + hr.ToString("X8"));
        }
        catch (Exception ex) { sb.AppendLine(label + " DumpProps EX: " + ex.Message); }
    }

    static byte[] GetByteArray(IWbemClassObject obj, string prop)
    {
        IntPtr varBuf = Marshal.AllocCoTaskMem(64);
        VariantInit(varBuf);
        int hr = obj.Get(prop, 0, varBuf, IntPtr.Zero, IntPtr.Zero);
        byte[] result = null;
        if (hr == 0)
        {
            object o = Marshal.GetObjectForNativeVariant(varBuf);
            result = o as byte[];
            if (result == null && o != null) sb.AppendLine(prop + " 类型: " + o.GetType().FullName);
        }
        else sb.AppendLine("Get(" + prop + ") hr=0x" + hr.ToString("X8"));
        VariantClear(varBuf);
        Marshal.FreeCoTaskMem(varBuf);
        return result;
    }

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "comprobe4-result.txt";
        try
        {
            ManagementScope scope = new ManagementScope(@"root\WMI");
            scope.Connect();
            FieldInfo fi = typeof(ManagementScope).GetField("wbemServices", BindingFlags.NonPublic | BindingFlags.Instance);
            IWbemServices svc = (IWbemServices)fi.GetValue(scope);

            string instPath = "MICommonInterface.InstanceName='ACPI\\PNP0C14\\MIFS_0'";

            IntPtr pClass;
            int hr = svc.GetObject("MICommonInterface", 0, IntPtr.Zero, out pClass, IntPtr.Zero);
            IWbemClassObject cls = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pClass, typeof(IWbemClassObject));

            // 方法签名
            IntPtr pInDef, pOutDef;
            hr = cls.GetMethod("MiInterface", 0, out pInDef, out pOutDef);
            sb.AppendLine("GetMethod hr=0x" + hr.ToString("X8"));
            sb.AppendLine("--- InSignature ---");
            if (pInDef != IntPtr.Zero)
                DumpProps("IN", (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pInDef, typeof(IWbemClassObject)));
            else sb.AppendLine("IN signature: (null)");
            sb.AppendLine("--- OutSignature ---");
            if (pOutDef != IntPtr.Zero)
                DumpProps("OUT", (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pOutDef, typeof(IWbemClassObject)));
            else sb.AppendLine("OUT signature: (null)");

            // 调用
            IWbemClassObject inDef = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pInDef, typeof(IWbemClassObject));
            IntPtr pInInst;
            inDef.SpawnInstance(0, out pInInst);
            IWbemClassObject inInst = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pInInst, typeof(IWbemClassObject));
            byte[] buf = new byte[32];
            buf[1] = 250;
            buf[3] = 13;
            IntPtr varBuf = Marshal.AllocCoTaskMem(64);
            Marshal.GetNativeVariantForObject((object)buf, varBuf);
            hr = inInst.Put("InData", 0, varBuf, 0x2011);
            sb.AppendLine("Put(InData) hr=0x" + hr.ToString("X8"));
            VariantClear(varBuf);
            Marshal.FreeCoTaskMem(varBuf);

            IntPtr pOutInst;
            hr = svc.ExecMethod(instPath, "MiInterface", 0, IntPtr.Zero, pInInst, out pOutInst, IntPtr.Zero);
            sb.AppendLine("ExecMethod hr=0x" + hr.ToString("X8"));
            if (hr == 0)
            {
                sb.AppendLine("--- 输出对象 ---");
                if (pOutInst != IntPtr.Zero)
                    DumpProps("OUTINST", (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pOutInst, typeof(IWbemClassObject)));
                else sb.AppendLine("输出对象: (null)");
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
