@echo off
REM ============================================================
REM  Generador de Anexos - compilacion automatica a .exe
REM  Doble clic sobre este archivo desde la carpeta winui
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
    echo  Se descargara automaticamente desde Microsoft.
    echo  No se necesitan permisos de administrador.
    echo  La primera descarga puede tardar varios minutos.
    echo.
    call :instalar_sdk_dotnet8
    if errorlevel 1 goto :fallo_sdk

    call :buscar_sdk_dotnet8
    if not defined DOTNET_CMD goto :fallo_sdk
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

REM 1. SDK descargado previamente por este mismo compilador.
if defined LOCALAPPDATA call :probar_sdk "%LOCALAPPDATA%\GeneradorAnexos\dotnet8\dotnet.exe"

REM 2. Instalaciones normales del sistema o del usuario.
if not defined DOTNET_CMD call :probar_sdk "%ProgramFiles%\dotnet\dotnet.exe"
if not defined DOTNET_CMD call :probar_sdk "%USERPROFILE%\.dotnet\dotnet.exe"

REM 3. Cualquier dotnet disponible en PATH.
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

:instalar_sdk_dotnet8
if defined LOCALAPPDATA (
    set "DOTNET_SDK_DIR=%LOCALAPPDATA%\GeneradorAnexos\dotnet8"
) else (
    set "DOTNET_SDK_DIR=%~dp0.herramientas\dotnet8"
)

set "GA_INSTALL_SCRIPT=%TEMP%\GeneradorAnexos-dotnet-install-%RANDOM%-%RANDOM%.ps1"
set "GA_INSTALL_URL=https://dot.net/v1/dotnet-install.ps1"
set "GA_INSTALL_URL_ALT=https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.ps1"

echo  Destino: %DOTNET_SDK_DIR%
echo  Descargando el instalador oficial de Microsoft...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; try { Invoke-WebRequest -UseBasicParsing -Uri $env:GA_INSTALL_URL -OutFile $env:GA_INSTALL_SCRIPT } catch { Invoke-WebRequest -UseBasicParsing -Uri $env:GA_INSTALL_URL_ALT -OutFile $env:GA_INSTALL_SCRIPT }"
if errorlevel 1 (
    del /q "%GA_INSTALL_SCRIPT%" >nul 2>nul
    exit /b 1
)

echo  Instalando el SDK de .NET 8 para Windows x64...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%GA_INSTALL_SCRIPT%" -Channel 8.0 -Quality GA -Architecture x64 -InstallDir "%DOTNET_SDK_DIR%" -NoPath
set "RESULTADO_INSTALACION=%ERRORLEVEL%"
del /q "%GA_INSTALL_SCRIPT%" >nul 2>nul

if not "%RESULTADO_INSTALACION%"=="0" exit /b 1
if not exist "%DOTNET_SDK_DIR%\dotnet.exe" exit /b 1
exit /b 0

:fallo_sdk
echo.
echo ============================================================
echo  NO SE PUDO PREPARAR EL SDK DE .NET 8
echo.
echo  Compruebe que el equipo tenga conexion a Internet y que el
echo  antivirus o proxy permita descargar desde Microsoft.
echo.
echo  Como alternativa, instale manualmente el SDK de .NET 8 desde:
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
