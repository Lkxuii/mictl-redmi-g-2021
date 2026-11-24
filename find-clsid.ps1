# 从 System.Management.dll 提取 WbemLocator 的真实 CLSID
$asm = [Reflection.Assembly]::LoadWithPartialName('System.Management')
foreach ($t in $asm.GetTypes()) {
    $attrs = $t.GetCustomAttributes($false) | Where-Object { $_ -is [System.Runtime.InteropServices.GuidAttribute] }
    foreach ($a in $attrs) {
        Write-Output ($t.FullName + '  ->  ' + $a.Value)
    }
}
