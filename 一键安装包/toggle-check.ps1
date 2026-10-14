param([string]$Folder)

# VS2010 兼容性检查 —— 切换某个目录的开关
# 用法：  toggle-check.ps1 "D:\作业\第3周"
# 在 VS 里作为“外部工具”调用时，参数填 $(SolutionDir)
#
# 注意：判断是“本目录或任意上级目录里有 vs2010check.on 就算开启”，
#       所以关闭的时候要整条上级链一起清，否则上层那个会继续生效。

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Folder)) { $Folder = (Get-Location).Path }

$Folder = $Folder.Trim()
if ($Folder.Length -gt 1) { $Folder = $Folder.TrimEnd('\') }
if ($Folder.EndsWith(':')) { $Folder = $Folder + '\' }

if (-not (Test-Path -LiteralPath $Folder)) {
    Write-Host ('[错误] 找不到目录: ' + $Folder)
    exit 1
}

# 这条目录链上（含自己，直到盘符根）所有的开关文件
$chain = @()
try { $dir = Get-Item -LiteralPath $Folder } catch { $dir = $null }
while ($dir -ne $null) {
    $m = Join-Path $dir.FullName 'vs2010check.on'
    if (Test-Path -LiteralPath $m) { $chain += $m }
    $dir = $dir.Parent
}

if ($chain.Count -gt 0) {
    foreach ($m in $chain) { Remove-Item -LiteralPath $m -Force }
    Write-Host ''
    Write-Host ('[已关闭] VS2010 兼容性检查: ' + $Folder)
    if ($chain.Count -gt 1) {
        Write-Host ('          顺带清掉了上级目录里的 ' + ($chain.Count - 1) + ' 个开关文件（不然它们还会把检查打开）:')
        $chain | Select-Object -Skip 1 | ForEach-Object { Write-Host ('            ' + $_) }
    }
    Write-Host '          红线/绿线会在 2 秒内自动消失（不用重开文件）'
} else {
    $marker = Join-Path $Folder 'vs2010check.on'
    Set-Content -LiteralPath $marker -Value '本文件存在 = 对本目录（含子目录）开启 VS2010 兼容性检查' -Encoding UTF8
    Write-Host ''
    Write-Host ('[已开启] VS2010 兼容性检查: ' + $Folder)
    Write-Host '          红线 = VS2010 直接编不过；绿线 = 能编过但结果不对'
    Write-Host '          红线会在 2 秒内自动出现（不用重开文件）'
}
Write-Host ''
exit 0
