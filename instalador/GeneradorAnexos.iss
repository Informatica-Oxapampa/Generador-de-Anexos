; ============================================================================
;  Generador de Anexos - script de instalador (Inno Setup 6)
;  Municipalidad Provincial de Oxapampa - Oficina de Tecnologia de la Informacion
;
;  Genera un unico Setup.exe con asistente de instalacion en espanol.
;
;  Requisitos para compilar este script:
;    1. Haber ejecutado antes  compilar.cmd   (crea publicado\ en la raíz)
;    2. Tener instalado Inno Setup 6  ->  https://jrsoftware.org/isdl.php
;
;  Forma facil de generarlo: doble clic en  crear-instalador.cmd
; ============================================================================

#define MiNombre        "Generador de Anexos"
#define MiVersion       "1.0.3"
#define MiPublicador    "Municipalidad Provincial de Oxapampa - Oficina de Tecnologia de la Informacion"
#define MiUrl           "https://www.munioxapampa.gob.pe"
#define MiEjecutable    "GeneradorAnexos.exe"
#define MiCarpetaOrigen "..\publicado"
#define MiIcono         "..\src\GeneradorAnexos.WinUI\Assets\logo.ico"

; --- Comprobacion temprana: sin la carpeta publicado no hay nada que instalar ---
#if !FileExists(AddBackslash(SourcePath) + MiCarpetaOrigen + "\" + MiEjecutable)
  #error No se encontro ..\publicado\GeneradorAnexos.exe. Ejecute compilar.cmd antes de generar el instalador.
#endif

; --- La version del script debe coincidir con la del ejecutable publicado ---
;     Evita publicar un Setup que anuncia una version distinta de la que
;     realmente instala, que romperia la comparacion del actualizador.
#define VersionEjecutable GetFileVersion(AddBackslash(SourcePath) + MiCarpetaOrigen + "\" + MiEjecutable)
#if VersionEjecutable != MiVersion + ".0"
  #error La version definida en MiVersion no coincide con la del ejecutable publicado. Actualice <Version> en el csproj o MiVersion en este script.
#endif

; --- Redistribuible de Visual C++ (opcional pero recomendado) ---
;     Si coloca VC_redist.x64.exe en la carpeta  instalador\redist\  el
;     instalador lo incluira y lo ejecutara en silencio solo si falta.
#define VcRedistArchivo "redist\VC_redist.x64.exe"
#if FileExists(AddBackslash(SourcePath) + VcRedistArchivo)
  #define IncluyeVcRedist
#endif

[Setup]
; AppId identifica al programa entre versiones. NO cambiar nunca: es lo que
; permite que una version nueva actualice a la anterior en lugar de duplicarla.
AppId={{7F2C6A18-4D9B-4C3E-9A61-3B8E5D2F71C4}
AppName={#MiNombre}
AppVersion={#MiVersion}
AppVerName={#MiNombre} {#MiVersion}
VersionInfoVersion={#MiVersion}
VersionInfoCompany={#MiPublicador}
VersionInfoDescription=Instalador de {#MiNombre}
AppPublisher={#MiPublicador}
AppPublisherURL={#MiUrl}
AppSupportURL={#MiUrl}
AppUpdatesURL={#MiUrl}

; Carpeta exclusiva y fija: evita mezclar la aplicación con archivos ajenos.
; Con PrivilegesRequired=admin, {autopf} resuelve a C:\Program Files.
DefaultDirName={autopf}\Generador de Anexos
DefaultGroupName={#MiNombre}
AllowNoIcons=yes
DisableProgramGroupPage=auto
DisableDirPage=yes
DisableWelcomePage=no

; Entrada en Configuracion > Aplicaciones (y en Panel de control).
UninstallDisplayName={#MiNombre} {#MiVersion}
UninstallDisplayIcon={app}\{#MiEjecutable}

; Salida
OutputDir=salida
OutputBaseFilename=GeneradorAnexos-{#MiVersion}-Setup
SetupIconFile={#MiIcono}
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=2

; Apariencia moderna del asistente
WizardStyle=modern
WizardSizePercent=110
ShowLanguageDialog=no

; Plataforma: la aplicacion es de 64 bits y requiere Windows 10 2004 o superior.
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.19041

; ─────────────────────── Permisos e instalacion ───────────────────────
; La instalacion es para todo el equipo y requiere permisos de administrador,
; que es el modelo estandar de una aplicacion de escritorio de Windows:
;
;   - El programa queda en C:\Program Files\Generador de Anexos
;   - Esa carpeta hereda la ACL de Program Files: los administradores pueden
;     escribir, los usuarios normales solo leer y ejecutar.
;   - Un usuario o un proceso sin privilegios NO puede sustituir el ejecutable
;     ni ninguna DLL de la aplicacion.
;
; DELIBERADAMENTE no se usa "permissions: users-modify" sobre {app}. Aflojar
; la ACL para que el actualizador escriba sin UAC es un fallo de seguridad
; clasico: convierte la carpeta del programa en un punto de escalada de
; privilegios. La actualizacion pide elevacion cuando la necesita.
;
; Los datos del usuario (registros, respaldos, preferencias y plantillas
; actualizadas) viven en %LOCALAPPDATA%, escribibles sin elevacion, de modo que
; el uso diario nunca pide permisos de administrador.
PrivilegesRequired=admin

; Si el programa esta abierto, Inno pide cerrarlo en lugar de fallar. El mutex
; lo publica la propia aplicacion al arrancar (App.xaml.cs), de modo que tanto
; la instalacion como la desinstalacion detectan que esta en uso y evitan
; dejar archivos bloqueados -y por tanto carpetas huerfanas- en el equipo.
CloseApplications=yes
CloseApplicationsFilter={#MiEjecutable}
RestartApplications=no
AppMutex=GeneradorAnexos.MPO.OTI

; Advertir si la carpeta fija ya existe para hacer visibles restos o archivos
; inesperados antes de continuar.
DirExistsWarning=yes

; Texto informativo que ve el usuario antes de instalar.
; Se usa la version con formato (RTF). Si prefiere texto plano, cambie la
; linea siguiente por:  InfoBeforeFile=informacion.txt
AppComments=Generador de Terminos de Referencia y Anexos N 06 al 09
InfoBeforeFile=informacion.rtf

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; \
    Description: "Crear un acceso directo en el &Escritorio"; \
    GroupDescription: "Accesos directos adicionales:"; \
    Flags: unchecked

[Files]
; Toda la carpeta publicada: ejecutable, runtime de .NET, Windows App SDK,
; recursos WinUI (.pri, .xbf), Assets y la subcarpeta plantillas.
Source: "{#MiCarpetaOrigen}\*"; DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

#ifdef IncluyeVcRedist
Source: "{#VcRedistArchivo}"; DestDir: "{tmp}"; Flags: deleteafterinstall
#endif

[Icons]
Name: "{autoprograms}\{#MiNombre}"; Filename: "{app}\{#MiEjecutable}"; \
    Comment: "Generar TDR y Anexos N 06 al 09"
Name: "{autodesktop}\{#MiNombre}"; Filename: "{app}\{#MiEjecutable}"; \
    Comment: "Generar TDR y Anexos N 06 al 09"; Tasks: desktopicon

[Run]
#ifdef IncluyeVcRedist
Filename: "{tmp}\VC_redist.x64.exe"; \
    Parameters: "/install /quiet /norestart"; \
    StatusMsg: "Instalando componentes de Microsoft Visual C++..."; \
    Check: FaltaVcRedist; Flags: waituntilterminated
#endif
Filename: "{app}\{#MiEjecutable}"; \
    Description: "Iniciar {#MiNombre} al terminar"; \
    Flags: nowait postinstall skipifsilent

; Cuando el propio programa se actualiza, el instalador corre en modo
; silencioso y las entradas "postinstall" no se ejecutan. Esta lo relanza para
; que el usuario recupere la aplicacion abierta al terminar la actualizacion.
Filename: "{app}\{#MiEjecutable}"; \
    Flags: nowait; Check: InstalacionSilenciosa

[Code]
{ ------------------------------------------------------------------------
  Comprobacion del redistribuible de Visual C++ 2015-2022 (x64), necesario
  para el tiempo de ejecucion autocontenido del Windows App SDK.
  ------------------------------------------------------------------------ }
function VcRedistInstalado: Boolean;
var
  Instalado: Cardinal;
begin
  Result := False;

  if RegQueryDWordValue(HKLM32,
       'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64',
       'Installed', Instalado) then
    Result := Instalado = 1;

  if (not Result) and RegQueryDWordValue(HKLM64,
       'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64',
       'Installed', Instalado) then
    Result := Instalado = 1;

end;

function FaltaVcRedist: Boolean;
begin
  Result := not VcRedistInstalado;
end;

function InitializeSetup: Boolean;
begin
  Result := True;

#ifndef IncluyeVcRedist
  if not VcRedistInstalado then
  begin
    MsgBox(
      'Este equipo no tiene instalado el paquete redistribuible de' + #13#10 +
      'Microsoft Visual C++ 2015-2022 (x64), que el programa necesita' + #13#10 +
      'para ejecutarse. La instalacion se cancelara para evitar dejar' + #13#10 +
      'un programa que no pueda iniciar.' + #13#10 + #13#10 +
      'Instale el componente oficial de Microsoft o coloque' + #13#10 +
      'VC_redist.x64.exe en instalador\redist y vuelva a generar el Setup.',
      mbError, MB_OK);
    Result := False;
  end;
#endif
end;

{ Cierto cuando el instalador se ejecuta sin interfaz, que es como lo lanza el
  actualizador automatico de la propia aplicacion. }
function InstalacionSilenciosa: Boolean;
begin
  Result := WizardSilent;
end;

{ Los datos del usuario nunca se borran desde el desinstalador elevado. Esto
  evita eliminar el perfil equivocado. Se gestionan desde Configuracion con la
  aplicación abierta bajo la cuenta propietaria. }
