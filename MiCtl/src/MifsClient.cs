// MIFS WMI 客户端 —— Bitland MIFS (MiInterface) 协议封装
// 已在 Redmi G 2021 锐龙版 (TIMI A7S) 实测验证：
//  - 必须通过实例路径 ExecMethod（类路径报 WBEM_E_INVALID_METHOD）
//  - System.Management 高级封装不可用，须直接用 COM 接口（vtable 顺序来自 System.Management 内部声明）
//  - 输出缓冲：OutData[1]=状态(0x80成功/0xE0不支持) OutData[3]=功能码回显 OutData[4..]=数据
using System;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MiCtl
{
    public sealed class MifsClient : IDisposable
    {
        // ---------- COM interop（vtable 顺序权威，勿改动方法顺序） ----------
        [ComImport, Guid("9556DC99-828C-11CF-A37E-00AA003240C7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWbemServices
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
        private interface IWbemClassObject
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
        private static extern int VariantClear(IntPtr pv);

        // ---------- MIFS 协议常量 ----------
        private const string InstPath = "MICommonInterface.InstanceName='ACPI\\PNP0C14\\MIFS_0'";
        private const byte OP_GET = 250;
        private const byte OP_SET = 251;
        private const byte STATUS_OK = 0x80;

        public static class Fn
        {
            public const byte PerMode = 8;          // 性能档位：固件真实映射 2=静谧 1=均衡 0/3=极速（见 Profile 枚举，勿按字面序理解）
            public const byte GpuMode = 9;          // 本机 GET 返回的是风扇/温度状态（见 GetFanTempStatus）；SET 的定义是 GPU 模式切换（未验证）
            public const byte KbdType = 10;
            public const byte FnLock = 11;          // 本机读取恒为 1，不是 Fn 锁状态；Fn 锁状态在 EC 事件 id 7
            public const byte TpLock = 12;          // 触控板锁: 0/1
            public const byte FanSpeeds = 13;       // 风扇转速读（本 BIOS 不支持）
            public const byte RgbKbMode = 16;       // 背光模式: 0=off 1=cyclic 2=fixed 3=custom
            public const byte RgbKbColor = 17;
            public const byte RgbKbBrightness = 18; // 背光亮度: 0-3
            public const byte AcType = 19;
            public const byte MaxFanSwitch = 20;    // 风扇最大开关（本 BIOS 不支持）
            public const byte MaxFanSpeed = 21;     // （本 BIOS 不支持）
            public const byte CpuTherm = 22;        // CPU 温度读（本 BIOS 不支持）
            public const byte CpuPower = 23;
        }

        /// <summary>
        /// 性能档位（本机实测固件映射，Redmi G 2021 / TIMI A7S）：
        /// WMI 值 2 = 静谧，1 = 均衡，0 和 3 都 = 极速（固件只有三档，
        /// Bitland 的 WMI 值序与字面含义不符：Linux 驱动的 balanced(0) 实际落固件极速档）。
        /// </summary>
        public enum Profile : byte
        {
            Quiet = 2,      // 静谧
            Balanced = 1,   // 均衡
            FullSpeed = 3,  // 极速（写入 0 或 3 固件效果相同，用 3 与官方 Fn+K 行为一致）
        }

        /// <summary>把 GET 回读的原始值映射到档位（0/3=极速，1=均衡，2=静谧）</summary>
        public static Profile? FromRaw(byte raw)
        {
            switch (raw)
            {
                case 2: return Profile.Quiet;
                case 1: return Profile.Balanced;
                case 0:
                case 3: return Profile.FullSpeed;
                default: return null;
            }
        }

        private ManagementScope _scope;
        private IWbemServices _svc;
        private IWbemClassObject _inDef;
        private readonly object _lock = new object();

        public bool Available { get; private set; }
        public string InitError { get; private set; }

        public MifsClient()
        {
            try
            {
                _scope = new ManagementScope(@"root\WMI");
                _scope.Connect();
                // 借用 System.Management 内部建立的 IWbemServices 连接（反射取私有字段）
                FieldInfo fi = typeof(ManagementScope).GetField("wbemServices",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi == null)
                    throw new InvalidOperationException("ManagementScope.wbemServices 字段不可用（框架版本不匹配）");
                _svc = (IWbemServices)fi.GetValue(_scope);

                IntPtr pClass;
                int hr = _svc.GetObject("MICommonInterface", 0, IntPtr.Zero, out pClass, IntPtr.Zero);
                if (hr != 0)
                    throw new InvalidOperationException("GetObject(MICommonInterface) hr=0x" + hr.ToString("X8"));
                IWbemClassObject cls = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pClass, typeof(IWbemClassObject));

                IntPtr pInDef, pOutDef;
                hr = cls.GetMethod("MiInterface", 0, out pInDef, out pOutDef);
                if (hr != 0)
                    throw new InvalidOperationException("GetMethod(MiInterface) hr=0x" + hr.ToString("X8"));
                _inDef = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pInDef, typeof(IWbemClassObject));

                Available = true;
            }
            catch (Exception ex)
            {
                Available = false;
                InitError = ex.Message;
            }
        }

        /// <summary>
        /// 一次 MIFS 调用。返回 28 字节数据区（Linux struct bitland_mifs_output.data 对应段），
        /// 失败/不支持返回 null。
        /// </summary>
        private byte[] Call(bool set, byte fn, byte[] payload, out int execHr)
        {
            execHr = -1;
            if (!Available) return null;
            lock (_lock)
            {
                try
                {
                    IntPtr pInInst;
                    int hr = _inDef.SpawnInstance(0, out pInInst);
                    if (hr != 0) return null;
                    IWbemClassObject inInst = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pInInst, typeof(IWbemClassObject));

                    byte[] buf = new byte[32];
                    buf[1] = set ? OP_SET : OP_GET;
                    buf[3] = fn;
                    if (payload != null)
                        Array.Copy(payload, 0, buf, 4, Math.Min(payload.Length, 28));

                    IntPtr varBuf = Marshal.AllocCoTaskMem(64);
                    Marshal.GetNativeVariantForObject((object)buf, varBuf);
                    hr = inInst.Put("InData", 0, varBuf, 0x2011); // CIM_UINT8 | CIM_FLAG_ARRAY
                    VariantClear(varBuf);
                    Marshal.FreeCoTaskMem(varBuf);
                    if (hr != 0) return null;

                    IntPtr pOutInst;
                    hr = _svc.ExecMethod(InstPath, "MiInterface", 0, IntPtr.Zero, pInInst, out pOutInst, IntPtr.Zero);
                    if (hr != 0)
                    {
                        execHr = hr;
                        return null;
                    }

                    IWbemClassObject outInst = (IWbemClassObject)Marshal.GetTypedObjectForIUnknown(pOutInst, typeof(IWbemClassObject));

                    IntPtr varOut = Marshal.AllocCoTaskMem(64);
                    hr = outInst.Get("OutData", 0, varOut, IntPtr.Zero, IntPtr.Zero);
                    byte[] outData = null;
                    if (hr == 0)
                    {
                        object o = Marshal.GetObjectForNativeVariant(varOut);
                        outData = o as byte[];
                    }
                    VariantClear(varOut);
                    Marshal.FreeCoTaskMem(varOut);

                    uint reserved = 0;
                    IntPtr varRes = Marshal.AllocCoTaskMem(64);
                    hr = outInst.Get("Reserved", 0, varRes, IntPtr.Zero, IntPtr.Zero);
                    if (hr == 0)
                    {
                        object o = Marshal.GetObjectForNativeVariant(varRes);
                        if (o != null)
                        {
                            try { reserved = Convert.ToUInt32(o); }
                            catch { }
                        }
                    }
                    VariantClear(varRes);
                    Marshal.FreeCoTaskMem(varRes);

                    if (outData == null || outData.Length < 30) return null;
                    if (outData[1] != STATUS_OK) return null;          // 0xE0 = 不支持/失败
                    if (outData[3] != fn && outData[3] != 0) return null; // 功能码回显校验（SET 成功时回显为 0）

                    // 数据区 = OutData[4..29] + Reserved(2字节小端) = 28 字节
                    byte[] data = new byte[28];
                    Array.Copy(outData, 4, data, 0, 26);
                    data[26] = (byte)(reserved & 0xFF);
                    data[27] = (byte)((reserved >> 8) & 0xFF);
                    return data;
                }
                catch
                {
                    return null;
                }
            }
        }

        public byte[] Get(byte fn)
        {
            int hr;
            return Call(false, fn, null, out hr);
        }

        public bool Set(byte fn, byte[] payload)
        {
            int hr;
            return Call(true, fn, payload, out hr) != null;
        }

        // ---------- 高层 API ----------

        /// <summary>读当前性能档位；失败返回 null</summary>
        public Profile? GetProfile()
        {
            byte[] d = Get(Fn.PerMode);
            if (d == null) return null;
            return FromRaw(d[0]);
        }

        /// <summary>
        /// 风扇与温度状态（fn9 GET）——游戏盒子"监控器"页面的同源数据（它每 2 秒轮询一次）。
        /// Linux 驱动把 fn9 标为 GPU_MODE，但本机 GET 返回的其实是这个状态包，其余字节恒为 0：
        ///   data[0..1] LE16 = 风扇转速 A   data[2..3] LE16 = 风扇转速 B
        ///   data[6]  = CPU 温度（°C，实测随 CPU 满载从 49 升到 69）
        ///   data[10] = GPU 温度（°C，实测与 nvidia-smi 逐秒一致）
        /// 风扇 A/B 哪路是 CPU、哪路是 GPU 是按字段排布推断的（轻载下两路数值接近，未能实测区分）。
        /// </summary>
        public sealed class FanTempStatus
        {
            public int CpuRpm;
            public int GpuRpm;
            public int CpuTemp = -1;
            public int GpuTemp = -1;
        }

        public FanTempStatus GetFanTempStatus()
        {
            byte[] d = Get(Fn.GpuMode); // fn9
            if (d == null) return null;
            int r1 = d[0] | (d[1] << 8);
            int r2 = d[2] | (d[3] << 8);
            if (r1 < 100 || r1 > 9000 || r2 < 100 || r2 > 9000) return null; // 不像转速
            FanTempStatus s = new FanTempStatus();
            s.CpuRpm = r1;
            s.GpuRpm = r2;
            if (d[6] > 0 && d[6] < 128) s.CpuTemp = d[6];
            if (d[10] > 0 && d[10] < 128) s.GpuTemp = d[10];
            return s;
        }

        /// <summary>设置性能档位</summary>
        public bool SetProfile(Profile p)
        {
            return Set(Fn.PerMode, new byte[] { (byte)p });
        }

        /// <summary>
        /// 发送 GPU 模式切换指令（fn9 SET，0=混合 1=独显直连 2=UMA，定义来自 Linux bitland-mifs-wmi 驱动）。
        /// 本机只确认过"固件返回成功"；指令是否真的改变 GPU 模式尚未验证（要重启才看得到）。
        /// 当前模式无法用 fn9 GET 读取——GET 返回的是风扇/温度状态，不是模式。
        /// </summary>
        public bool SetGpuMode(int mode)
        {
            return Set(Fn.GpuMode, new byte[] { (byte)mode });
        }

        /// <summary>探测功能码在本 BIOS 是否支持（只 GET）</summary>
        public bool IsGetSupported(byte fn)
        {
            return Get(fn) != null;
        }

        public void Dispose()
        {
            _inDef = null;
            _svc = null;
            _scope = null;
        }
    }
}
