# 在 wmi-classes.txt 中匹配候选 GUID，dump 对应类的完整定义
$root = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5'
$guids = @('6E5C5DE2-0AC5-446A-A473-2F736CD06B18', '799F1D61-045A-4A3C-895A-03152C28A2F2', 'DF541A2B-1DAD-4930-B42B-79719761F838')

foreach ($g in $guids) {
    Write-Output ('=== ' + $g + ' ===')
    $hit = Select-String -Path ($root + '\wmi-classes.txt') -Pattern $g -SimpleMatch
    if ($hit) { $hit | ForEach-Object { Write-Output ('  ' + $_.Line) } else { Write-Output '  (root\WMI 中无此类)' }
    }
}

# 对存在的类 dump 完整定义
foreach ($g in $guids) {
    $hit = Select-String -Path ($root + '\wmi-classes.txt') -Pattern $g -SimpleMatch
    if ($hit) {
        $clsName = ($hit.Line -split '\s+')[0]
        Write-Output ''
        Write-Output ('=== 类定义: ' + $clsName + ' ===')
        $c = Get-CimClass -Namespace root\WMI -ClassName $clsName
        Write-Output ($c.ToString() + '  GUID=' + $c.CimClassQualifiers['guid'].Value)
        foreach ($p in $c.CimClassProperties) {
            Write-Output ('  prop: ' + $p.Name + ' : ' + $p.CimType + '  ID=' + $p.Qualifiers['ID'].Value)
        }
        foreach ($m in $c.CimClassMethods) {
            Write-Output ('  method: ' + $m.Name)
            foreach ($p in $m.Parameters) {
                Write-Output ('    param: ' + $p.Name + ' : ' + $p.CimType + '  ID=' + $p.Qualifiers['ID'].Value)
            }
        }
    }
}
