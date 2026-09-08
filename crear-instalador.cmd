@echo off
setlocal
cd /d "%~dp0"
call compilar.cmd --verificar
if errorlevel 1 goto fallo
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\crear-instalador-manual.ps1"
if errorlevel 1 goto fallo
echo Instalador creado en instalador\salida. Compatible con actualizaciones desde la aplicacion.
pause
exit /b 0
:fallo
echo No se pudo crear el instalador. Revise el error anterior.
pause
exit /b 1
