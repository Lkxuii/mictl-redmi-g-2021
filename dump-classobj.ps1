# dump IWbemClassObject 接口方法序列 + ManagementScope 内部结构
$asm = [Reflection.Assembly]::LoadWithPartialName('System.Management')
foreach ($t in $asm.GetTypes()) {
    if ($t.Name -match 'IWbemClassObject') {
        Write-Output ('=== ' + $t.FullName + '  Guid=' + (($t.GetCustomAttributes($false) | Where-Object { $_ -is [System.Runtime.InteropServices.GuidAttribute] } | ForEach-Object { $_.Value }) -join ',') + '  IsImport=' + $t.IsImport + '  IsInterface=' + $t.IsInterface)
        if ($t.IsInterface) {
            $i = 0
            foreach ($m in $t.GetMethods([Reflection.BindingFlags]'Public,Instance,DeclaredOnly')) {
                $params = ($m.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }) -join ', '
                Write-Output ('  [' + $i + '] ' + $m.ReturnType.Name + ' ' + $m.Name + '(' + $params + ')')
                $i++
            }
        }
    }
}
Write-Output ''
Write-Output '=== ManagementScope 非公开字段 ==='
$t2 = $asm.GetType('System.Management.ManagementScope')
foreach ($f in $t2.GetFields([Reflection.BindingFlags]'NonPublic,Instance')) {
    Write-Output ('  field: ' + $f.FieldType.FullName + ' ' + $f.Name)
}
