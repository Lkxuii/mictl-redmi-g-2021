# 直取 TMA 相关类（枚举可能漏动态类，但 GetObject 可直取）
$names = @('TMA_WMIEvent', 'RW_TMAWMI', 'TMAWMI', 'TMA_WMIData', 'TMAWMIData')
foreach ($n in $names) {
    Write-Output ('=== ' + $n + ' (root\WMI) ===')
    try {
        $c = Get-CimClass -Namespace root\WMI -ClassName $n -ErrorAction Stop
        Write-Output ('  存在! GUID=' + $c.CimClassQualifiers['guid'].Value)
        foreach ($p in $c.CimClassProperties) {
            $qid = $p.Qualifiers['ID']
            Write-Output ('  prop: ' + $p.Name + ' : ' + $p.CimType + '  ID=' + ($qid ? $qid.Value : '-'))
        }
        foreach ($m in $c.CimClassMethods) {
            Write-Output ('  method: ' + $m.Name + '  ID=' + $m.Qualifiers['WmiMethodId'].Value)
            foreach ($p in $m.Parameters) {
                $qid = $p.Qualifiers['ID']
                Write-Output ('    param: ' + $p.Name + ' : ' + $p.CimType + '  ID=' + ($qid ? $qid.Value : '-'))
            }
        }
    } catch {
        Write-Output ('  不存在: ' + $_.Exception.Message)
    }
}

# 枚举时带 deep 查询 WMIEvent 的全部子类（可能有遗漏）
Write-Output ''
Write-Output '=== root\WMI 中名字含 TMA/HID 的类 ==='
Get-CimClass -Namespace root\WMI | Where-Object { $_.CimClassName -match 'TMA|HID' } | ForEach-Object {
    Write-Output ('  ' + $_.CimClassName + '  guid=' + $_.CimClassQualifiers['guid'].Value)
}
