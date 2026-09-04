@echo off
REM ============================================================
REM  Generador de Anexos - compilacion automatica a .exe
REM  Doble clic sobre este archivo desde la carpeta raíz del repositorio
REM ============================================================
setlocal EnableExtensions
cd /d "%~dp0"

set "MODO_VERIFICACION="
if /i "%~1"=="--verificar" set "MODO_VERIFICACION=1"

set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
set "NUGET_XMLDOC_MODE=skip"

echo.
echo ============================================================
echo  Compilando Generador de Anexos
echo  Carpeta: %CD%
echo ============================================================

REM Los parentesis en la ruta a veces molestan al compilador de XAML.
set "RUTA=%~dp0"
set "SINPAR=%RUTA:(=%"
if not "%SINPAR%"=="%RUTA%" (
    echo.
    echo  AVISO: la ruta contiene parentesis. Si la compilacion falla,
    echo  mueva la carpeta a C:\GA y vuelva a ejecutar compilar.cmd.
)

echo.
echo [1/4] Buscando el SDK de .NET 8...
call :buscar_sdk_dotnet8

if not defined DOTNET_CMD (
    echo.
    echo  No se encontro el SDK de .NET 8.
    echo  Instale manualmente el SDK oficial de .NET 8.
    echo  Por seguridad este script no descarga ni ejecuta herramientas.
    echo.
    echo  Comando recomendado:
    echo  winget install --id Microsoft.DotNet.SDK.8 --exact --source winget
    echo.
    goto :fallo_sdk
)

for %%D in ("%DOTNET_CMD%") do set "DOTNET_ROOT=%%~dpD"
set "PATH=%DOTNET_ROOT%;%PATH%"

echo  SDK encontrado:
"%DOTNET_CMD%" --version
if errorlevel 1 goto :fallo_sdk

echo.
echo [2/4] Comprobando archivos del proyecto...
if not exist "GeneradorAnexos.WinUI.sln" (
    echo  ERROR: no se encontro GeneradorAnexos.WinUI.sln.
    goto :fallo
)
if not exist "src\GeneradorAnexos.WinUI\GeneradorAnexos.WinUI.csproj" (
    echo  ERROR: no se encontro el proyecto WinUI 3.
    goto :fallo
)
echo  Archivos principales encontrados.

echo.
echo [3/4] Descargando dependencias del proyecto...
"%DOTNET_CMD%" restore "GeneradorAnexos.WinUI.sln"
if errorlevel 1 goto :fallo

echo.
echo [4/4] Generando el ejecutable autocontenido...
if exist "%~dp0publicado" rmdir /s /q "%~dp0publicado"
if exist "%~dp0publicado" goto :fallo
"%DOTNET_CMD%" publish "src\GeneradorAnexos.WinUI\GeneradorAnexos.WinUI.csproj" -c Release -r win-x64 --self-contained true -o "%~dp0publicado"
if errorlevel 1 goto :fallo

echo.
echo ============================================================
echo  LISTO
echo  El programa esta en:
echo  %~dp0publicado\GeneradorAnexos.exe
echo ============================================================
echo.
if defined MODO_VERIFICACION exit /b 0
start "" "%~dp0publicado"
pause
exit /b 0

:buscar_sdk_dotnet8
set "DOTNET_CMD="

REM 1. Instalaciones oficiales normales del sistema o del usuario.
call :probar_sdk "%ProgramFiles%\dotnet\dotnet.exe"
if not defined DOTNET_CMD call :probar_sdk "%USERPROFILE%\.dotnet\dotnet.exe"

REM 2. Cualquier dotnet disponible en PATH.
if not defined DOTNET_CMD (
    for /f "delims=" %%D in ('where dotnet 2^>nul') do if not defined DOTNET_CMD call :probar_sdk "%%D"
)
exit /b 0

:probar_sdk
if not exist "%~1" exit /b 0
"%~1" --list-sdks 2>nul | "%SystemRoot%\System32\findstr.exe" /b /r "8\." >nul
if errorlevel 1 exit /b 0
set "DOTNET_CMD=%~1"
exit /b 0

:fallo_sdk
echo.
echo ============================================================
echo  NO SE PUDO PREPARAR EL SDK DE .NET 8
echo.
echo  Instale manualmente el SDK oficial de .NET 8 desde:
echo  https://dotnet.microsoft.com/download/dotnet/8.0
echo ============================================================
echo.
if defined MODO_VERIFICACION exit /b 1
pause
exit /b 1

:fallo
echo.
echo ============================================================
echo  LA COMPILACION FALLO
echo  Copie TODO el texto de arriba y enviemelo.
echo ============================================================
echo.
if defined MODO_VERIFICACION exit /b 1
pause
exit /b 1
