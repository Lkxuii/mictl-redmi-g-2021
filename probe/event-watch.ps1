# 事件通道测试协调：提权监听 90 秒 + 可见提示
$root = 'D:\ZCode\.zcode\workspace\d5dbea00-ec3e-4c33-a2a1-69fb62e36ba5'
Remove-Item ($root + '\probe\evwatch-result.txt') -ErrorAction SilentlyContinue

# 1) 弹可见提示（60 秒）
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$f = New-Object System.Windows.Forms.Form
$f.Text = 'MiCtl 事件测试'
$f.Size = New-Object System.Drawing.Size(500, 220)
$f.StartPosition = 'CenterScreen'
$f.TopMost = $true
$f.BackColor = [System.Drawing.Color]::FromArgb(22, 22, 30)
$lbl = New-Object System.Windows.Forms.Label
$lbl.Text = "MiCtl 事件通道测试（90 秒）`r`n`r`n现在请：`r`n  1. 用键盘快捷键切换触控板 2-3 次`r`n  2. 按 Fn+Esc 2 次`r`n`r`n窗口自动关闭后程序会分析结果"
$lbl.ForeColor = [System.Drawing.Color]::White
$lbl.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 11)
$lbl.Dock = 'Fill'
$f.Controls.Add($lbl)
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 60000
$timer.Add_Tick({ $f.Close() })
$timer.Start()
$f.Add_Shown({ $f.Activate() })

# 2) 提权启动监听（90 秒，与提示窗口并行）
Start-Process -Verb RunAs -WindowStyle Hidden -FilePath 'pwsh' -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File',($root + '\probe\ev-listen.ps1')

[void]$f.ShowDialog()

# 3) 等监听收尾
Start-Sleep 35

# 4) 读结果
if (Test-Path ($root + '\probe\evwatch-result.txt')) {
    Write-Output '=== 收到的事件 ==='
    Get-Content ($root + '\probe\evwatch-result.txt')
} else {
    Write-Output '监听无输出（结果文件不存在）'
}
