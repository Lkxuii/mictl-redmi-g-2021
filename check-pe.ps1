# 检查 GamingBoxHost.exe 是否 .NET 程序集 + 提取字符串上下文
$path = 'C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\GamingBoxHost.exe'
$bytes = [IO.File]::ReadAllBytes($path)
$ascii = [Text.Encoding]::ASCII.GetString($bytes)

Write-Output ('BSJB(.NET元数据) 存在: ' + ($ascii.Contains('BSJB')))
Write-Output ('mscoree 导入存在: ' + ($ascii.Contains('mscoree')))
Write-Output ('_CorExeMain 存在: ' + ($ascii.Contains('_CorExeMain')))

# UTF-16 字符串上下文：找 MiInterface / MICommonInterface / InData 前后各 400 字符
$utf16 = [Text.Encoding]::Unicode.GetString($bytes)
foreach ($key in @('MiInterface', 'MICommonInterface', 'InData', 'MIFS')) {
    $pos = $utf16.IndexOf($key)
    while ($pos -ge 0) {
        $start = [Math]::Max(0, $pos - 400)
        $len = [Math]::Min(800, $utf16.Length - $start)
        $ctx = $utf16.Substring($start, $len) -replace '[\x00-\x1F]', '·'
        Write-Output ('==== [' + $key + '] @' + $pos + ' ====')
        Write-Output $ctx
        $next = $utf16.IndexOf($key, $pos + 1)
        if ($next -lt 0) { break } else { $pos = $next }
    }
}
