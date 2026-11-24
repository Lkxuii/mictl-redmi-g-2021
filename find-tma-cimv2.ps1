# 在 root\cimv2 查 TMA 类
foreach ($n in @('RW_TMAWMI', 'TMA_WMIEvent')) {
    Write-Output ('=== ' + $n + ' (root\cimv2) ===')
    try {
        $c = Get-CimClass -Namespace root\cimv2 -ClassName $n -ErrorAction Stop
        Write-Output ('  存在! GUID=' + $c.CimClassQualifiers['guid'].Value)
        Write-Output ('  super=' + $c.CimSuperClassName)
        foreach ($p in $c.CimClassProperties) {
            $qid = $p.Qualifiers['ID']
            Write-Output ('  prop: ' + $p.Name + ' : ' + $p.CimType + '  ID=' + ($qid ? $qid.Value : '-') + '  qual=[' + (($p.Qualifiers | ForEach-Object { $_.Name }) -join ',') + ']')
        }
        foreach ($m in $c.CimClassMethods) {
            Write-Output ('  method: ' + $m.Name + '  ID=' + $m.Qualifiers['WmiMethodId'].Value)
            foreach ($p in $m.Parameters) {
                $qid = $p.Qualifiers['ID']
                $pmid = $p.Qualifiers['WmiMethodId']
                Write-Output ('    param: ' + $p.Name + ' : ' + $p.CimType + '  ID=' + ($qid ? $qid.Value : '-') + ($pmid ? ' WmiMethodId=' + $pmid.Value : ''))
            }
        }
    } catch {
        Write-Output ('  不存在: ' + $_.Exception.Message)
    }
}
