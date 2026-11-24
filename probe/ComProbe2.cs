// 原生 COM ExecMethod 完整实现
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

class ComProbe2
{
    // ---- COM 声明 ----
    [ComImport, Guid("4590F811-1D3D-11D0-891F-00AA004B2E24")]
    class WbemLocator { }

    [ComImport, Guid("DC12A680-737F-11CF-884D-00AA004B2E24"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IWbemLocator
    {
        void Reserved();
        void Reserved2();
        int ConnectServer([MarshalAs(UnmanagedType.BStr)] string strNetworkResource,
                          [MarshalAs(UnmanagedType.BStr)] string strUser,
                          [MarshalAs(UnmanagedType.BStr)] string strPassword,
                          [MarshalAs(UnmanagedType.BStr)] string strLocale,
                          int lSecurityFlags,
                          [MarshalAs(UnmanagedType.BStr)] string strAuthority,
                          IntPtr pCtx,
                          out IntPtr ppNamespace);
    }

    [ComImport, Guid("7C857801-7381-11CF-884D-00AA004B2E24"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IWbemServices
    {
        int OpenNamespace(string strNamespace, int lFlags, IntPtr pCtx, IntPtr ppWorking, IntPtr ppResult);
        int CancelAsyncCall(IntPtr pSink);
        int QueryObjectSink(int lFlags, IntPtr ppResponseHandler);
        int GetObject(string strObjectPath, int lFlags, IntPtr pCtx, out IntPtr ppObject, IntPtr ppCallResult);
        int PutClass(IntPtr pObject, int lFlags, IntPtr pCtx, IntPtr ppCallResult);
        int DeleteClass(string strPath, int lFlags, IntPtr pCtx, IntPtr ppCallResult);
        int GetClass(string strPath, int lFlags, IntPtr pCtx, out IntPtr ppObject, IntPtr ppCallResult);
        int PutInstance(IntPtr pInst, int lFlags, IntPtr pCtx, IntPtr ppCallResult);
        int DeleteInstance(string strPath, int lFlags, IntPtr pCtx, IntPtr ppCallResult);
        int CreateInstanceEnum(string strFilter, int lFlags, IntPtr pCtx, out IntPtr ppEnum);
        int CreateClassEnum(string strSuperClass, int lFlags, IntPtr pCtx, out IntPtr ppEnum);
        int ExecQuery(string strQueryLanguage, string strQuery, int lFlags, IntPtr pCtx, out IntPtr ppEnum);
        int ExecNotificationQuery(string strQueryLanguage, string strQuery, int lFlags, IntPtr pCtx, out IntPtr ppEnum);
        int ExecMethod(string strObjectPath, string strMethodName, int lFlags, IntPtr pCtx, IntPtr pInParams, out IntPtr ppOutParams, IntPtr ppCallResult);
    }

    [ComImport, Guid("DC12A681-737F-11CF-884D-00AA004B2E24"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IWbemClassObject
    {
        int GetQualifierSet(out IntPtr ppQualSet);
        int Get(string wszName, int lFlags, IntPtr pVal, IntPtr pType, IntPtr plFlavor);
        int Put(string wszName, int lFlags, IntPtr pVal, short cimType);  // CIMTYPE = long in vtable
        int Delete(string wszName);
        int GetNames(string wszQualifierName, int lFlags, IntPtr pQualifierVal, IntPtr pNames);
        int BeginEnumeration(int lEnumFlags);
        int Next(int lFlags, IntPtr strName, IntPtr pVal, IntPtr pType, IntPtr plFlavor);
        int EndEnumeration();
        int GetPropertyQualifierSet(string wszProperty, out IntPtr ppQualSet);
        int Clone(out IntPtr ppCopy);
        int GetObjectText(int lFlags, out IntPtr strObjectText);
        int SpawnDerivedClass(int lFlags, out IntPtr ppNewClass);
        int SpawnInstance(int lFlags, out IntPtr ppNewInstance);
        int CompareTo(int lFlags, IntPtr pComparator);
        int GetPropertyOrigin(string wszPropertyName, out IntPtr pstrClassName);
        int GetMethod(string wszName, int lFlags, out IntPtr ppInSignature, out IntPtr ppOutSignature);
        int PutMethod(string wszName, int lFlags, IntPtr pInSignature, IntPtr pOutSignature);
        int DeleteMethod(string wszName);
        int GetMethodQualifierSet(string wszName, out IntPtr ppQualSet);
        int GetMethodOrigin(string wszMethodName, out IntPtr pstrClassName);
    }

    [DllImport("ole32.dll")]
    static extern int CoInitializeEx(IntPtr pv, uint coInit);
    [DllImport("ole32.dll")]
    static extern int CoInitializeSecurity(IntPtr pSecDesc, int cAuthSvc, IntPtr asAuthSvc, IntPtr pReserved1, uint dwAuthnLevel, uint dwImpLevel, IntPtr pAuthList, uint dwCapabilities, IntPtr pReserved3);
    [DllImport("ole32.dll")]
    static extern int CoCreateInstance(ref Guid clsid, IntPtr punkOuter, uint dwClsContext, ref Guid iid, [Out] out IntPtr ppv);
    [DllImport("oleaut32.dll")]
    static extern int VariantClear(IntPtr pv);
    [DllImport("oleaut32.dll")]
    static extern void VariantInit(IntPtr pv);

    // CIM type flags
    const short CIM_UINT8 = 17;      // 0x11
    const short CIM_FLAG_ARRAY = 0x2000;
    const short CIM_UINT16 = 18;     // 0x12

    static StringBuilder sb = new StringBuilder();

    static void TestCall(IWbemServices svc, string label, string objectPath, byte op, byte fn, bool fillOut)
    {
        try
        {
            // 1. 取类定义
            IntPtr pClass;
            int hr = svc.GetObject("MICommonInterface", 0, IntPtr.Zero, out pClass, IntPtr.Zero);
            sb.AppendLine(label + " GetObject(class) hr=0x" + hr.ToString("X8"));
            if (hr != 0) return;
            IWbemClassObject cls = (IWbemClassObject)Marshal.GetObjectForIUnknown(pClass);

            // 2. 取方法参数定义
            IntPtr pInDef, pOutDef;
            hr = cls.GetMethod("MiInterface", 0, out pInDef, out pOutDef);
            sb.AppendLine(label + " GetMethod hr=0x" + hr.ToString("X8"));
            if (hr != 0) return;
            IWbemClassObject inDef = (IWbemClassObject)Marshal.GetObjectForIUnknown(pInDef);

            // 3. SpawnInstance
            IntPtr pInInst;
            hr = inDef.SpawnInstance(0, out pInInst);
            sb.AppendLine(label + " SpawnInstance hr=0x" + hr.ToString("X8"));
            if (hr != 0) return;
            IWbemClassObject inInst = (IWbemClassObject)Marshal.GetObjectForIUnknown(pInInst);

            // 4. Put InData (byte[32])
            byte[] buf = new byte[32];
            buf[1] = op;
            buf[3] = fn;
            IntPtr varBuf = Marshal.AllocCoTaskMem(32); // VARIANT 是 24 字节，给 32 安全
            VariantInit(varBuf);
            Marshal.GetNativeVariantForObject((object)buf, varBuf);
            hr = inInst.Put("InData", 0, varBuf, (short)(CIM_UINT8 | CIM_FLAG_ARRAY));
            sb.AppendLine(label + " Put(InData) hr=0x" + hr.ToString("X8"));
            VariantClear(varBuf);
            Marshal.FreeCoTaskMem(varBuf);
            if (hr != 0) return;

            if (fillOut)
            {
                // 5. Put OutData (byte[30]) 与 Reserved (uint16 0)
                IntPtr pOutDefLocal = pOutDef;
                if (pOutDefLocal != IntPtr.Zero)
                {
                    IWbemClassObject outDef = (IWbemClassObject)Marshal.GetObjectForIUnknown(pOutDefLocal);
                    IntPtr pOutInstTmp;
                    hr = outDef.SpawnInstance(0, out pOutInstTmp);
                    // out 实例不能作为输入参数的一部分传给 ExecMethod —— out 参数由 provider 填充
                }
            }

            // 6. ExecMethod
            IntPtr pOutInst;
            hr = svc.ExecMethod(objectPath, "MiInterface", 0, IntPtr.Zero, pInInst, out pOutInst, IntPtr.Zero);
            sb.AppendLine(label + " ExecMethod(" + objectPath + ") hr=0x" + hr.ToString("X8"));
            if (hr != 0) return;

            IWbemClassObject outInst = (IWbemClassObject)Marshal.GetObjectForIUnknown(pOutInst);
            // 读 ReturnValue / OutData / Reserved
            IntPtr varRet = Marshal.AllocCoTaskMem(32);
            VariantInit(varRet);
            hr = outInst.Get("ReturnValue", 0, varRet, IntPtr.Zero, IntPtr.Zero);
            if (hr == 0)
            {
                object o = Marshal.GetObjectForNativeVariant(varRet);
                sb.AppendLine(label + " ReturnValue=" + (o ?? "(null)"));
            }
            VariantClear(varRet);
            Marshal.FreeCoTaskMem(varRet);

            IntPtr varOut = Marshal.AllocCoTaskMem(64);
            VariantInit(varOut);
            hr = outInst.Get("OutData", 0, varOut, IntPtr.Zero, IntPtr.Zero);
            if (hr == 0)
            {
                object o = Marshal.GetObjectForNativeVariant(varOut);
                byte[] arr = o as byte[];
                sb.AppendLine(label + " OutData=" + (arr == null ? "(null)" : BitConverter.ToString(arr, 0, Math.Min(arr.Length, 32)).Replace('-', ' ')));
            }
            VariantClear(varOut);
            Marshal.FreeCoTaskMem(varOut);

            IntPtr varRes = Marshal.AllocCoTaskMem(32);
            VariantInit(varRes);
            hr = outInst.Get("Reserved", 0, varRes, IntPtr.Zero, IntPtr.Zero);
            if (hr == 0)
            {
                object o = Marshal.GetObjectForNativeVariant(varRet);
                sb.AppendLine(label + " Reserved=" + (o ?? "(null)"));
            }
            VariantClear(varRes);
            Marshal.FreeCoTaskMem(varRes);
        }
        catch (Exception ex)
        {
            sb.AppendLine(label + " EXCEPTION: " + ex.GetType().Name + " " + ex.Message);
        }
    }

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "comprobe2-result.txt";
        try
        {
            CoInitializeEx(IntPtr.Zero, 0x0); // COINIT_MULTITHREADED=0x0
            int hr = CoInitializeSecurity(IntPtr.Zero, -1, IntPtr.Zero, IntPtr.Zero, 6, 3, IntPtr.Zero, 0, IntPtr.Zero);
            sb.AppendLine("CoInitializeSecurity hr=0x" + hr.ToString("X8"));

            Guid locClsid = new Guid("4590F811-1D3D-11D0-891F-00AA004B2E24");
            Guid locIid = new Guid("DC12A680-737F-11CF-884D-00AA004B2E24");
            IntPtr pLoc;
            hr = CoCreateInstance(ref locClsid, IntPtr.Zero, 1, ref locIid, out pLoc);
            sb.AppendLine("CoCreate hr=0x" + hr.ToString("X8"));
            if (hr != 0) throw new Exception("CoCreateInstance failed");

            IWbemLocator loc = (IWbemLocator)Marshal.GetObjectForIUnknown(pLoc);
            IntPtr pSvc;
            hr = loc.ConnectServer(@"ROOT\WMI", null, null, null, 0, null, IntPtr.Zero, out pSvc);
            sb.AppendLine("ConnectServer hr=0x" + hr.ToString("X8"));
            if (hr != 0) throw new Exception("ConnectServer failed");

            IWbemServices svc = (IWbemServices)Marshal.GetObjectForIUnknown(pSvc);

            string instPath = "MICommonInterface.InstanceName='ACPI\\PNP0C14\\MIFS_0'";
            TestCall(svc, "[1 类路径 onlyIn]", "MICommonInterface", 250, 13, false);
            TestCall(svc, "[2 实例路径 onlyIn]", instPath, 250, 13, false);
            TestCall(svc, "[3 类路径 fillOut]", "MICommonInterface", 250, 13, true);
            TestCall(svc, "[4 实例路径 fillOut]", instPath, 250, 13, true);
        }
        catch (Exception ex)
        {
            sb.AppendLine("FATAL: " + ex.Message);
        }
        File.WriteAllText(outFile, sb.ToString());
        return 0;
    }
}
