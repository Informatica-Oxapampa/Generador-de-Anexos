using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using GeneradorAnexos.Infrastructure.Windows.Documents;

namespace GeneradorAnexos.WinUI.Services.Actualizaciones;

/// <summary>
/// Canal de actualización independiente para las plantillas de Word.
/// </summary>
/// <remarks>
/// <b>Por qué existe separado del programa.</b> Corregir una redacción del
/// Anexo N.° 06 no debería obligar a descargar un instalador de 240 MB ni a
/// reinstalar nada. Con este canal, una corrección de plantilla son unos pocos
/// kilobytes.
///
/// <b>Dónde se instalan.</b> En la carpeta de datos del usuario
/// (<c>%LOCALAPPDATA%\GeneradorAnexos\plantillas</c>) y no junto al ejecutable.
/// Dos razones: la instalación puede estar en Archivos de programa, donde un
/// usuario sin permisos de administrador no puede escribir; y así una
/// reinstalación del programa nunca pisa unas plantillas más recientes.
///
/// <b>Qué plantilla se usa.</b> <see cref="RutasPlantillas"/> mira primero la
/// carpeta del usuario y, si no hay nada, usa las que trajo el instalador. Si
/// una actualización de plantillas saliera defectuosa, basta con borrar esa
/// carpeta para volver a las originales.
///
/// <b>Instalación atómica.</b> El paquete se extrae a una carpeta temporal, se
/// comprueba que contenga las dos plantillas y solo entonces se sustituye la
/// carpeta buena. Un corte a mitad de proceso no deja plantillas incompletas.
/// </remarks>
public static class ServicioPlantillas
{
    /// <summary>Plantillas que debe contener todo paquete válido.</summary>
    private static readonly string[] Obligatorias =
    {
        "plantilla_anexos.docx",
        "plantilla_tdr.docx",
    };

    /// <summary>Carpeta de plantillas administradas por el usuario.</summary>
    public static string Carpeta { get; } = Path.Combine(
        PreferenciasUi.RutaCarpeta, "plantillas");

    /// <summary>Archivo que guarda la versión de las plantillas instaladas.</summary>
    private static string ArchivoVersion => Path.Combine(Carpeta, "version.txt");

    /// <summary>
    /// Conecta la resolución de plantillas con la carpeta del usuario. Se llama
    /// una vez al arrancar, antes de generar ningún documento.
    /// </summary>
    public static void Inicializar()
    {
        RutasPlantillas.CarpetaPreferida = Directory.Exists(Carpeta) ? Carpeta : null;
        Registro.Info("TEMPLATES_SOURCE_" + (RutasPlantillas.CarpetaPreferida is null ? "BUNDLED" : "USER"));
    }

    /// <summary>
    /// Versión de plantillas instalada: la descargada si existe, y si no la que
    /// vino con el instalador.
    /// </summary>
    public static VersionSemantica VersionInstalada()
    {
        if (VersionSemantica.TryParse(LeerTexto(ArchivoVersion), out var usuario))
        {
            return usuario;
        }

        var incluida = Path.Combine(AppContext.BaseDirectory, "plantillas", "version.txt");
        return VersionSemantica.TryParse(LeerTexto(incluida), out var base_)
            ? base_
            : new VersionSemantica(0, 0, 0);
    }

    /// <summary>Descripción de la versión instalada, para Configuración.</summary>
    public static string VersionInstaladaTexto()
    {
        var version = VersionInstalada();
        var origen = Directory.Exists(Carpeta) ? "actualizadas" : "incluidas con el programa";
        return $"v{version} ({origen})";
    }

    /// <summary>
    /// Instala un paquete de plantillas ya descargado y verificado.
    /// </summary>
    /// <returns>True si las plantillas quedaron instaladas y utilizables.</returns>
    public static async Task<bool> InstalarAsync(
        string rutaPaquete,
        VersionSemantica version,
        CancellationToken cancelacion)
    {
        var temporal = Carpeta + ".nuevo";
        var anterior = Carpeta + ".anterior";

        try
        {
            BorrarCarpeta(temporal);
            Directory.CreateDirectory(temporal);

            // ExtractToDirectory rechaza por sí solo las entradas cuya ruta
            // saldría de la carpeta de destino (por ejemplo «..\..\algo.exe»),
            // así que un paquete manipulado no puede escribir fuera de aquí.
            // Se extrae en un hilo aparte para no congelar la interfaz.
            await Task.Run(
                () => ZipFile.ExtractToDirectory(rutaPaquete, temporal, overwriteFiles: true),
                cancelacion).ConfigureAwait(false);

            // El paquete debe traer las dos plantillas. Si falta alguna, se
            // descarta entero: es preferible seguir con las anteriores.
            foreach (var nombre in Obligatorias)
            {
                if (!File.Exists(Path.Combine(temporal, nombre)))
                {
                    Registro.Advertencia("TEMPLATES_PACKAGE_INCOMPLETE");
                    BorrarCarpeta(temporal);
                    return false;
                }
            }

            await File.WriteAllTextAsync(
                Path.Combine(temporal, "version.txt"),
                version.ToString(),
                cancelacion).ConfigureAwait(false);

            // Sustitución: primero se aparta la carpeta buena, después se
            // promueve la nueva. Si el segundo paso fallara, se restaura.
            BorrarCarpeta(anterior);

            if (Directory.Exists(Carpeta))
            {
                Directory.Move(Carpeta, anterior);
            }

            try
            {
                Directory.Move(temporal, Carpeta);
            }
            catch (IOException)
            {
                if (Directory.Exists(anterior) && !Directory.Exists(Carpeta))
                {
                    Directory.Move(anterior, Carpeta);
                }

                throw;
            }

            BorrarCarpeta(anterior);
            Inicializar();

            // El paquete puede traer catálogos nuevos (áreas usuarias, bancos):
            // se releen para que estén disponibles sin reiniciar el programa.
            ServicioCatalogos.Recargar();

            Registro.Info("TEMPLATES_INSTALLED");
            return true;
        }
        catch (Exception excepcion)
        {
            Registro.Error("TEMPLATES_INSTALL_FAILED", excepcion);
            BorrarCarpeta(temporal);
            return false;
        }
    }

    /// <summary>
    /// Vuelve a las plantillas que trajo el instalador, borrando las
    /// descargadas. Es la salida si una actualización de plantillas sale mal.
    /// </summary>
    public static bool RestaurarIncluidas()
    {
        try
        {
            BorrarCarpeta(Carpeta);
            Inicializar();
            ServicioCatalogos.Recargar();
            Registro.Info("TEMPLATES_RESTORED");
            return true;
        }
        catch (Exception excepcion)
        {
            Registro.Error("TEMPLATES_RESTORE_FAILED", excepcion);
            return false;
        }
    }

    private static string LeerTexto(string ruta)
    {
        try
        {
            return File.Exists(ruta) ? File.ReadAllText(ruta).Trim() : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static void BorrarCarpeta(string ruta)
    {
        try
        {
            if (Directory.Exists(ruta))
            {
                Directory.Delete(ruta, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
