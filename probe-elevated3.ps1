$out = New-Object System.Collections.Generic.List[string]
$out.Add('IsAdmin=' + (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))

$in = New-Object byte[] 32
$in[1] = 250   # GET
$in[3] = 13    # FAN_SPEEDS

$r = Invoke-CimMethod -Namespace root\WMI -ClassName MICommonInterface -MethodName MiInterface -Arguments @{ InData = [byte[]]$in }
$out.Add('返回对象类型: ' + $r.GetType().FullName)
foreach ($p in $r.PSObject.Properties) {
    $v = $p.Value
    if ($v -is [byte[]]) { $v = (($v | ForEach-Object { $_.ToString('X2') }) -join ' ') }
    $out.Add(('  ' + $p.Name + ' = ' + $v))
}
$out.Add('--- ToString ---')
$out.Add($r.ToString())

$out | Out-File -FilePath 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\probe-elevated-result.txt' -Encoding utf8
