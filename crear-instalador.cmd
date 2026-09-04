@echo off
REM ============================================================
REM  Generador de Anexos - instalador firmado (Setup.exe)
REM ============================================================
setlocal EnableExtensions
cd /d "%~dp0"

echo.
echo ============================================================
echo  Creando el instalador firmado de Generador de Anexos
echo  Carpeta: %CD%
echo ============================================================

if not defined GENERADOR_ANEXOS_SIGN_PFX goto :fallo_firma_config
if not exist "%GENERADOR_ANEXOS_SIGN_PFX%" goto :fallo_firma_config
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0validar-certificado-firma.ps1"
if errorlevel 1 goto :fallo_firma_config

where signtool >nul 2>nul
if errorlevel 1 goto :fallo_firma

echo.
echo [1/4] Compilando la aplicacion...
call "%~dp0compilar.cmd" --verificar
if errorlevel 1 goto :fallo_compilacion
if not exist "%~dp0publicado\GeneradorAnexos.exe" goto :fallo_compilacion

echo.
echo [2/4] Firmando la aplicacion...
if defined GENERADOR_ANEXOS_SIGN_PASSWORD (
    signtool sign /fd SHA256 /tr https://timestamp.digicert.com /td SHA256 /f "%GENERADOR_ANEXOS_SIGN_PFX%" /p "%GENERADOR_ANEXOS_SIGN_PASSWORD%" "%~dp0publicado\GeneradorAnexos.exe"
) else (
    signtool sign /fd SHA256 /tr https://timestamp.digicert.com /td SHA256 /f "%GENERADOR_ANEXOS_SIGN_PFX%" "%~dp0publicado\GeneradorAnexos.exe"
)
if errorlevel 1 goto :fallo_firma
signtool verify /pa /all /v "%~dp0publicado\GeneradorAnexos.exe"
if errorlevel 1 goto :fallo_firma

echo.
echo [3/4] Buscando Inno Setup 6...
set "ISCC="
call :probar_iscc "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC call :probar_iscc "%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not defined ISCC call :probar_iscc "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not defined ISCC (
    for /f "delims=" %%D in ('where ISCC 2^>nul') do if not defined ISCC call :probar_iscc "%%D"
)
if not defined ISCC goto :fallo_inno
echo  Compilador encontrado: %ISCC%

echo.
echo [4/4] Generando y firmando el instalador...
if exist "%~dp0instalador\salida" rmdir /s /q "%~dp0instalador\salida"
"%ISCC%" /Q "%~dp0instalador\GeneradorAnexos.iss"
if errorlevel 1 goto :fallo_iss

set "SETUP="
for %%F in ("%~dp0instalador\salida\GeneradorAnexos-*-Setup.exe") do set "SETUP=%%~fF"
if not defined SETUP goto :fallo_firma

if defined GENERADOR_ANEXOS_SIGN_PASSWORD (
    signtool sign /fd SHA256 /tr https://timestamp.digicert.com /td SHA256 /f "%GENERADOR_ANEXOS_SIGN_PFX%" /p "%GENERADOR_ANEXOS_SIGN_PASSWORD%" "%SETUP%"
) else (
    signtool sign /fd SHA256 /tr https://timestamp.digicert.com /td SHA256 /f "%GENERADOR_ANEXOS_SIGN_PFX%" "%SETUP%"
)
if errorlevel 1 goto :fallo_firma
signtool verify /pa /all /v "%SETUP%"
if errorlevel 1 goto :fallo_firma

echo.
echo ============================================================
echo  LISTO
echo  El instalador firmado esta en:
echo  %~dp0instalador\salida
echo ============================================================
echo.
start "" "%~dp0instalador\salida"
pause
exit /b 0

:probar_iscc
if exist "%~1" set "ISCC=%~1"
exit /b 0

:fallo_compilacion
echo.
echo ============================================================
echo  NO SE PUDO COMPILAR LA APLICACION
echo  Ejecute compilar.cmd por separado y revise el mensaje de error.
echo ============================================================
echo.
pause
exit /b 1

:fallo_inno
echo.
echo ============================================================
echo  NO SE ENCONTRO INNO SETUP 6
echo  Instalelo desde https://jrsoftware.org/isdl.php
echo ============================================================
echo.
pause
exit /b 1

:fallo_iss
echo.
echo ============================================================
echo  LA GENERACION DEL INSTALADOR FALLO
echo ============================================================
echo.
pause
exit /b 1

:fallo_firma_config
echo.
echo ============================================================
echo  NO SE PUBLICARA UN INSTALADOR SIN FIRMA
echo  Configure GENERADOR_ANEXOS_SIGN_PFX y GENERADOR_ANEXOS_SIGN_PASSWORD.
echo  La huella SHA-256 tambien debe estar fijada en
echo  ConfiguracionActualizaciones.cs.
echo ============================================================
echo.
pause
exit /b 1

:fallo_firma
echo.
echo ============================================================
echo  LA FIRMA O SU VERIFICACION FALLO
echo  El instalador no es apto para distribucion.
echo ============================================================
echo.
pause
exit /b 1
