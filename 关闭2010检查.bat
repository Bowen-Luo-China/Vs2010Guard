@echo off
setlocal
set "TARGET=%~1"
if "%TARGET%"=="" set "TARGET=%~dp0"
if not exist "%TARGET%\" set "TARGET=%~dp0"
if "%TARGET:~-1%"=="\" set "TARGET=%TARGET:~0,-1%"
set /a N=0
:loop
if exist "%TARGET%\vs2010check.on" (
  del "%TARGET%\vs2010check.on"
  echo   已删除: %TARGET%\vs2010check.on
  set /a N+=1
)
for %%I in ("%TARGET%\..") do set "PARENT=%%~fI"
if /i "%PARENT%"=="%TARGET%" goto done
set "TARGET=%PARENT%"
goto loop
:done
echo.
if %N%==0 goto none
echo   [已关闭] VS2010 兼容性检查
echo            红线会在 2 秒内自动消失（不用重开文件）
goto finish
:none
echo   [本来就是关闭的] 这条目录链上没有开关文件
:finish
echo.
pause
