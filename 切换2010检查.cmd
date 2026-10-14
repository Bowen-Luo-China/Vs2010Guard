@echo off
rem Toggle the VS2010 compatibility check for a folder.
rem Double-click = current folder. Drag a folder onto it = that folder.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0toggle-check.ps1" "%~1"
if "%~1"=="" pause
