# 提取 WMIBinaryMofResource 中 MIFS 相关的 MOF 数据
$out = New-Object System.Collections.Generic.List[string]
$dir = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\mof'
New-Item -ItemType Directory -Force -Path $dir | Out-Null

try {
    $mofs = Get-CimInstance -Namespace root\wmi -ClassName WMIBinaryMofResource -ErrorAction Stop
    foreach ($m in $mofs) {
        $name = $m.MofResourceName
        $out.Add('=== ' + $name + ' (MofData len: ' + ($m.MofData | Measure-Object).Count + ') ===')
        if ($m.MofData) {
            $bytes = [byte[]]$m.MofData
            # MOF 二进制通常是 UTF-16 文本
            $text = [Text.Encoding]::Unicode.GetString($bytes)
            if ($text -match 'MICommonInterface|MiInterface|B60BFB48') {
                $safe = ($name -replace '[\\/:*?"<>|]', '_')
                $p = Join-Path $dir ($safe + '.txt')
                [IO.File]::WriteAllText($p, $text)
                $out.Add('  MATCH! saved to ' + $p)
            }
            else {
                # 也按 UTF-8 试试
                $text8 = [Text.Encoding]::UTF8.GetString($bytes)
                if ($text8 -match 'MICommonInterface|MiInterface|B60BFB48') {
                    $safe = ($name -replace '[\\/:*?"<>|]', '_')
                    $p = Join-Path $dir ($safe + '_u8.txt')
                    [IO.File]::WriteAllText($p, $text8)
                    $out.Add('  MATCH-utf8! saved to ' + $p)
                }
            }
        }
    }
}
catch {
    $out.Add('ERROR: ' + $_.Exception.Message)
}
$out | Out-File -FilePath 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5\mof-extract-result.txt' -Encoding utf8
