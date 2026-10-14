@echo off
call "D:\Application\VS2010\VC\bin\vcvars32.bat" >nul 2>&1
cd /d "D:\vs2010-kit\samples\diff"
for %%f in (t06_snprintf.c t23_isnan.c t30_static_assert.c t01_mixed_decl.c) do (
  echo ### FILE %%~nxf
  cl /nologo /W3 /Fe"%TEMP%\lk.exe" "%%f"
  echo --- exit %ERRORLEVEL%
)
