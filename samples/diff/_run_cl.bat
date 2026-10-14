@echo off
call "D:\Application\VS2010\VC\bin\vcvars32.bat" >nul 2>&1
cd /d "D:\vs2010-kit\samples\diff"
for %%f in (d*.c t*.c) do (
  echo ### FILE %%~nxf
  cl /nologo /c /W3 /Fo"%TEMP%\diffout.obj" "%%f"
)
