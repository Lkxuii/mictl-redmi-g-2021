# 切换到 LHM 0.9.4（内嵌 WinRing0）并复制依赖
$root = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5'
$src = $root + '\lhm094-extract'

# libs 目录
Copy-Item ($src + '\LibreHardwareMonitorLib.dll') ($root + '\MiCtl\libs\') -Force
Copy-Item ($src + '\HidSharp.dll') ($root + '\MiCtl\libs\') -Force
Copy-Item ($src + '\Newtonsoft.Json.dll') ($root + '\MiCtl\libs\') -Force
Copy-Item ($src + '\System.CodeDom.dll') ($root + '\MiCtl\libs\') -Force

# probe 目录（诊断程序运行时需要）
Copy-Item ($src + '\LibreHardwareMonitorLib.dll') ($root + '\probe\') -Force
Copy-Item ($src + '\HidSharp.dll') ($root + '\probe\') -Force
Copy-Item ($src + '\Newtonsoft.Json.dll') ($root + '\probe\') -Force
Copy-Item ($src + '\System.CodeDom.dll') ($root + '\probe\') -Force

Write-Output '已替换为 LHM 0.9.4'

# 检查 0.9.4 的 dll 里是否内嵌 WinRing0
$bytes = [IO.File]::ReadAllBytes($src + '\LibreHardwareMonitorLib.dll')
$ascii = [Text.Encoding]::ASCII.GetString($bytes)
Write-Output ('内嵌 WinRing0 资源: ' + $ascii.Contains('WinRing0'))
