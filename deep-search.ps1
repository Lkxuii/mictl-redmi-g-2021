# 1) 枚举 root 下所有命名空间并在其中搜 RW_TMAWMI/Touchpad 类
# 2) GamingBoxHost ASCII 字符串里的 WMI 类名/命名空间
# 3) 触摸板注册表状态
$out = New-Object System.Collections.Generic.List[string]

Write-Output '=== 1. root 下命名空间 ==='
$nsList = New-Object System.Collections.Generic.List[string]
$nsList.Add('root')
foreach ($n in (Get-CimInstance -Namespace root -ClassName __Namespace)) {
    $nsList.Add('root\' + $n.Name)
}
foreach ($ns in $nsList) { Write-Output ('  ' + $ns) }

Write-Output ''
Write-Output '=== 2. 各命名空间搜 RW_TMAWMI / Touchpad 类 ==='
foreach ($ns in $nsList) {
    if ($ns -eq 'root') { continue }
    try {
        $cls = Get-CimClass -Namespace $ns -ErrorAction Stop
        foreach ($c in $cls) {
            if ($c.CimClassName -match 'RW_TMAWMI|Touchpad|TouchPad|TMAWMI|Inventec|IWSMI') {
                Write-Output ('  [' + $ns + '] ' + $c.CimClassName + '  guid=' + $c.CimClassQualifiers['guid'].Value)
                foreach ($p in $c.CimClassProperties) { Write-Output ('    prop: ' + $p.Name + ' : ' + $p.CimType) }
                foreach ($m in $c.CimClassMethods) { Write-Output ('    method: ' + $m.Name) }
            }
        }
    } catch { }
}

Write-Output ''
Write-Output '=== 3. GamingBoxHost ASCII 中的 WMI 类名/命名空间 ==='
$b = [IO.File]::ReadAllBytes('C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\GamingBoxHost.exe')
$ascii = [Text.Encoding]::ASCII.GetString($b)
$rx = New-Object System.Text.RegularExpressions.Regex '[\x20-\x7E]{6,}'
foreach ($mm in $rx.Matches($ascii)) {
    $s = $mm.Value
    if ($s -match 'root\\|TMAWMI|Select|SELECT|WQL|Interface|AWM|_WMI') {
        $out.Add($s)
    }
}
foreach ($s in ($out | Select-Object -Unique)) { Write-Output ('  ' + $s) }

Write-Output ''
Write-Output '=== 4. 触摸板注册表状态 ==='
try {
    $tp = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad' -ErrorAction Stop
    $tp.PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' } | ForEach-Object { Write-Output ('  ' + $_.Name + ' = ' + $_.Value) }
} catch { Write-Output ('  PrecisionTouchPad 读取失败: ' + $_.Exception.Message) }
