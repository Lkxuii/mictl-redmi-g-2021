# dump System.Management 内部的 COM 接口声明（方法顺序 + GUID）
$asm = [Reflection.Assembly]::LoadWithPartialName('System.Management')
foreach ($t in $asm.GetTypes()) {
    if ($t.Name -match 'IWbemLocator|IWbemServices|WbemLocator') {
        Write-Output ('=== ' + $t.FullName + '  Guid=' + (($t.GetCustomAttributes($false) | Where-Object { $_ -is [System.Runtime.InteropServices.GuidAttribute] } | ForEach-Object { $_.Value }) -join ',') + '  IsComImport=' + $t.IsImport)
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
