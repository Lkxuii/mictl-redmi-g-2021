# 干净克隆构建测试：验证"从 GitHub 克隆后直接 build.ps1 能否编译"（含依赖自动下载分支）
$dst = Join-Path $env:TEMP 'mictl-clone-test'
if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }

gh repo clone Lkxuii/mictl-redmi-g-2021 $dst -- --quiet 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Output 'CLONE FAILED'; exit 1 }
Write-Output ('CLONE OK -> ' + $dst)
Write-Output ('克隆到的提交: ' + (git -C $dst rev-parse --short HEAD))

Write-Output '--- build.ps1（libs 为空，应触发自动下载）---'
& pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $dst 'MiCtl\build.ps1')

Write-Output '--- 产物 ---'
$outDir = Join-Path $dst 'MiCtl\out'
if (Test-Path $outDir) {
    Get-ChildItem $outDir | ForEach-Object { Write-Output ('  ' + $_.Name + '  ' + $_.Length) }
} else { Write-Output '  (无 out 目录)' }
Write-Output ('libs 文件数: ' + @(Get-ChildItem (Join-Path $dst 'MiCtl\libs') -ErrorAction SilentlyContinue).Count)
