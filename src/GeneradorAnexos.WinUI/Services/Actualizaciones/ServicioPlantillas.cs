using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
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
    private const int MaximoEntradasPaquete = 100;
    private const int MaximoEntradasDocx = 2000;
    private const long MaximoExtraidoPaquete = 60L * 1024 * 1024;
    private const long MaximoExtraidoDocx = 50L * 1024 * 1024;
    private const long MaximoArchivoIndividual = 20L * 1024 * 1024;
    private const int MaximaRelacionCompresion = 100;
    /// <summary>Plantillas que debe contener todo paquete válido.</summary>
    private static readonly string[] Obligatorias =
    {
        "plantilla_anexos.docx",
        "plantilla_tdr.docx",
    };
    private static readonly HashSet<string> ArchivosPermitidosPaquete = new(
        Obligatorias.Concat(new[] { "catalogos.json", "version.txt" }),
        StringComparer.OrdinalIgnoreCase);

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
        RutasPlantillas.CarpetaPreferida = ConjuntoValido(Carpeta) ? Carpeta : null;
        Registro.Info("TEMPLATES_SOURCE_" + (RutasPlantillas.CarpetaPreferida is null ? "BUNDLED" : "USER"));
    }

    /// <summary>
    /// Versión de plantillas instalada: la descargada si existe, y si no la que
    /// vino con el instalador.
    /// </summary>
    public static VersionSemantica VersionInstalada()
    {
        if (ConjuntoValido(Carpeta) &&
            VersionSemantica.TryParse(LeerTexto(ArchivoVersion), out var usuario))
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
        var origen = ConjuntoValido(Carpeta) ? "actualizadas" : "incluidas con el programa";
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
        var sufijo = Guid.NewGuid().ToString("N");
        var temporal = Carpeta + ".nuevo-" + sufijo;
        var anterior = Carpeta + ".anterior-" + sufijo;
        var carpetaApartada = false;

        try
        {
            if (!ValidarPaqueteZip(rutaPaquete))
            {
                Registro.Advertencia("TEMPLATES_PACKAGE_REJECTED");
                return false;
            }

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


            if (!ConjuntoValido(temporal))
            {
                Registro.Advertencia("TEMPLATES_CONTENT_INVALID");
                return false;
            }

            await File.WriteAllTextAsync(
                Path.Combine(temporal, "version.txt"),
                version.ToString(),
                cancelacion).ConfigureAwait(false);

            // Sustitución: primero se aparta la carpeta buena, después se
            // promueve la nueva. Si el segundo paso fallara, se restaura.
            if (Directory.Exists(Carpeta))
            {
                Directory.Move(Carpeta, anterior);
                carpetaApartada = true;
            }

            try
            {
                Directory.Move(temporal, Carpeta);
            }
            catch
            {
                if (Directory.Exists(anterior) && !Directory.Exists(Carpeta))
                {
                    Directory.Move(anterior, Carpeta);
                    carpetaApartada = false;
                }

                throw;
            }

            BorrarCarpeta(anterior);
            carpetaApartada = false;
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
            if (carpetaApartada && Directory.Exists(anterior) && !Directory.Exists(Carpeta))
            {
                try
                {
                    Directory.Move(anterior, Carpeta);
                    carpetaApartada = false;
                }
                catch (Exception restauracion)
                {
                    Registro.Critico("TEMPLATES_ROLLBACK_FAILED", restauracion);
                }
            }

            return false;
        }
        finally
        {
            BorrarCarpeta(temporal);
            if (!carpetaApartada)
            {
                BorrarCarpeta(anterior);
            }

            IntentarEliminarArchivo(rutaPaquete);
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
            if (!BorrarCarpeta(Carpeta) || Directory.Exists(Carpeta))
            {
                return false;
            }

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

    private static bool BorrarCarpeta(string ruta)
    {
        try
        {
            var raiz = Path.GetFullPath(PreferenciasUi.RutaCarpeta)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var objetivo = Path.GetFullPath(ruta);
            if (!objetivo.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    objetivo.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    raiz.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("La carpeta no pertenece al directorio de datos.");
            }

            if (Directory.Exists(ruta))
            {
                var atributos = File.GetAttributes(ruta);
                Directory.Delete(ruta, recursive: (atributos & FileAttributes.ReparsePoint) == 0);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool ValidarPaqueteZip(string ruta)
    {
        try
        {
            var info = new FileInfo(ruta);
            if (!info.Exists || info.Length <= 0 ||
                info.Length > ConfiguracionActualizaciones.TamanoMaximoPlantillas)
            {
                return false;
            }

            using var archivo = ZipFile.OpenRead(ruta);
            if (!ArchivoComprimidoAcotado(
                    archivo, MaximoEntradasPaquete, MaximoExtraidoPaquete))
            {
                return false;
            }

            var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entrada in archivo.Entries)
            {
                var nombre = entrada.FullName.Replace('\\', '/');
                if (nombre.Contains('/') ||
                    !ArchivosPermitidosPaquete.Contains(nombre) ||
                    !nombres.Add(nombre))
                {
                    return false;
                }
            }

            return Obligatorias.All(nombres.Contains);
        }
        catch (Exception excepcion) when (excepcion is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ConjuntoValido(string carpeta)
    {
        try
        {
            if (!Directory.Exists(carpeta))
            {
                return false;
            }

            foreach (var nombre in Obligatorias)
            {
                if (!DocxSeguro(Path.Combine(carpeta, nombre)))
                {
                    return false;
                }
            }

            var catalogos = Path.Combine(carpeta, "catalogos.json");
            if (File.Exists(catalogos))
            {
                var info = new FileInfo(catalogos);
                if (info.Length <= 0 || info.Length > 1024 * 1024)
                {
                    return false;
                }

                using var json = JsonDocument.Parse(File.ReadAllBytes(catalogos));
                if (json.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or InvalidDataException or JsonException or
            XmlException or OpenXmlPackageException)
        {
            return false;
        }
    }

    private static bool DocxSeguro(string ruta)
    {
        var info = new FileInfo(ruta);
        if (!info.Exists || info.Length <= 0 || info.Length > MaximoArchivoIndividual)
        {
            return false;
        }

        using (var zip = ZipFile.OpenRead(ruta))
        {
            if (!ArchivoComprimidoAcotado(zip, MaximoEntradasDocx, MaximoExtraidoDocx))
            {
                return false;
            }

            var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entrada in zip.Entries)
            {
                var nombre = entrada.FullName.Replace('\\', '/');
                if (!nombres.Add(nombre) ||
                    nombre.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) ||
                    nombre.Contains("/activeX/", StringComparison.OrdinalIgnoreCase) ||
                    nombre.Contains("/embeddings/", StringComparison.OrdinalIgnoreCase) ||
                    nombre.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (nombre.EndsWith(".rels", StringComparison.OrdinalIgnoreCase) &&
                    !RelacionesExternasPermitidas(entrada))
                {
                    return false;
                }
            }
        }

        using var documento = WordprocessingDocument.Open(ruta, isEditable: false);
        return documento.DocumentType != WordprocessingDocumentType.MacroEnabledDocument &&
               documento.MainDocumentPart?.Document?.Body is not null;
    }

    private static bool ArchivoComprimidoAcotado(
        ZipArchive zip,
        int maximoEntradas,
        long maximoExtraido)
    {
        if (zip.Entries.Count == 0 || zip.Entries.Count > maximoEntradas)
        {
            return false;
        }

        long total = 0;
        foreach (var entrada in zip.Entries)
        {
            if (entrada.Length < 0 || entrada.Length > MaximoArchivoIndividual)
            {
                return false;
            }

            total = checked(total + entrada.Length);
            if (total > maximoExtraido ||
                (entrada.Length > 0 && entrada.CompressedLength <= 0) ||
                (entrada.CompressedLength > 0 &&
                 (double)entrada.Length / entrada.CompressedLength > MaximaRelacionCompresion))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RelacionesExternasPermitidas(ZipArchiveEntry entrada)
    {
        using var flujo = entrada.Open();
        using var lector = XmlReader.Create(flujo, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 1024 * 1024,
        });
        var xml = XDocument.Load(lector, LoadOptions.None);

        foreach (var relacion in xml.Descendants()
                     .Where(x => string.Equals(
                         (string?)x.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase)))
        {
            var destino = (string?)relacion.Attribute("Target");
            if (!Uri.TryCreate(destino, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(uri.Host, "denuncias.servicios.gob.pe", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static void IntentarEliminarArchivo(string ruta)
    {
        try
        {
            var raiz = Path.GetFullPath(ServicioActualizaciones.CarpetaDescargas)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var objetivo = Path.GetFullPath(ruta);
            if (!objetivo.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(objetivo), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (File.Exists(objetivo))
            {
                File.Delete(objetivo);
            }
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException)
        {
            Registro.Advertencia("TEMPLATES_PACKAGE_CLEANUP_FAILED");
        }
    }
}
