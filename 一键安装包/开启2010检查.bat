@echo off
setlocal
set "TARGET=%~1"
if "%TARGET%"=="" set "TARGET=%~dp0"
if not exist "%TARGET%\" set "TARGET=%~dp0"
if "%TARGET:~-1%"=="\" set "TARGET=%TARGET:~0,-1%"
> "%TARGET%\vs2010check.on" echo VS2010 compatibility check ENABLED
echo.
echo   [已开启] VS2010 兼容性检查: %TARGET%
echo           红线 = VS2010 直接编不过；绿线 = 能编过但结果不对
echo           红线会在 2 秒内自动出现（不用重开文件）
echo.
pause
