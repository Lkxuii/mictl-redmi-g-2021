# 用 SWbemServices.ExecMethod 按实例路径调用（模拟 GamingBoxHost 的姿势）
$out = New-Object System.Collections.Generic.List[string]
$out.Add('IsAdmin=' + (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))

$loc = New-Object -ComObject WbemScripting.SWbemLocator
$svc = $loc.ConnectServer('.', 'root\wmi', '', '')
$svc.Security_.ImpersonationLevel = 3
$svc.Security_.AuthenticationLevel = 6

$cls = $svc.Get('MICommonInterface')
$out.Add('class path: ' + $cls.Path_.Path)

# 取方法参数模板
$gm = $cls.GetMethod_('MiInterface')
$out.Add('GetMethod 返回属性: ' + (($gm.PSObject.Properties.Name) -join ', '))
$inDef = $gm.objwbemInParameters
if (-not $inDef) { $inDef = $gm.InParameters }
$out.Add('inDef 属性: ' + (($inDef.PSObject.Properties.Name) -join ', '))

$in = $inDef.SpawnInstance_()
$buf = New-Object byte[] 32
$buf[1] = 250   # GET
$buf[3] = 13    # FAN_SPEEDS
$in.InData = [byte[]]$buf

$instPath = "MICommonInterface.InstanceName='ACPI\PNP0C14\MIFS_0'"
try {
    $r = $svc.ExecMethod($instPath, 'MiInterface', 0, $in)
    $out.Add('ExecMethod 返回属性: ' + (($r.PSObject.Properties.Name) -join ', '))
    foreach ($pn in $r.PSObject.Properties.Name) {
        $v = $r.$pn
        if ($v -is [byte[]]) { $v = (($v | ForEach-Object { $_.ToString('X2') }) -join ' ') }
        $out.Add('  ' + $pn + ' = ' + ($v ?? '(null)'))
    }
} catch {
    $out.Add('ExecMethod EXCEPTION: ' + $_.Exception.Message)
}

$out | Out-File -FilePath 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\probe-swbem-result.txt' -Encoding utf8
