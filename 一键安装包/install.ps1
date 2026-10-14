$ErrorActionPreference = 'Stop'
$vsix = Join-Path $PSScriptRoot 'Vs2010Guard.vsix'

Write-Host '=================================================='
Write-Host '  VS2010 兼容性提示插件 (Vs2010Guard) - 一键安装'
Write-Host '=================================================='
Write-Host ''

if (-not (Test-Path $vsix)) {
    Write-Host '[错误] 找不到 Vs2010Guard.vsix，请把它和本脚本放在同一个文件夹里。'
    exit 1
}

$pf86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
$vswhere = Join-Path $pf86 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    Write-Host '[错误] 没找到 vswhere.exe，请确认这台机器装了 Visual Studio 2022 或 2026。'
    exit 1
}

Write-Host '正在查找 Visual Studio ...'
$instances = & $vswhere -prerelease -products * -format json | ConvertFrom-Json
$ok = @()
foreach ($i in $instances) {
    if ($i.installationVersion -match '^(1[7-9]|2[0-9])\.') {
        $ok += $i
        Write-Host ('  ' + $i.displayName + '   ' + $i.installationPath)
    }
}
if ($ok.Count -eq 0) {
    Write-Host '[错误] 没有检测到 Visual Studio 2022 / 2026。'
    exit 1
}
Write-Host ''

if (Get-Process devenv -ErrorAction SilentlyContinue) {
    Write-Host '[注意] Visual Studio 正开着，装完需要重启 VS 才生效。'
    Write-Host ''
}

$installer = Join-Path $ok[0].installationPath 'Common7\IDE\VSIXInstaller.exe'
if (-not (Test-Path $installer)) {
    Write-Host ('[错误] 找不到 VSIXInstaller.exe：' + $installer)
    exit 1
}

Write-Host '开始安装 ...'
& $installer /quiet $vsix
$rc = $LASTEXITCODE
Write-Host ''
if ($rc -eq 0) {
    Write-Host '[完成] 安装成功。'
    Write-Host ''
    Write-Host '怎么用：'
    Write-Host '  1) 把「开启2010检查.bat」复制到你的作业文件夹里，双击它；'
    Write-Host '  2) 在 VS 里重新打开 .c 文件；'
    Write-Host '  3) VS2010 编不过的地方会标红线，鼠标悬停能看到原因和改法；'
    Write-Host '     也可以在 VS 的「工具」菜单里对当前解决方案一键开关。'
} else {
    Write-Host ('[失败] 安装程序返回代码 ' + $rc)
    Write-Host '  常见原因：Visual Studio 还开着；或装的是 VS2019 及更早版本。'
}
exit $rc
