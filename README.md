# MiCtl — Redmi G 2021 轻量控制台

为 **Redmi G 2021 锐龙版**（TIMI A7S，R7 5800H + RTX 3060）写的托盘控制台，对标小米游戏盒子的硬件控制功能，目标是更轻的常驻工具。

硬件接口来自对小米游戏盒子（`GamingBoxHost.exe` 与其前端 `dist/main.js`）和 Linux `bitland-mifs-wmi` 驱动的分析，并在本机逐项实测——包括一次 256 个功能码的全枚举。

**哪些功能实测过、哪些没有**，见 [MiCtl/README.md](MiCtl/README.md) 的验证状态表；接手开发请先读 [HANDOFF.md](HANDOFF.md)。

## 仓库内容

| 目录 | 内容 |
|---|---|
| [`MiCtl/`](MiCtl/) | 控制台本体（C# WinForms，`csc.exe` 直接编译）。协议细节见 [MiCtl/README.md](MiCtl/README.md)；`preview.ps1` 可把界面离屏渲染成 PNG，`deploy.ps1` 编译并以管理员启动 |
| [`rev/`](rev/) | GamingBoxHost.exe 的全量字符串表、RTTI 类名清单 |
| [`probe/`](probe/) | 逆向与验证用的诊断工具源码，以及各阶段的实测输出 |
| `*.ps1`（根目录） | 逆向过程脚本：WMI 类枚举、二进制字符串提取、PawnIO 安装验证等。多数是一次性的，内含本机绝对路径 |
| `bitland-mifs-wmi.c` | Linux 内核驱动参考副本（第三方，GPL-2.0） |

## 核心发现（本机实测）

**固件接口 MIFS**（WMI 类 `MICommonInterface`，须在实例路径 `...InstanceName='ACPI\PNP0C14\MIFS_0'` 上调用）：

- 256 个功能码全枚举，本机固件只响应 4 个：fn8（性能档位）、fn9（风扇转速 + CPU/GPU 温度）、fn10、fn11（后两个含义未知）。
- **fn9 与 Linux 驱动的标注不符**：驱动称之为 GPU_MODE，本机读取返回的实际是两路风扇转速加 CPU、GPU 温度——游戏盒子"监控器"页每 2 秒轮询的就是它。
- 档位真实映射：`2` = 静谧、`1` = 均衡、`0` 与 `3` 都是极速（只有三档）。

**EC 事件**（WMI 事件类 `HID_EVENT20`）：推送触控板、Fn 锁的状态变化；不推送风扇转速。

## 编译

```powershell
pwsh -File MiCtl/build.ps1
```

依赖缺失时会自动下载 LibreHardwareMonitor 0.9.6。已用干净克隆验证过可以一次编译通过。

## 安全软件误报说明

用 .NET 自带 csc.exe 自行编译出的 `MiCtl.exe` 没有数字签名，可能被启发式引擎误报（开发机上的火绒将其判为 Backdoor/Steam，编译输出落地即被隔离）。程序需要管理员权限是为了调用笔记本固件的 WMI 接口；包含低级键盘钩子是「禁用 Win 键」功能所需。处理方式：把编译输出目录加入安全软件信任区，或提交误报申诉（模板见 [docs/火绒误报申诉.md](docs/火绒误报申诉.md)）。

## 界面

深色自绘界面。顶部是一块常驻的仪表：CPU、GPU、内存三个环，下面 CPU、GPU 各一行数据（负载、频率、功耗、风扇）。再往下用页签分成三页：**控制**（性能档位、温度自动联动、触控板 / Win 键 / Fn 锁、GPU 模式、开机自启）、**系统**（内存与电池概况、内存占用前 5 的进程及其结束按钮、打开任务管理器）、**网络**（上下行速率、网卡、接入点、本地 IP、网关与公网延迟）。

监控部分的对照对象是小米游戏盒子的"监控器"页，各项数据来源和验证情况见 [MiCtl/README.md](MiCtl/README.md)。进程声明为系统 DPI 感知，在高缩放屏幕上由程序自己按物理像素绘制，不依赖系统拉伸。

MiCtl 以管理员运行，截图工具读不到它的窗口；开发时用下面的命令把主窗口离屏渲染成 PNG 来检查（不连硬件，`-Mode empty` 渲染读不到数据的状态）：

```powershell
pwsh -File MiCtl/preview.ps1 -Scale 1.5 -Mode sample -Page all
```

界面结构、设计约定与各个控件的说明见 [MiCtl/README.md](MiCtl/README.md)。

## 运行

以管理员权限运行 `MiCtl/out/MiCtl.exe`。温度、转速、档位、GPU 负载和功耗不需要内核驱动；CPU 功耗读数另需安装 PawnIO 2.1.0，详见 [MiCtl/README.md](MiCtl/README.md)。

## 许可

仓库作者自写代码的许可尚未指定。`bitland-mifs-wmi.c` 为他人所著，保留其 GPL-2.0 头部声明。
