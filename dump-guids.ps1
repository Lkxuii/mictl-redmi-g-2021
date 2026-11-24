# 枚举 root\WMI 中带 GUID 限定符的类（找 46C93E13 事件类）
$out = New-Object System.Collections.Generic.List[string]
$all = Get-CimClass -Namespace root\WMI
foreach ($c in $all) {
    $g = $c.CimClassQualifiers['guid']
    if ($g) {
        $out.Add($c.CimClassName + '  guid=' + $g.Value + '  super=' + $c.CimSuperClassName)
    }
}
$out | Out-File -FilePath 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\wmi-classes.txt' -Encoding utf8
Write-Output ('带 GUID 的类共 ' + $out.Count + ' 个，已写入 wmi-classes.txt')
