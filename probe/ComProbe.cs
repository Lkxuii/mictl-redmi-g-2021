// 原生 COM IWbemServices::ExecMethod 探测
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

class ComProbe
{
    [ComImport, Guid("DC12A681-737F-11CF-884D-00AA004B2E24")]
    class WbemLocator { }

    [ComImport, Guid("DC12A680-737F-11CF-884D-00AA004B2E24"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IWbemLocator
    {
        int CreateInstanceEnum([In, MarshalAs(UnmanagedType.BStr)] string strNamespace, [In] int lFlags, [In] IntPtr pCtx, [In, Out] ref IntPtr pServices);
        int CreateInstanceEnum2();
        int ConnectServer([In, MarshalAs(UnmanagedType.BStr)] string strNetworkResource, [In, MarshalAs(UnmanagedType.BStr)] string strUser, [In, MarshalAs(UnmanagedType.BStr)] string strPassword, [In, MarshalAs(UnmanagedType.BStr)] string strLocale, [In] int lSecurityFlags, [In, MarshalAs(UnmanagedType.BStr)] string strAuthority, [In] IntPtr pCtx, [In, Out] ref IntPtr ppNamespace);
    }

    [ComImport, Guid("7C857801-7381-11CF-884D-00AA004B2E24"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IWbemServices
    {
        // vtable order matters
        int OpenNamespace([In, MarshalAs(UnmanagedType.BStr)] string strNamespace, [In] int lFlags, [In] IntPtr pCtx, [In, Out] ref IntPtr ppWorking, [Out] IntPtr ppResult);
        int CancelAsyncCall([In] IntPtr pSink);
        int QueryObjectSink([In] int lFlags, [Out] out IntPtr ppResponseHandler);
        int GetObject([In, MarshalAs(UnmanagedType.BStr)] string strObjectPath, [In] int lFlags, [In] IntPtr pCtx, [In, Out] ref IntPtr ppObject, [Out] IntPtr ppCallResult);
        int GetObject2();
        int PutClass([In] IntPtr pObject, [In] int lFlags, [In] IntPtr pCtx, [Out] IntPtr ppCallResult);
        int DeleteClass([In, MarshalAs(UnmanagedType.BStr)] string strPath, [In] int lFlags, [In] IntPtr pCtx, [Out] IntPtr ppCallResult);
        int GetClass([In, MarshalAs(UnmanagedType.BStr)] string strPath, [In] int lFlags, [In] IntPtr pCtx, [In, Out] ref IntPtr ppObject, [Out] IntPtr ppCallResult);
        int PutInstance([In] IntPtr pInst, [In] int lFlags, [In] IntPtr pCtx, [Out] IntPtr ppCallResult);
        int DeleteInstance([In, MarshalAs(UnmanagedType.BStr)] string strPath, [In] int lFlags, [In] IntPtr pCtx, [Out] IntPtr ppCallResult);
        int CreateInstanceEnum([In, MarshalAs(UnmanagedType.BStr)] string strFilter, [In] int lFlags, [In] IntPtr pCtx, [Out] out IntPtr ppEnum);
        int CreateClassEnum([In, MarshalAs(UnmanagedType.BStr)] string strSuperClass, [In] int lFlags, [In] IntPtr pCtx, [Out] out IntPtr ppEnum);
        int ExecQuery([In, MarshalAs(UnmanagedType.BStr)] string strQueryLanguage, [In, MarshalAs(UnmanagedType.BStr)] string strQuery, [In] int lFlags, [In] IntPtr pCtx, [Out] out IntPtr ppEnum);
        int ExecNotificationQuery([In, MarshalAs(UnmanagedType.BStr)] string strQueryLanguage, [In, MarshalAs(UnmanagedType.BStr)] string strQuery, [In] int lFlags, [In] IntPtr pCtx, [Out] out IntPtr ppEnum);
        int ExecMethod([In, MarshalAs(UnmanagedType.BStr)] string strObjectPath, [In, MarshalAs(UnmanagedType.BStr)] string strMethodName, [In] int lFlags, [In] IntPtr pCtx, [In] IntPtr pInParams, [In, Out] ref IntPtr ppOutParams, [Out] IntPtr ppCallResult);
    }

    [DllImport("ole32.dll")]
    static extern int CoInitializeEx(IntPtr pv, uint coInit);
    [DllImport("ole32.dll")]
    static extern int CoInitializeSecurity(IntPtr pSecDesc, int cAuthSvc, IntPtr asAuthSvc, IntPtr pReserved1, uint dwAuthnLevel, uint dwImpLevel, IntPtr pAuthList, uint dwCapabilities, IntPtr pReserved3);
    [DllImport("ole32.dll")]
    static extern int CoCreateInstance(ref Guid clsid, IntPtr punkOuter, uint dwClsContext, ref Guid iid, [Out] out IntPtr ppv);

    [DllImport("oleaut32.dll")]
    static extern int SafeArrayCreateVector(int vt, int lLbound, int cElements);
    [DllImport("oleaut32.dll")]
    static extern int SafeArrayPutElement(IntPtr psa, int[] idx, ref byte data);
    [DllImport("oleaut32.dll")]
    static extern int SafeArrayDestroy(IntPtr psa);

    static int Main(string[] args)
    {
        string outFile = args.Length > 0 ? args[0] : "comprobe-result.txt";
        StringBuilder sb = new StringBuilder();
        try
        {
            CoInitializeEx(IntPtr.Zero, 0x2);
            // RPC_C_AUTHN_LEVEL_PKT_PRIVACY=6, RPC_C_IMP_LEVEL_IMPERSONATE=3, EOAC_NONE=0
            int hr = CoInitializeSecurity(IntPtr.Zero, -1, IntPtr.Zero, IntPtr.Zero, 6, 3, IntPtr.Zero, 0, IntPtr.Zero);
            sb.AppendLine("CoInitializeSecurity hr=0x" + hr.ToString("X8"));

            Guid locClsid = new Guid("4590f811-1d3d-11d0-891f-00aa004b2e24");
            Guid locIid = new Guid("DC12A680-737F-11CF-884D-00AA004B2E24");
            IntPtr pLoc;
            hr = CoCreateInstance(ref locClsid, IntPtr.Zero, 1, ref locIid, out pLoc);
            sb.AppendLine("CoCreateInstance(WbemLocator) hr=0x" + hr.ToString("X8"));

            IWbemLocator loc = (IWbemLocator)Marshal.GetObjectForIUnknown(pLoc);
            IntPtr pSvc = IntPtr.Zero;
            hr = loc.ConnectServer(@"ROOT\WMI", null, null, null, 0, null, IntPtr.Zero, ref pSvc);
            sb.AppendLine("ConnectServer hr=0x" + hr.ToString("X8"));

            IWbemServices svc = (IWbemServices)Marshal.GetObjectForIUnknown(pSvc);

            // 32 字节输入
            IntPtr psa = SafeArrayCreateVector(0x11 /*VT_UI1*/, 0, 32);
            int[] idx = new int[1];
            byte[] buf = new byte[32];
            buf[1] = 250; buf[3] = 13;
            for (int i = 0; i < 32; i++) { idx[0] = i; byte b = buf[i]; SafeArrayPutElement(psa, idx, ref b); }

            // 创建 in 参数对象：从类定义 GetObject + SpawnInstance? 简化：直接用 GetMethod? IWbemClassObject 接口未声明——
            // 改用 .NET 包装混合：不行，纯 COM 需要 IWbemClassObject vtable。这里完整声明。
            // —— 由于工程量大，先用 Microsoft 的包装：System.Management 在同进程内创建的 COM 对象可复用安全上下文。
            sb.AppendLine("(省略纯 COM ExecMethod —— 见 Probe2)");
        }
        catch (Exception ex)
        {
            sb.AppendLine("FATAL " + ex);
        }
        File.WriteAllText(outFile, sb.ToString());
        return 0;
    }
}
