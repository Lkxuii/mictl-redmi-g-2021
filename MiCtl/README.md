# MiCtl — Redmi G 2021 轻量控制台

为 **Redmi G 2021 锐龙版**（TIMI A7S，R7 5800H + RTX 3060）写的托盘控制台。
硬件接口来自对小米游戏盒子（`GamingBoxHost.exe` 与前端 `dist/main.js`）和 Linux `bitland-mifs-wmi` 驱动的分析，并在本机逐项实测。

## 功能与验证状态

| 功能 | 实现方式 | 验证情况 |
|---|---|---|
| 性能档位（静谧 / 均衡 / 极速） | 固件接口 fn8 | 实测：写入后回读一致 |
| 风扇转速 | 固件接口 fn9 轮询（每 2 秒） | 实测：与游戏盒子同源数据 |
| CPU / GPU 温度 | 固件接口 fn9 轮询 | 实测：CPU 值随满载变化，GPU 值与 nvidia-smi 逐秒一致；CPU 值与 PawnIO/SMN 独立读数一致（±0.5°C） |
| GPU 负载 / 功耗 | NVML（经 LibreHardwareMonitorLib） | 实测有数值 |
| GPU 核心频率 / 显存 | NVML（`GPU Core` 时钟、`GPU Memory Used/Total`） | 实测：1567 MHz、2066 / 6144 MB；频率与游戏盒子监控页显示的数值一致 |
| CPU 负载 | 传感器库 `CPU Total`（不需要内核驱动） | 实测：随负载变化（9.6%~16.3%） |
| CPU 频率 | 传感器库 `Cores (Average)`（需要内核驱动） | 提权诊断（13:48）中有值 3.3~3.4 GHz；**新版程序接入后未在提权下实测** |
| CPU 功耗 | LibreHardwareMonitorLib + PawnIO | 实测有数值（14~25W），重启电脑后复测仍可读。需安装 PawnIO |
| 内存使用率 / 已用 / 可用 / 频率 | `GlobalMemoryStatusEx` + WMI `Win32_PhysicalMemory` | 实测：37%、12 / 31.8 GB、3200MHz |
| 电池电量 / 充放电功率 / 健康度 | 传感器库 Battery 硬件 | 实测：100%、健康度 81.7% |
| 网速 | 真正上网的那块网卡的计数差分（有 IPv4 网关、非虚拟） | 实测有数值；选中 WLAN 而非 TUN 虚拟网卡 |
| 网卡 / 接入点 / 本地 IP / 网关延迟 / 公网延迟 | `NetworkInterface`、`netsh wlan show interfaces`、ping | 实测：WLAN、SSID Nkxuii、信号 90%、192.168.1.146、网关 2 ms、公网 35 ms |
| 进程数 / 内存占用前 5 / 结束进程 / 打开任务管理器 | `Process.GetProcesses()`（同名合并，排除系统关键进程） | 榜单实测有值；**结束进程未实测** |
| 档位跟随（游戏盒子 / 键盘切换后同步） | 轮询 fn8 | 数据层实测，界面未目视确认 |
| 触控板 / Fn 锁状态跟随键盘快捷键 | EC 事件 id 6 / 7 | 事件实测能收到；语义已确认（锁定 = 功能键，解锁 = F1–F12 出音量/亮度）；事件 value 与锁定/解锁的对应推断为 0 = 解锁、1 = 锁定，**待一次显式对照** |
| 禁用触控板（界面开关） | pnputil 禁用设备 | **未实测** |
| 禁用 Win 键 | 低级键盘钩子 | **未实测** |
| 温度自动联动 | 双阈值 + 迟滞 + 15 秒防抖 | **未实测** |
| GPU 模式识别 | 集显是否可见 | 独显直连方向已验证（当前机器确为独显直连，识别一致）；混合模式方向未验证 |
| GPU 模式切换 | 固件接口 fn9 SET | **未实测**：固件接受指令，是否真的切换未知 |
| 开机自启 | 计划任务 | **未实测** |
| 界面（自绘控件，系统 DPI 感知） | `Theme.cs` / `Controls.cs` / `UiState.cs` | 离屏渲染的 150% 与 100% 缩放画面已目视检查；**真实窗口待目视** |

界面可以在屏幕外离屏渲染成 PNG 来检查（`pwsh -File preview.ps1`，与屏幕渲染逐像素一致）。MiCtl 以管理员权限运行，非提权进程的 UI Automation 和截图都会被 Windows 拦截，所以真实窗口的显示效果只能请用户目视。

## 硬件接口（本机实测）

### 固件接口 MIFS（`root\WMI` 的 `MICommonInterface`，方法 `MiInterface`）

- 必须在**实例路径** `MICommonInterface.InstanceName='ACPI\PNP0C14\MIFS_0'` 上调用；对类直接调用会报"无效的方法参数"。
- 请求 32 字节：`[1]` = 操作码（250 读 / 251 写），`[3]` = 功能码，`[4..31]` = 参数。
- 响应 30 字节：`[1]` = 状态（`0x80` 成功，`0xE0` 不支持），`[3]` = 功能码回显，`[4..]` = 数据。
- 256 个功能码全部读了一遍，本机固件只响应 4 个：
  - **fn8** 性能档位。固件真实映射是 `2` = 静谧、`1` = 均衡、`0` 和 `3` 都是极速（只有三档，`0` 与 `3` 等效）。
  - **fn9** 读：风扇与温度状态包（其余字节恒为 0）
    - `data[0..1]` 小端 u16 = 风扇转速 A（RPM）
    - `data[2..3]` 小端 u16 = 风扇转速 B（RPM）
    - `data[6]` = CPU 温度（°C）
    - `data[10]` = GPU 温度（°C）
  - **fn10**、**fn11**：含义未知，读到的值恒为 0 和 1。
- Linux 驱动把 fn9 标为 `GPU_MODE`、fn13 标为 `FAN_SPEEDS`，这与本机不符：fn9 读回来的是上面的状态包，fn13 不支持。
- 游戏盒子"监控器"页面的转速和温度就是每 2 秒轮询一次 fn9 得到的。

### EC 事件（`root\WMI` 的 `HID_EVENT20`）

- 事件数据 8 字节：`type, id, value_low, value_high, 0, 0, 0, 0`。
- 一份 68 个事件的日志里只出现过三种：`id 6` 触控板状态、`id 7` Fn 模式、`id 10`。
- **没有收到过风扇转速事件**，所以转速只能靠 fn9 轮询。
- 实测语义：触控板 `value 0` = 不能用。Fn 锁语义（用户确认）：锁定 = F1–F12 是正常功能键，解锁 = F1–F12 出音量/亮度；事件 value 与锁定/解锁的对应推断为 0 = 解锁、1 = 锁定，待一次显式对照。

## 没搞清楚或没验证的

- **两路风扇哪路是 CPU、哪路是 GPU**：按字段排布推断（`data[0..1]` 当 CPU），轻载下两路数值接近，没法实测区分。
- **Fn 锁事件 value 与锁定/解锁的对应**：语义已确认（锁定 = 功能键，解锁 = 媒体键），value 0 = 解锁是推断，按一次 Fn+Esc 对照即可钉死。
- **GPU 模式切换是否生效**：要重启才能看到结果，没试过。
- **键盘快捷键禁用触控板在硬件层做了什么**：设备管理器里的设备状态没有变化。
- **fn10 / fn11 的含义**。

## 运行要求

- 需要管理员权限（固件接口的写操作）。
- 温度、转速、档位、GPU 负载/功耗都**不需要内核驱动**。
- 只有 CPU 功耗需要 [PawnIO](https://github.com/namazso/PawnIO.Setup/releases) 2.1.0。没有它其余功能不受影响，只是界面不显示 CPU 功耗。实测：安装后重启电脑，驱动仍自动加载，功耗读数正常。

### PawnIO 安装注意（踩过的坑）

- **2.2.0 的安装包有缺陷**：只部署 `PawnIO.cat` / `pawnio.inf`，没有 `PawnIO.sys`，装完驱动无法启动。用 **2.1.0**。
- 安装前不要执行 `sc delete PawnIO`：服务处于"标记删除"状态时，新装的 `AddService` 会失败（错误 `0x430`）。
- **`sc query` 显示 RUNNING 不能说明安装成功**：驱动只要加载过就会留在内存里直到重启，即使设备根本没装上。要确认的是 `pnputil /enum-devices` 里有 `ROOT\PAWNIO` 且状态为已启动，`C:\Windows\INF\setupapi.dev.log` 的新段落里没有 `!!!` 行，并且重启电脑后仍能读到数据。
- 仓库根目录的 `install-pawnio-clean.ps1` 是验证过可用的安装脚本。用之前要把 2.1.0 的 `PawnIO_setup_210.exe` 放到脚本同级目录，并改掉脚本里的绝对路径。`install-pawnio210.ps1` 是会导致安装失败的旧脚本，不要用。
- WinRing0 在这台机器上不可用：`StartService` 返回 `0x5AA`（驱动被系统拦截），所以 LibreHardwareMonitor 0.9.4 及更早的内嵌 WinRing0 路径全部无效，必须用 0.9.6 + PawnIO。

## 配置

`%LOCALAPPDATA%\MiCtl\config.json`：`{"auto":true,"low":65,"high":78}`

## 界面结构

主窗口只读一份 `UiState` 快照、只通过 `AppController` 的公开方法执行动作；`AppController` 为 `null` 时就是预览模式。

- `Theme.cs`：配色、DPI 缩放（`Theme.S()`，所有布局常量按 96 DPI 逻辑像素书写）、字体缓存、GDI+ 辅助。
- `Controls.cs`：自绘控件。温度环（`GaugeRing`）、滑动分段（`Segmented`）、页签条（`TabStrip`）、开关、状态标签、步进器、按钮。
- `MainWindow.cs`：布局与绑定。
  - 常驻的仪表卡片：CPU、GPU、内存三个环；下面 CPU、GPU 各一行（负载 · 频率 · 功耗 · 风扇）。
  - 页签分三页，窗口高度随页签变化：**控制**（性能档位、温度自动联动、快捷控制、GPU 模式、开机自启）、**系统**（内存与电池概况、内存占用前 5 的进程、打开任务管理器）、**网络**（上下行速率、网卡、接入点、本地 IP、延迟、其它地址）。
- `SystemInfo.cs`：进程榜（同名合并、排除系统关键进程）、结束进程、WiFi 信息、延迟检测。

设计约定：橙色只表示状态或可以点的动作；档位分段的选中色就是档位色；设置项的选中块用中性灰（选择不等于生效，要点"应用"）；彩色面上的文字用深色；实时数值用等宽字体；分隔线固定 1 物理像素。

对照对象是小米游戏盒子（Mi Gaming Box）的"监控器"页：三环、每个硬件一行的数据、进程榜与网络卡片都对齐了；游戏盒子没有的温度自动联动、触控板 / Win 键 / Fn 锁控制、GPU 模式保留。两处有意的不同：网速只算真正上网的网卡（游戏盒子把 TUN 代理虚拟网卡当成了"网卡类型"）；进程榜排除系统关键进程。

程序在 `app.manifest` 里声明为系统 DPI 感知。没有这条声明时，Windows 会把整个窗口位图拉伸到屏幕缩放比例，文字和线条全部发虚。

## 编译

```powershell
pwsh -File build.ps1
# 产物：out\MiCtl.exe（依赖 libs\ 下的 LibreHardwareMonitorLib 0.9.6 及其配套 DLL）
#   带应用图标、版本信息（1.1.0.0）和 manifest，全部编译期由 tools/make_res.py 生成的
#   .res 嵌入（需要 python；没有 python 时退回纯 manifest，无图标与版本信息）

# 界面离屏预览（不连硬件）：渲染 out\ui-preview-<sample|empty>-<control|system|network>-<scale>x.png
pwsh -File preview.ps1 -Scale 1.5 -Mode sample -Page all

# 部署新版：等你在托盘退出 MiCtl → 编译 → 以管理员身份启动（弹一次 UAC）
pwsh -File deploy.ps1

# 或者（需要管理员权限运行本脚本）：直接结束运行中的实例、编译、启动
pwsh -File restart.ps1
```

运行中的 `out\MiCtl.exe` 被系统锁定，重新编译前必须先结束进程；MiCtl 以管理员运行，普通权限结束不了。`restart.ps1` 用提权脚本结束它；本机的火绒会拦截这种"提权运行脚本 + 结束进程"的组合，所以首选 `deploy.ps1`。
