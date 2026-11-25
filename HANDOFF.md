# 交接文档

给下一位接手这个项目的人。本文假设你没有之前的对话记录。

硬件协议和各功能的验证状态，以 [MiCtl/README.md](MiCtl/README.md) 为准。本文讲的是：现在是什么状态、哪些没做完、怎么继续、别再踩哪些坑。

## 1. 项目是什么

给 **Redmi G 2021 锐龙版**（TIMI A7S，R7 5800H + RTX 3060，Windows 10 19045）写的常驻托盘控制台 `MiCtl`，对标小米游戏盒子（GamingBox）的硬件控制功能，但要更轻。

用户点名要过的功能：性能档位、风扇转速、CPU/GPU 温度与功耗、网速、禁用触控板、解锁 Fn、禁用 Win 键、GPU 模式、现代风格的界面。

技术栈：C# / WinForms / .NET Framework 4.8，用系统自带的 `csc.exe` 直接编译。唯一的第三方依赖是 LibreHardwareMonitorLib 0.9.6，用来取 GPU 负载/功耗和 CPU 功耗。

## 2. 接手时的状态（2026-11-25）

| 项 | 状态 |
|---|---|
| 仓库 | [Lkxuii/mictl-redmi-g-2021](https://github.com/Lkxuii/mictl-redmi-g-2021)，公开，分支 `main`；2026-11-26 已把界面重做与新功能提交推送（此前的所有工作都只在本地） |
| MiCtl 进程 | **11-26 00:57 已部署 1.1.0.0（应用图标 + 版本信息 + manifest）并运行中**。部署链路（覆盖写 out\MiCtl.exe + 提权启动）在火绒信任配置下全程无拦截 |
| 产物 | `MiCtl/out/MiCtl.exe` = 1.1.0.0（112128 字节）；工作区的全部改动均未提交，`main` 上还是最初的界面 |
| PawnIO 2.1.0 | 已安装，**重启电脑后复测通过**：服务自动加载，检查程序读到 CPU 功耗 14–25 W，CPU 温度两个来源一致（固件 60°C，传感器库 60.4°C） |
| 用户确认过的 | 风扇转速显示正常（旧界面，用户原话："转速没问题，cpu温度和功耗还是要的"）。新界面（11-25 晚起）日常使用中，未收到问题反馈，但也没有逐项确认 |
| 没人目视确认过的 | 真实窗口的细节（用户 11-25 晚自己部署后没有反馈问题，但也未逐项确认）：150% 缩放下是否锐利、过渡动画、悬停/焦点态、每 2 秒刷新的观感；"结束"进程按钮；网络页"重新检测"；电池充放电功率非零时的显示（本机满电，功率一直是 0） |

## 3. 仓库地图

```
MiCtl/src/
  MifsClient.cs        固件接口（COM 互操作 + 协议常量 + 解析）   ← 改协议先看这里
  FanSpeedWatcher.cs   EC 事件订阅（名字是历史遗留：它不提供转速）
  SensorService.cs     LibreHardwareMonitor 封装 + 网速
  AppController.cs     2 秒轮询线程；合并温度来源；执行自动切档；刷新触控板/自启的设备状态
  AutoProfile.cs       温度自动切档的判定
  AppSettings.cs       配置读写
  SystemInfo.cs        进程榜（同名合并、排除关键进程）、结束进程、WiFi（netsh）、延迟（ping）
  UiState.cs           界面状态快照（纯数据，不含 IO）
  MainWindow.cs        主窗口：只读 UiState，动作走 AppController    ← 改布局看这里
  Theme.cs             配色、DPI 缩放、字体缓存、GDI+ 辅助         ← 改外观看这里
  Controls.cs          自绘控件：温度环 / 滑动分段 / 开关 / 状态标签 / 步进器 / 按钮
  TrayContext.cs       托盘图标与菜单（StartupManager 也在这里）
  WinKeyHook.cs        Win 键钩子  TouchpadControl.cs  触控板设备禁用
  Program.cs           入口（单实例 + DPI 缩放初始化）
  app.manifest         requireAdministrator + 系统 DPI 感知（已并入 tools/app.res，随图标与版本信息一起在编译期嵌入）
MiCtl/assets/app.ico   应用图标（assets/make_icon.py 生成，多尺寸）
MiCtl/tools/make_res.py 生成 Win32 .res：图标 + 版本信息 + manifest（见第 6 节火绒条目）
MiCtl/build.ps1        编译脚本，依赖缺失时自动下载
MiCtl/preview.ps1      界面离屏预览：渲染 out/ui-preview-<sample|empty>-<control|system|network>-<缩放>x.png
MiCtl/deploy.ps1       部署：等旧实例退出 → 编译 → 以管理员启动（弹一次 UAC）
MiCtl/restart.ps1      提权：直接结束实例 → 编译 → 启动（本机火绒会拦，见第 6 节第 22 条）
probe/                 诊断工具源码 + 各阶段实测输出
                         UiPreview.cs 预览工具；SysProbe.cs → sysprobe.txt 系统数据诊断；AppCheck.cs → appcheck.txt 端到端检查
rev/                   GamingBoxHost.exe 的字符串表、RTTI 类名；gb-frontend-channels.txt 是前端 main.js 的通道名与片段
docs/                  火绒误报申诉材料（含报毒名、病毒 ID、提交渠道与正文模板）
extract-gb-channels.ps1  生成 rev/gb-frontend-channels.txt
clone-build-test.ps1   干净克隆构建验证：从 GitHub 克隆到 %TEMP% 后跑 build.ps1（推送后应跑一次）
*.ps1（根目录）         逆向过程脚本，多数是一次性的，含本机绝对路径
bitland-mifs-wmi.c      Linux 内核驱动参考副本（第三方，GPL）
```

根目录脚本里有两个要小心：

- `install-pawnio210.ps1`：**有问题的旧脚本**，会先 `sc delete PawnIO`，导致安装失败。装 PawnIO 用 `install-pawnio-clean.ps1`。
- `clean-pawnio.ps1`：会删除 PawnIO 的服务、驱动文件和安装目录。

`probe/` 里值得留意的：

- `AppCheck.cs` + `build-run-appcheck.ps1`：端到端检查，见第 7 节。
- `MifsEnum.cs` → `mifs-enum.txt`：256 个功能码全枚举的代码和输出。
- `FanTempDiag.cs` → `fantemp-diag.txt`：CPU 满载前后逐秒对比固件数据、传感器库、nvidia-smi，是 fn9 各字段语义的依据。
- `EcDump.cs`：经 PawnIO 直读 EC 的工具，**从未成功运行过**（当时等 UAC 超时被终止），不要假设它能用。

## 4. 已实测确认的结论

| 结论 | 依据 |
|---|---|
| 固件接口只在实例路径上响应 fn8 / fn9 / fn10 / fn11，其余功能码返回 `0xE0` | `probe/mifs-enum.txt` |
| fn8：`2` = 静谧、`1` = 均衡、`3` = 极速，写入后回读一致 | `probe/result-set.txt` |
| fn9 读：`data[0..1]` 风扇 A（小端 u16）、`data[2..3]` 风扇 B、`data[6]` CPU 温度、`data[10]` GPU 温度 | `probe/fantemp-diag.txt`：CPU 满载后 `data[6]` 从 49 升到 69；`data[10]` 与 nvidia-smi 逐秒一致 |
| CPU 温度两个独立来源一致：固件 69°C，传感器库经 PawnIO 读到 69.3–69.5°C | `probe/appcheck.txt` |
| EC 事件里只有触控板（id 6）、Fn 锁（id 7）、id 10 三种，没有风扇转速 | 一份 68 个事件的日志 |
| 触控板事件 `value 0` 时触控板不能用 | 用户对照确认 |
| Fn 锁语义（用户 2026-11-26 确认，纠正了他此前"fn 开 = 媒体键"的说法）：**锁定 = F1–F12 是正常功能键（F1 就是 F1）；解锁 = F1–F12 是媒体键（音量/亮度）** | 用户原话："锁定应该是正常的f1，解锁才是音量" |
| EC 事件 id 7 的 value 对应（已对照确认）：value 1 = 锁定（F1 是正常 F1），value 0 = 解锁（F1–F12 出音量/亮度） | 用户 2026-11-26 对照：界面 pill 显示"已锁定"与 F1 实际行为一致 |
| EC 事件 id 7 的 value 对应（已对照确认）：value 1 = 锁定（F1 是正常 F1），value 0 = 解锁（F1–F12 出音量/亮度） | 用户 2026-11-26 对照：界面 pill 显示"已锁定"与 F1 实际行为一致 |
| **fn11 SET 不能切换 Fn 锁**：SET 0/1 都返回成功，但 12 秒内无 EC 事件 id7、GET 恒为 1、行为无变化 | probe/FnLockProbe.cs → fnlock-probe.txt（提权探针） |
| root\WMI 里带方法的类只有 MICommonInterface 一个；游戏盒子封装的 Inventec/Quanta WMI 类（inventec_sync_operation::EnableFnKey 等）在本机不存在——它的 Fn 软件切换通道在这台机器上同样不可用 | root\WMI 全量类枚举（Get-CimClass）；rev/rtti.txt |
| GPU 模式识别（Radeon 集显不可见 = 独显直连）方向已验证：当前确为独显直连，界面识别一致；混合模式方向未验证（用户未切换过） | 用户确认（2026-11-26）："当前就是独显直连" |
| 新增监控项都能在本机读到真实值：CPU 负载、GPU 核心频率与显存、内存（使用率 / 已用 / 可用 / 频率）、电池、默认网卡与网速、WiFi、延迟、进程榜 | `probe/sysprobe.txt`（13:48 提权诊断，列出全部传感器）；`probe/appcheck.txt`（普通权限，5 轮） |
| 游戏盒子监控页的 GPU 频率与温度和传感器库同源 | 它显示 1567 MHz / 49°C，`probe/lhm-diag-final.txt` 里 `GPU Core` 时钟是 1567、温度 49 |
| 游戏盒子的监控数据由宿主进程推送（`register_cpu_usage`、`register_physicalMemory`、`register_gpu_clock_speed`、`register_network_adapter_ssid` 等），结束进程是 `gaming_os.kill_process`，这些都不走固件 | `rev/gb-frontend-channels.txt` |
| 游戏盒子在离电且处于极速档时会自动切回均衡（MiCtl 的自动联动里已有同样的限制） | 同上，前端里的 `0===t.ac&&…&&r===C.FAST&&qe(C.NORMAL)` |

fn9 的一条实际响应（30 字节）：

```
00 80 00 09 61 09 63 09 00 00 31 00 00 00 23 00 00 00 00 ...
   │     │  └风扇A=2401┘└风扇B=2403┘      │           │
   │     │                          CPU=49°C      GPU=35°C
 状态=成功  功能码回显
```

## 5. 没做完或没验证的

按建议的处理顺序排列。

### 第一优先：把"没验证"变成"验证过"

1. **新界面的真实窗口没有人目视过。** 离屏预览图检查过（`MiCtl/out/ui-preview-*.png`），但预览是一张静态图：真实窗口在 150% 缩放下是否清晰、温度环缓动/分段滑动/开关的过渡是否顺、悬停与键盘焦点态、实时数据每 2 秒刷新时的跳动，都要看真实窗口。
2. ~~Fn 锁的 value 对应待一次显式对照~~ **已解决（2026-11-26）**：语义与 value 对应均已确认（锁定 = 正常 F1，解锁 = F1–F12 出音量/亮度；value 1 = 锁定、0 = 解锁）。界面显示"已解锁/已锁定"，说明文字两个状态都写明。
3. **两路风扇谁是 CPU、谁是 GPU**。`data[0..1]` 被当作 CPU，是按字段排布推断的。轻载时两路数值接近分不出来，等跑游戏两路转速拉开时对照游戏盒子的左右两张卡片。用户说过满载是 4000 多转。
4. **从没实测过的功能**：界面开关禁用触控板（`TouchpadControl`，走 pnputil）、禁用 Win 键（`WinKeyHook`）、温度自动联动、开机自启（计划任务）。

### 第二优先：用户点名要、但没做到位的

5. **软件切换 Fn 锁：已验证不可行，不做开关**。fn11 SET 返回成功但不生效（探针实测，见第 4 节）；游戏盒子走的是 Inventec/Quanta WMI 类，本机没有这些类；EC 直写没有可靠偏移、风险高。所以 Fn 锁只有 Fn+Esc 一条切换路，界面保持状态显示（pill + 悬停显示官方解锁键位表）。若将来拿到 EC 偏移或新版 BIOS，再评估。
6. **触控板的两层状态已经拆开显示，但没实测。** 界面开关现在只表示并控制设备层（pnputil）；键盘快捷键那一层（EC 事件，启动后没收到过事件时未知）单独用一个"快捷键已锁定"标签提示。原来两层合在一个开关上、取消禁用后会弹回去的问题，在逻辑上不再出现，但没有在真实窗口里验证过。
7. **游戏盒子在这台机器上到底怎么实现触控板 / Fn 锁 / Win 键锁的切换，没有查清**。前端有 `set_a7_touchpad_status`、`set_a7_fn_key_status`、`set_a7_windows_key_status`、`set_a7_down_key_mode`。固件接口的 fn12（驱动里标为触控板锁）在本机返回不支持。`rev/rtti.txt` 里能看到 `EnableTouchpad` / `DisableFnKey` / `GetDownKeyMode` 等类名，但它们挂在 `inventec_sync_operation` 和 `syncoperation` 两个命名空间下，二进制里没有 Bitland 字样，也没有 Inventec / Quanta 的 WMI 类名——没有证据表明本机走这些类。
8. **GPU 模式切换是否生效**。固件会接受 fn9 的写入并返回成功，但读不回模式，要重启才知道结果，没试过。从游戏盒子前端代码看，这个功能只对 A7SR 机型开放。界面的提示语已经改成如实表述。
9. **fn10、fn11 的含义**未知，读数恒为 0 和 1。

### 可选的改进

10. **去掉 LibreHardwareMonitor 依赖**。它现在只提供 GPU 负载/功耗（来自 NVML）和 CPU 功耗。前两项可以直接调显卡驱动自带的 `nvml.dll`，能省掉 12 个 DLL；代价是 CPU 功耗仍需 PawnIO。进程内存目前 70–100 MB，这部分值得评估。
11. 再次运行 `MiCtl.exe` 时，如果已有实例在运行，会弹"已在运行"而不是打开窗口。
12. Fn 锁在启动后显示"--"，要等第一次 Fn+Esc 事件才有值。
13. 根目录 60 多个过程脚本没有整理，`probe/` 里也有多次试错的产物。
14. `AutoProfile` 的"迟滞 3°C"不是真正的迟滞：它只是把静谧阈值整体下移了 3°C，上升和下降用同一条边界，靠 15 秒防抖兜底。
15. 手动切档后的 15 秒防抖已修正（原来把计时设到 15 秒前，等于立即放行自动切档），修正后没有在真实窗口里验证过。
16. **"结束"进程按钮没有实测**：确认框、关键进程保护（`SystemInfo.IsCritical`）、部分进程结束失败时的提示，都只过了代码。实测时先拿一个自己打开的、无关紧要的程序试。
17. CPU 频率（`Cores (Average)`）已提权实测：新版程序里读到 2949~3162 MHz 随负载波动（2026-11-26 `probe/appcheck.txt`）。
18. 网络页的"重新检测"按钮、电池充放电功率的显示（本机满电时功率为 0，没见过非零值）没有实测。WiFi 解析只在本机（中文系统、Intel AX210、已连接）验证过，有线、断开、英文系统都没试。
19. 进程榜每 4 秒枚举一次全部进程、WiFi 每 16 秒起一次 netsh，都放在后台线程；对轮询周期和内存占用的影响没有量过。

## 6. 踩过的坑

**固件接口**

1. `System.Management.ManagementObject.InvokeMethod` 和 `Invoke-CimMethod` 调 `MiInterface` 都会失败（"无效的方法参数" / "拒绝访问" / 返回空）。可行的做法是：自己声明 COM 接口（方法顺序照抄 System.Management 内部声明，`IWbemServices` 是 23 个方法、`IWbemClassObject` 是 24 个），用反射从 `ManagementScope` 取出私有字段 `wbemServices`，然后在**实例路径**上 `ExecMethod`。`OutData` 和 `Reserved` 都是输出参数，不能传进去。需要管理员权限。实现见 `MifsClient.cs`。

**工具链**

2. `csc.exe`（v4.0.30319）只支持 C# 5 语法：`out _`、`$""`、`?.`、表达式体成员都用不了。PowerShell 5.1 的 `Add-Type` 同样。
3. 这台机器的命令行是 cmd.exe。带 `$` 的 PowerShell 不要内联，写成 `.ps1` 用 `pwsh -File` 跑。
4. 脚本用 pwsh 7 运行。Windows PowerShell 5.1 读不带 BOM 的 UTF-8 脚本时，会把中文注释读坏，并连累紧跟其后的那行代码。
5. **MiCtl 的界面读不到**：它是管理员进程，Windows UIPI 不允许非提权进程对它做 UI Automation，截图工具也一样。不要绕过，改用第 7 节的检查程序验证数据层，界面请用户目视。
6. 编译前要先退出正在运行的 MiCtl，否则 `out\MiCtl.exe` 被占用。

**传感器库与驱动**

7. WinRing0 在这台机器上被系统拦截（`StartService` 返回 `0x5AA`），所以 LibreHardwareMonitor 0.9.4 及更早的版本读不到 CPU 数据。
8. PawnIO **2.2.0 的安装包有缺陷**：只部署 `.cat` 和 `.inf`，没有 `PawnIO.sys`。用 2.1.0。安装包不在仓库里，从 [namazso/PawnIO.Setup](https://github.com/namazso/PawnIO.Setup/releases) 下载。
9. 安装 PawnIO 前不要 `sc delete`：旧服务处于"标记删除"状态时，新装的 `AddService` 会失败（错误 `0x430`），设备装不上。
10. **`sc query` 显示 RUNNING 不能说明安装成功**。11 月 19 日那次安装实际上是失败的。安装脚本跑了两次：第一次很可能装好了，第二次先执行了 `sc delete`，于是新装的服务创建失败、设备被删掉；但已加载的内核驱动还留在内存里，所以当时能读到温度，重启后就没了。判断安装是否成功，要看 `pnputil /enum-devices` 里有 `ROOT\PAWNIO` 且状态为已启动，`C:\Windows\INF\setupapi.dev.log` 的新段落里没有 `!!!` 行，并且**重启电脑后再测一次**（2026-11-25 这次三项都做了，复测通过）。
11. LibreHardwareMonitorLib 的 `Computer.Open()` 成功不代表读得到数据。驱动不可用时它返回 `0` 而不是 `null`，`SensorService` 已把这些 `0` 当作读不到处理。
12. NVIDIA GPU 有 `GPU Core` 和 `GPU Hot Spot` 两个温度传感器，空闲时后者高 3–4°C。任务管理器和 nvidia-smi 对应的是 `GPU Core`。
13. 开启 `IsMemoryEnabled` 会要求 `RAMSPDToolkit-NDD.dll`。

**Git 与命令行**

14. 推送后用 `git ls-remote origin main` 核对提交哈希。有一条命令把 `git push` 接在管道后面，输出被吞掉，推送没执行而当时没有察觉。
15. 在 cmd 里给 `gh` 传中文会乱码，改用 `--input` 传 JSON 文件。`gh` 输出的中文在控制台里显示成乱码只是显示问题，远端内容是好的。

**其它**

16. 每个提权操作都会弹 UAC，需要用户点确认。用户不在电脑前时，这类命令会一直挂到超时。
17. 被占用的日志文件（例如游戏盒子的 `debug.log`）要用 `FileShare.ReadWrite` 打开才能读。那份日志只是浏览器内核的控制台输出，没有硬件信息。

**界面**

18. `app.manifest` 不声明 DPI 感知时，Windows 会把整个窗口当位图拉伸到屏幕缩放比例，文字和线条全部发虚（这台机器是 150%）。声明之后系统不再代劳，所有坐标和字号都要自己按 `Theme.Scale` 换算。
19. 字体统一用 `GraphicsUnit.Pixel` 并经 `TextRenderer` 绘制：像素大小与 DPI、与离屏位图都无关，屏幕渲染和预览图才能逐像素一致。`Point` 单位的字体会随 DPI 变化，不要用。
20. `Control.DrawToBitmap` 在窗口不可见时不会画任何子控件（只有背景）。预览工具要把窗口 `Show()` 到屏幕外再渲染。预览进程也要带 `dpiAware` manifest，否则窗口会被虚拟屏幕高度裁掉（第一次渲染只得到 980 像素高）。
21. 1px 的线经 `Theme.S(1)` 在 150% 下是 2 像素。分隔线要细就固定 1 物理像素，不要过 `S()`。
22. 运行中的 MiCtl 是提权进程：普通权限 `Stop-Process` 无效，而且 `out\\MiCtl.exe` 被锁定不能覆盖。部署用 `MiCtl/deploy.ps1`：用户先在托盘退出旧实例，脚本在普通权限下编译，只对启动 exe 这一步提权（`restart.ps1` 的"提权脚本结束进程"方式被火绒拦，见下）。

**火绒（本机安全软件，2026-11-26 实测）**

23. 新出现的 exe（新文件名，或复制出的副本）会被文件实时监控判为 **Backdoor/Steam 后门病毒并自动隔离**，连运行都没运行就被扣（复制到 %TEMP% 的测试副本当场消失）。同路径同名覆盖已存在的 exe 不会被扣——所以调试产物一律写到已信任的具体文件路径（如 `probe096\\MiCtl.exe`）覆盖写。
24. "编译后再改 exe"（UpdateResource 加版本信息）不可行：对信任区内的 exe 操作返回拒绝访问；新副本又被隔离。版本信息、图标、manifest 全部改为**编译期**用 `/win32res` 嵌入：`MiCtl/tools/make_res.py` 生成 .res（需要 python；不可用时退回纯 manifest）。
25. 手写 .res 的两个格式坑（csc 报 CS1583 "不是有效的 Win32 资源文件"）：ordinal（数字）类型必须写成 0xFFFF 标记 + WORD 数字（即 DWORD = 数字<<16 | 0xFFFF），写裸 DWORD 会被当字符串读；文件开头必须有一条 32 字节的空记录（type=0、name=0、DataSize=0），这是 rc.exe 的固定开场。字段约定见微软 RESOURCEHEADER 文档。
26. 用户已把 `MiCtl/out/MiCtl.exe` 和 `probe096/MiCtl.exe` 两个文件路径加入火绒信任区；部署链路（同名覆盖 + 提权启动）实测无拦截。更稳妥的办法是加**文件夹及子目录**条目（如整个仓库目录），否则新文件名仍会被扣。治本是提交误报申诉（材料已备好：`docs/火绒误报申诉.md`，报毒名 Backdoor/Steam，病毒 ID A560FE0CA8008175，渠道为火绒论坛"样本误报"版块或 seclab@huorong.cn）。
23. 本机有一块 TUN 代理虚拟网卡（`vgate0`，类型 53，描述含 Wintun，IP 172.30.236.116，没有网关）。`NetworkInterface` 把它当普通网卡，原来的网速把它和 WLAN 各算一遍，读数虚高；游戏盒子的"网卡类型"显示的也是它。现在只统计"已连接、非虚拟、有 IPv4 网关"的网卡，它归到"其它地址"。
24. 非管理员下 `MifsClient.Available` 是 True，但 fn8 / fn9 返回空（`GetFanTempStatus`、`GetProfile` 都是 null）。普通权限跑检查程序看不到温度、档位、风扇，不代表程序坏了。
25. 传感器库的 CPU 时钟 `Cores (Average)` 在非管理员下没有值（`probe/appcheck.txt` 里是 `--`）；提权诊断 `probe/sysprobe.txt` 里是 3.3~3.4 GHz。
26. `netsh wlan show interfaces` 的输出是系统代码页（936），标签随系统语言本地化（中文系统是"状态 / 信号 / 无线电类型 / 接收速率(Mbps)"）。解析按中英文都写了，输出按 936 解码。

## 7. 构建、运行、验证

```powershell
# 构建（依赖缺失时自动下载；已用干净克隆验证过）。运行中的 MiCtl 会锁住 out\MiCtl.exe，先退出它
pwsh -File MiCtl/build.ps1

# 部署新版：等旧实例从托盘退出 → 编译 → 以管理员启动（弹一次 UAC）
pwsh -File MiCtl/deploy.ps1

# 端到端检查：把 MiCtl.exe 编译到 probe096\，再编译 AppCheck 引用它
pwsh -File build-run-appcheck.ps1            # 只编译（普通权限）
pwsh -File build-run-appcheck.ps1 -Run      # 编译后以管理员运行（弹 UAC），打印 probe/appcheck.txt
# 普通权限也能跑，读不到 CPU 温度 / 功耗 / 频率和固件数据，但能验证其余全部数据

# 界面离屏预览（不连硬件，不需要 UAC）
pwsh -File MiCtl/preview.ps1 -Scale 1.5 -Mode sample -Page all      # 控制 / 系统 / 网络三页
pwsh -File MiCtl/preview.ps1 -Scale 1.5 -Mode empty  -Page all      # 什么都读不到的状态

# 系统数据诊断（全部传感器 + 内存 + 性能计数器 + 网卡 + netsh + 进程 + 电池），需要管理员
probe\SysProbe.exe probe\sysprobe.txt

# 推送后验证：从 GitHub 干净克隆到 %TEMP% 并构建（应能一次编译通过；
# 注意：新克隆目录里编译出的 exe 会被未加白的火绒当场隔离，属于预期行为）
pwsh -File clone-build-test.ps1
```

`AppCheck` 不会经过界面控件，只验证数据链路。`preview.ps1` 只验证静态外观。两者都替代不了对真实窗口的目视。

`build-run-appcheck.ps1` 不会经过界面控件，只验证数据链路。界面表现需要用户目视。

单看固件数据：`pwsh -File build-run-fantemp.ps1` 会在 CPU 满载 12 秒的过程中逐秒对比固件数据、传感器库和 nvidia-smi。

配置文件在 `%LOCALAPPDATA%\MiCtl\config.json`。

## 8. 用户的偏好和反馈

- 中文交流，表达直接。**很在意"没验证就说做好了"**，要的是实测证据。"确定完全逆向了吗"这句追问，就是针对之前"100% 打通""完整逆向"之类的说法。
- 汇报时先说结论，再说没做到的部分。
- 不喜欢为测试反复配合按键或守着计时窗口。需要他配合时，提前说清要做什么、持续多久。
- 界面偏好现代深色风格。固件不支持的功能宁可删掉，不要留摆设。
- 反馈过的具体事实：
  - 档位只有三个，原来的四个里有一个重复。
  - 键盘背光由键盘快捷键控制，亮度只有两档，不需要软件控制。
  - Fn 锁由 Fn+Esc 控制，只对笔记本自带键盘生效；锁定 = 正常 F1，解锁 = F1–F12 出音量/亮度（2026-11-26 确认）。
  - 当前 GPU 模式是独显直连（2026-11-26 确认）。
  - 满载风扇是 4000 多转；天气冷时空载很难让风扇满载。
- 用户选择把仓库设为公开。

## 9. 已被推翻的旧结论

下面的说法在早期出现过，都是错的，不要从旧输出或旧提交里捡回来：

| 旧说法 | 实际情况 |
|---|---|
| fn9 是 GPU 模式 | fn9 读回来的是风扇转速和温度。GPU 模式能否通过 fn9 写入，未验证 |
| 风扇转速由 EC 事件（id 26 / 32）推送 | 一份 68 个事件的日志里没有任何转速事件，转速靠轮询 fn9 |
| 这台机器是英业达代工、走 Inventec 的 WMI 接口 | 没有依据。游戏盒子在本机用的是 `MICommonInterface` |
| "MIFS 协议 100% 打通""完整逆向" | 夸大。未查清的部分见第 5 节 |
| 固件不接受软件关闭 Fn 锁 | 依据不足，见第 5 节第 6 条 |
| 2026-11-19 PawnIO 2.1.0 安装成功 | 当时失败了，见第 6 节第 10 条 |
| CPU 温度只能靠 PawnIO 读 | 固件接口 fn9 就有 CPU 温度，PawnIO 只影响 CPU 功耗 |

`probe/mifs-enum.txt` 开头那行"已知: … 9=GPU模式 13=风扇转速…"是枚举当时按 Linux 驱动写的标注，同样已过时。
