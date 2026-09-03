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
#define MiVersion       "1.0.1"
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
#if Pos(MiVersion, VersionEjecutable) != 1
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

; Carpeta propuesta; el asistente permite cambiarla.
; Con PrivilegesRequired=admin, {autopf} resuelve a C:\Program Files.
DefaultDirName={autopf}\Generador de Anexos
DefaultGroupName={#MiNombre}
AllowNoIcons=yes
DisableProgramGroupPage=auto
DisableDirPage=no
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
CloseApplicationsFilter=*.exe,*.dll,*.docx
RestartApplications=no
AppMutex=GeneradorAnexos.MPO.OTI

; No advertir si la carpeta de destino ya existe: el programa siempre se
; instala en una carpeta propia, y esa advertencia solo confundia al usuario
; cuando quedaban restos de una version anterior.
DirExistsWarning=no

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

[UninstallDelete]
; Elimina cualquier resto que haya quedado en la carpeta del programa: archivos
; que el usuario haya copiado dentro, ficheros bloqueados en un intento previo o
; subcarpetas vacias. Sin esto, la carpeta podia sobrevivir a la desinstalacion
; y la siguiente instalacion la encontraba ocupada.
; La comprobacion evita borrar de mas si alguien eligio como destino una carpeta
; compartida del sistema en lugar de una carpeta propia del programa.
Type: filesandordirs; Name: "{app}"; Check: CarpetaSeguraParaBorrar

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

  { Respaldo: si la biblioteca ya esta en el sistema, se da por valido. }
  if not Result then
    Result := FileExists(ExpandConstant('{sys}\vcruntime140_1.dll'));
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
    Result := MsgBox(
      'Este equipo no tiene instalado el paquete redistribuible de' + #13#10 +
      'Microsoft Visual C++ 2015-2022 (x64), que el programa necesita' + #13#10 +
      'para ejecutarse.' + #13#10 + #13#10 +
      'Puede continuar con la instalacion, pero si el programa no abre,' + #13#10 +
      'instale primero ese paquete desde el sitio de Microsoft.' + #13#10 + #13#10 +
      'Desea continuar de todos modos?',
      mbConfirmation, MB_YESNO) = IDYES;
  end;
#endif
end;

{ ------------------------------------------------------------------------
  Salvaguarda del borrado de la carpeta de instalacion: nunca se vacia una
  raiz de unidad ni una carpeta compartida del sistema, solo una carpeta
  propia del programa.
  ------------------------------------------------------------------------ }
{ Cierto cuando el instalador se ejecuta sin interfaz, que es como lo lanza el
  actualizador automatico de la propia aplicacion. }
function InstalacionSilenciosa: Boolean;
begin
  Result := WizardSilent;
end;

function CarpetaSeguraParaBorrar: Boolean;
var
  Ruta: String;
begin
  Ruta := RemoveBackslash(ExpandConstant('{app}'));

  Result := (Length(Ruta) > 3)
    and (CompareText(Ruta, RemoveBackslash(ExpandConstant('{autopf}'))) <> 0)
    and (CompareText(Ruta, RemoveBackslash(ExpandConstant('{autopf32}'))) <> 0)
    and (CompareText(Ruta, RemoveBackslash(ExpandConstant('{localappdata}'))) <> 0)
    and (CompareText(Ruta, RemoveBackslash(ExpandConstant('{userappdata}'))) <> 0)
    and (CompareText(Ruta, RemoveBackslash(ExpandConstant('{userdocs}'))) <> 0)
    and (CompareText(Ruta, RemoveBackslash(ExpandConstant('{userdesktop}'))) <> 0)
    and (CompareText(Ruta, RemoveBackslash(ExpandConstant('{win}'))) <> 0)
    and (CompareText(Ruta, RemoveBackslash(ExpandConstant('{sys}'))) <> 0);
end;

{ Al desinstalar, se ofrece conservar o borrar los datos del usuario
  (registros guardados, respaldos y preferencias). Por defecto se conservan. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  CarpetaDatos: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    CarpetaDatos := ExpandConstant('{localappdata}\GeneradorAnexos');
    if DirExists(CarpetaDatos) then
    begin
      if MsgBox(
           'Desea eliminar tambien los registros guardados, los respaldos' + #13#10 +
           'y las preferencias de este usuario?' + #13#10 + #13#10 +
           'Si elige No, los datos se conservan por si vuelve a instalar' + #13#10 +
           'el programa.',
           mbConfirmation, MB_YESNO) = IDYES then
        DelTree(CarpetaDatos, True, True, True);
    end;
  end;
end;
