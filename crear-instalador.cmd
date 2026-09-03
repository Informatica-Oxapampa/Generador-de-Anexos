@echo off
REM ============================================================
REM  Generador de Anexos - creacion del instalador (Setup.exe)
REM
REM  Paso 1: compila la aplicacion  ->  publicado\
REM  Paso 2: empaqueta el instalador ->  instalador\salida
REM
REM  Doble clic sobre este archivo desde la carpeta raíz del repositorio.
REM ============================================================
setlocal EnableExtensions
cd /d "%~dp0"

echo.
echo ============================================================
echo  Creando el instalador de Generador de Anexos
echo  Carpeta: %CD%
echo ============================================================

echo.
echo [1/3] Compilando la aplicacion...
call "%~dp0compilar.cmd" --verificar
if errorlevel 1 goto :fallo_compilacion

if not exist "%~dp0publicado\GeneradorAnexos.exe" goto :fallo_compilacion
echo  Aplicacion compilada en: %~dp0publicado

echo.
echo [2/3] Buscando Inno Setup 6...
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
echo [3/3] Generando el instalador...
"%ISCC%" /Q "%~dp0instalador\GeneradorAnexos.iss"
if errorlevel 1 goto :fallo_iss

REM Fuera de un bloque if (...): en CMD el SET dentro de parentesis no se ve
REM hasta que el bloque termina (sin delayed expansion).
set "SETUP="
for %%F in ("%~dp0instalador\salida\GeneradorAnexos-*-Setup.exe") do set "SETUP=%%~fF"

if not defined GENERADOR_ANEXOS_SIGN_PFX goto :despues_firma
if not defined SETUP goto :despues_firma

echo.
echo [extra] Firmando el instalador...
where signtool >nul 2>nul
if errorlevel 1 (
    echo  AVISO: no se encontro signtool.exe. El Setup queda sin firmar.
    goto :despues_firma
)
if defined GENERADOR_ANEXOS_SIGN_PASSWORD (
    signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f "%GENERADOR_ANEXOS_SIGN_PFX%" /p "%GENERADOR_ANEXOS_SIGN_PASSWORD%" "%SETUP%"
) else (
    signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f "%GENERADOR_ANEXOS_SIGN_PFX%" "%SETUP%"
)
if errorlevel 1 echo  AVISO: la firma fallo. El Setup se entrega sin firmar.

:despues_firma

echo.
echo ============================================================
echo  LISTO
echo  El instalador esta en:
echo  %~dp0instalador\salida
echo.
echo  Entregue ese unico archivo .exe a los usuarios finales.
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
echo.
echo  Instalelo una sola vez en este equipo desde:
echo  https://jrsoftware.org/isdl.php
echo.
echo  Es gratuito y no hace falta en los equipos de los usuarios:
echo  solo se necesita aqui, para generar el Setup.exe.
echo ============================================================
echo.
pause
exit /b 1

:fallo_iss
echo.
echo ============================================================
echo  LA GENERACION DEL INSTALADOR FALLO
echo  Copie TODO el texto de arriba para poder revisarlo.
echo ============================================================
echo.
pause
exit /b 1
