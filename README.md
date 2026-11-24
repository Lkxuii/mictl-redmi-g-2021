# MiCtl — Redmi G 2021 轻量控制台

为 **Redmi G 2021 锐龙版**（TIMI A7S，R7 5800H + RTX 3060）写的托盘控制台，目标是一个比小米游戏盒子轻、功能不缺的常驻工具。

硬件接口来自对小米游戏盒子（`GamingBoxHost.exe` 与其前端 `dist/main.js`）和 Linux `bitland-mifs-wmi` 驱动的分析，**并在本机逐项实测**——包括一次 256 个功能码的全枚举。

## 仓库内容

| 目录 | 内容 |
|---|---|
| [`MiCtl/`](MiCtl/) | 控制台本体（C# WinForms，csc.exe 直接编译）。协议细节见 [MiCtl/README.md](MiCtl/README.md) |
| [`rev/`](rev/) | GamingBoxHost.exe 的全量字符串表、RTTI 类名清单 |
| [`probe/`](probe/) | 逆向与验证用的诊断工具源码（20+ 个 C# 小工具）及各自的输出结果 |
| `*.ps1`（根目录） | 逆向过程脚本：WMI 类枚举、二进制字符串提取、安装验证等 |

## 核心发现（本机实测）

**固件接口 MIFS**（WMI 类 `MICommonInterface`，须在实例路径 `...InstanceName='ACPI\PNP0C14\MIFS_0'` 上调用）：

- 256 个功能码全枚举，本机固件只响应 4 个：fn8（性能档位）、fn9（风扇转速 + CPU/GPU 温度状态包）、fn10、fn11。
- **fn9 与 Linux 驱动的标注不符**：驱动称之为 GPU_MODE，本机 GET 返回的实际是 `{风扇A RPM: u16le, 风扇B RPM: u16le, ..., CPU温度, ..., GPU温度}`——这正是游戏盒子"监控器"页每 2 秒轮询的数据源。
- 档位真实映射：`2`=静谧、`1`=均衡、`0` 与 `3` 都=极速（三档，两值等效）。

**EC 事件**（WMI 事件类 `HID_EVENT20`）：触控板/Fn 锁状态变化实时推送；**风扇转速不在事件里**，只能轮询 fn9——这是实测 68 个事件得出的结论，也解释了为什么游戏盒子要轮询。

完整协议表、验证状态清单（哪些实测过、哪些没验证）见 [MiCtl/README.md](MiCtl/README.md)。

## 编译

```powershell
pwsh -File MiCtl/build.ps1
```

缺依赖时会自动下载 LibreHardwareMonitor 0.9.6（GitHub Releases）到 `MiCtl/libs/`。

## 许可

本仓库自写代码按 MIT 发布；`bitland-mifs-wmi.c` 为他人所著的 Linux 内核驱动参考副本，保留其 GPL-2.0 头部声明。
