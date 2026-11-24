# 扫描 GamingBoxHost 代码段里的 MIFS op/fn 赋值指令
# x64 mov byte ptr [reg+disp8], imm8 = C6 /0：C6 41 01 FA (rcx+1)、C6 43 01 FA (rbx+1)、C6 44 24 01 FA (rsp+1)...
# 以及 disp32：C6 83 xx xx xx xx FA (rbx+disp32)
$exe = 'C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\GamingBoxHost.exe'
$b = [IO.File]::ReadAllBytes($exe)

$ops = @{ 0xFA = 'GET'; 0xFB = 'SET' }
$hits = New-Object System.Collections.Generic.List[string]

# disp8 形式：C6 <modrm: mod=01, reg=0, rm=reg> 01 <imm>   → C6 4x 01 FA|FB
for ($i = 0; $i -lt $b.Length - 4; $i++) {
    if ($b[$i] -eq 0xC6 -and ($b[$i+1] -band 0xC7) -eq 0x40 -and $b[$i+2] -eq 0x01) {
        $imm = $b[$i+3]
        if ($imm -eq 0xFA -or $imm -eq 0xFB) {
            # 附近 ±48 字节找 mov byte [reg+3], imm（C6 <modrm mod=01> 03 <imm>）
            $fnFound = ''
            for ($j = [Math]::Max(0, $i - 48); $j -lt [Math]::Min($b.Length - 4, $i + 48); $j++) {
                if ($b[$j] -eq 0xC6 -and ($b[$j+1] -band 0xC7) -eq 0x40 -and $b[$j+2] -eq 0x03 -and $b[$j+3] -ne 0xFA -and $b[$j+3] -ne 0xFB) {
                    $fnFound = '0x' + $b[$j+3].ToString('X2')
                    break
                }
            }
            $hits.Add(('0x' + $i.ToString('X') + '  ' + $ops[$imm] + '  fn=' + $fnFound))
        }
    }
}

Write-Output ('命中 MIFS op 赋值: ' + $hits.Count + ' 处')
foreach ($h in $hits) { Write-Output ('  ' + $h) }
