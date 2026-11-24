$c = Get-CimClass -Namespace root\WMI -ClassName MICommonInterface
$m = $c.CimClassMethods['MiInterface']
Write-Output '=== Method Qualifiers ==='
foreach ($q in $m.Qualifiers) { Write-Output ('[' + $q.Name + '] = ' + $q.Value) }
Write-Output '=== Parameters ==='
foreach ($p in $m.Parameters) {
    Write-Output ('Param [' + $p.Name + '] ' + $p.CimType + ' IsArray=' + $p.IsArray)
    foreach ($q in $p.Qualifiers) { Write-Output ('   [' + $q.Name + '] = ' + $q.Value) }
}
