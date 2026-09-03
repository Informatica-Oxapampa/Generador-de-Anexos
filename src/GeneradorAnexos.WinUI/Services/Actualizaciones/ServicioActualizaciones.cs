using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GeneradorAnexos.WinUI.Services.Actualizaciones;

/// <summary>
/// Comprobación, descarga y verificación de las actualizaciones publicadas en
/// GitHub Releases.
/// </summary>
/// <remarks>
/// <b>Solo mira Releases, nunca la rama de desarrollo.</b> El manifiesto se lee
/// de <c>releases/latest/download/update.json</c>, así que los commits del día a
/// día no llegan a los equipos: solo llega lo que se publica expresamente como
/// versión.
///
/// <b>Reglas de seguridad:</b>
/// <list type="number">
///   <item>Solo HTTPS y solo dominios de GitHub.</item>
///   <item>El nombre y la carpeta del archivo descargado los decide la
///         aplicación, nunca el manifiesto.</item>
///   <item>Antes de usar nada se comprueban el tamaño exacto y el SHA-256
///         declarados; si algo no coincide, el archivo se borra.</item>
///   <item>Ningún fallo de red deja restos.</item>
/// </list>
/// </remarks>
public static class ServicioActualizaciones
{
    private static readonly TimeSpan EsperaComprobacion = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan EsperaDescarga = TimeSpan.FromMinutes(30);

    private static readonly Lazy<HttpClient> Cliente = new(() =>
    {
        var cliente = new HttpClient { Timeout = EsperaDescarga };
        cliente.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"GeneradorAnexos/{Constantes.AppVersion}");
        return cliente;
    });

    /// <summary>Carpeta donde se descargan los paquetes.</summary>
    public static string CarpetaDescargas { get; } = Path.Combine(
        PreferenciasUi.RutaCarpeta, "actualizaciones");

    /// <summary>Versión del programa instalada.</summary>
    public static VersionSemantica VersionInstalada
        => VersionSemantica.TryParse(Constantes.AppVersion, out var version)
            ? version
            : new VersionSemantica(0, 0, 0);

    /// <summary>Página de versiones publicadas.</summary>
    public static string UrlVersiones => ConfiguracionActualizaciones.UrlVersiones;

    /// <summary>
    /// Descarga el manifiesto y compara las dos versiones. Nunca lanza
    /// excepciones: no poder comprobar no es un error que deba molestar al
    /// usuario, así que se comunica en el resultado.
    /// </summary>
    public static async Task<ResultadoComprobacion> ComprobarAsync(CancellationToken cancelacion)
    {
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
            limite.CancelAfter(EsperaComprobacion);

            var bytes = await Cliente.Value
                .GetByteArrayAsync(ConfiguracionActualizaciones.UrlManifiesto, limite.Token)
                .ConfigureAwait(false);

            var manifiesto = JsonSerializer.Deserialize<ManifiestoActualizacion>(bytes);

            if (manifiesto is null)
            {
                Registro.Advertencia("UPDATE_MANIFEST_EMPTY");
                return ResultadoComprobacion.Fallida("El archivo de versión llegó vacío.");
            }

            if (manifiesto.Formato > ConfiguracionActualizaciones.FormatoManifiesto)
            {
                // El manifiesto es de un formato posterior: esta versión no lo
                // entiende y no debe adivinar. Se pide actualizar a mano.
                Registro.Advertencia("UPDATE_MANIFEST_NEWER_FORMAT");
                return ResultadoComprobacion.Fallida(
                    "La versión publicada usa un formato más reciente. "
                    + "Descárguela manualmente desde la página de versiones.");
            }

            if (!EsManifiestoReciente(manifiesto))
            {
                return ResultadoComprobacion.Fallida(
                    "La información de versión recibida es anterior a la última "
                    + "conocida y se ha descartado.");
            }

            Registro.Info("UPDATE_CHECK_OK");
            return ResultadoComprobacion.Correcta(manifiesto);
        }
        catch (OperationCanceledException)
        {
            Registro.Advertencia("UPDATE_CHECK_TIMEOUT");
            return ResultadoComprobacion.Fallida("La comprobación tardó demasiado y se canceló.");
        }
        catch (HttpRequestException)
        {
            Registro.Advertencia("UPDATE_CHECK_NO_NETWORK");
            return ResultadoComprobacion.Fallida(
                "No se pudo conectar. Compruebe su conexión a Internet.");
        }
        catch (JsonException)
        {
            Registro.Advertencia("UPDATE_MANIFEST_PARSE_FAILED");
            return ResultadoComprobacion.Fallida("El archivo de versión no se pudo interpretar.");
        }
        catch (Exception excepcion)
        {
            Registro.Error("UPDATE_CHECK_FAILED", excepcion);
            return ResultadoComprobacion.Fallida("No se pudo comprobar si hay actualizaciones.");
        }
    }

    /// <summary>
    /// Rechaza manifiestos anteriores al último que se aceptó.
    /// </summary>
    /// <remarks>
    /// Defiende contra dos ataques que la firma por sí sola no cubre, porque el
    /// atacante estaría reenviando un manifiesto <i>autentico pero antiguo</i>:
    ///
    /// <list type="bullet">
    ///   <item><b>Congelación.</b> Quien controle la red puede servir siempre el
    ///   manifiesto de ayer para que el equipo nunca reciba una corrección de
    ///   seguridad.</item>
    ///   <item><b>Retroceso de versión.</b> Servir el manifiesto de una versión
    ///   anterior con un fallo conocido, para inducir una instalación
    ///   vulnerable.</item>
    /// </list>
    ///
    /// Se guarda la versión más alta vista y no se acepta ninguna inferior. Es
    /// la misma idea que el número de secuencia de TUF.
    /// </remarks>
    private static bool EsManifiestoReciente(ManifiestoActualizacion manifiesto)
    {
        if (manifiesto.App is not { } app || !VersionSemantica.TryParse(app.Version, out var publicada))
        {
            return true;
        }

        var preferencias = new PreferenciasUi();

        if (VersionSemantica.TryParse(preferencias.VersionMasAltaVista, out var maxima)
            && publicada < maxima)
        {
            Registro.Advertencia("UPDATE_ROLLBACK_REJECTED");
            return false;
        }

        if (publicada > maxima)
        {
            preferencias.VersionMasAltaVista = publicada.ToString();
        }

        return true;
    }

    /// <summary>
    /// Descarga un paquete y comprueba su integridad.
    /// </summary>
    /// <returns>
    /// Ruta del archivo verificado, o <c>null</c> si algo falló. En caso de
    /// fallo no queda ningún archivo a medias.
    /// </returns>
    public static async Task<string?> DescargarVerificadoAsync(
        PaqueteActualizacion paquete,
        string nombreDestino,
        IProgress<EstadoDescarga> progreso,
        CancellationToken cancelacion)
    {
        if (!PaqueteActualizacion.EsDescargaPermitida(paquete.Url))
        {
            Registro.Advertencia("UPDATE_URL_REJECTED");
            return null;
        }

        var destino = Path.Combine(CarpetaDescargas, NombreSeguro(nombreDestino));

        try
        {
            Directory.CreateDirectory(CarpetaDescargas);
            LimpiarDescargasAnteriores(destino);

            progreso.Report(EstadoDescarga.Etapa("Preparando actualización…"));

            using var respuesta = await Cliente.Value
                .GetAsync(paquete.Url, HttpCompletionOption.ResponseHeadersRead, cancelacion)
                .ConfigureAwait(false);

            respuesta.EnsureSuccessStatusCode();

            var total = respuesta.Content.Headers.ContentLength ?? paquete.Tamano;

            await using (var origen = await respuesta.Content
                             .ReadAsStreamAsync(cancelacion).ConfigureAwait(false))
            await using (var archivo = new FileStream(
                             destino, FileMode.Create, FileAccess.Write, FileShare.None,
                             bufferSize: 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long escrito = 0;
                int leido;

                while ((leido = await origen.ReadAsync(buffer, cancelacion).ConfigureAwait(false)) > 0)
                {
                    await archivo.WriteAsync(buffer.AsMemory(0, leido), cancelacion)
                        .ConfigureAwait(false);

                    escrito += leido;
                    progreso.Report(EstadoDescarga.Descargando(
                        total > 0 ? (double)escrito / total : 0, escrito, total));
                }
            }

            progreso.Report(EstadoDescarga.Etapa("Verificando archivo…"));

            if (!await VerificarAsync(destino, paquete, cancelacion).ConfigureAwait(false))
            {
                Descartar(destino);
                return null;
            }

            Registro.Info("UPDATE_DOWNLOAD_VERIFIED");
            return destino;
        }
        catch (OperationCanceledException)
        {
            Registro.Info("UPDATE_DOWNLOAD_CANCELLED");
            Descartar(destino);
            return null;
        }
        catch (HttpRequestException excepcion)
        {
            Registro.Error("UPDATE_DOWNLOAD_NETWORK_FAILED", excepcion);
            Descartar(destino);
            return null;
        }
        catch (IOException excepcion)
        {
            Registro.Error("UPDATE_DOWNLOAD_IO_FAILED", excepcion);
            Descartar(destino);
            return null;
        }
        catch (UnauthorizedAccessException excepcion)
        {
            Registro.Error("UPDATE_DOWNLOAD_DENIED", excepcion);
            Descartar(destino);
            return null;
        }
    }

    /// <summary>Comprueba tamaño y SHA-256 del archivo descargado.</summary>
    private static async Task<bool> VerificarAsync(
        string ruta,
        PaqueteActualizacion paquete,
        CancellationToken cancelacion)
    {
        try
        {
            if (paquete.Tamano > 0 && new FileInfo(ruta).Length != paquete.Tamano)
            {
                Registro.Advertencia("UPDATE_SIZE_MISMATCH");
                return false;
            }

            await using var archivo = new FileStream(
                ruta, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 81920, useAsync: true);

            using var algoritmo = SHA256.Create();
            var resumen = await algoritmo.ComputeHashAsync(archivo, cancelacion).ConfigureAwait(false);

            if (!string.Equals(
                    Convert.ToHexString(resumen),
                    paquete.Sha256.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                Registro.Advertencia("UPDATE_HASH_MISMATCH");
                return false;
            }

            return true;
        }
        catch (IOException excepcion)
        {
            Registro.Error("UPDATE_VERIFY_FAILED", excepcion);
            return false;
        }
        catch (UnauthorizedAccessException excepcion)
        {
            Registro.Error("UPDATE_VERIFY_DENIED", excepcion);
            return false;
        }
    }

    /// <summary>
    /// Lanza el instalador, comprobando su hash otra vez justo antes.
    /// </summary>
    /// <remarks>
    /// <b>Por qué se vuelve a verificar.</b> Entre la verificación de la
    /// descarga y el momento de ejecutar pasa tiempo: el usuario lee un
    /// diálogo, decide. Otro proceso del mismo usuario podría sustituir el
    /// archivo en esa ventana. Repetir la comprobación justo antes de lanzar
    /// cierra esa condición de carrera.
    ///
    /// <b>Sobre la elevación.</b> El instalador pide privilegios de
    /// administrador por su propio manifiesto, así que Windows muestra el aviso
    /// de UAC. La aplicación nunca se ejecuta elevada, ni instala ningún
    /// servicio en segundo plano con privilegios: eso convertiría al
    /// actualizador en una vía de escalada permanente. La elevación es puntual,
    /// visible y la autoriza el usuario.
    /// </remarks>
    public static async Task<ResultadoInstalacion> InstalarAsync(
        string rutaInstalador,
        PaqueteActualizacion paquete,
        CancellationToken cancelacion)
    {
        try
        {
            if (!File.Exists(rutaInstalador))
            {
                Registro.Advertencia("UPDATE_INSTALLER_MISSING");
                return ResultadoInstalacion.Fallo;
            }

            if (!await VerificarAsync(rutaInstalador, paquete, cancelacion).ConfigureAwait(false))
            {
                Registro.Advertencia("UPDATE_INSTALLER_TAMPERED");
                Descartar(rutaInstalador);
                return ResultadoInstalacion.Manipulado;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = rutaInstalador,
                Arguments = "/SILENT /NOCANCEL /NORESTART /SP-",
                UseShellExecute = true,
            });

            Registro.Info("UPDATE_INSTALLER_LAUNCHED");
            return ResultadoInstalacion.Lanzado;
        }
        catch (System.ComponentModel.Win32Exception excepcion)
            when (excepcion.NativeErrorCode == 1223)
        {
            // El usuario rechazó el aviso de UAC. No es un error del programa.
            Registro.Info("UPDATE_ELEVATION_DECLINED");
            return ResultadoInstalacion.ElevacionRechazada;
        }
        catch (Exception excepcion)
        {
            Registro.Error("UPDATE_INSTALLER_FAILED", excepcion);
            return ResultadoInstalacion.Fallo;
        }
    }

    private static string NombreSeguro(string nombre)
    {
        foreach (var caracter in Path.GetInvalidFileNameChars())
        {
            nombre = nombre.Replace(caracter, '_');
        }

        return string.IsNullOrWhiteSpace(nombre) ? "paquete.bin" : nombre;
    }

    /// <summary>Borra paquetes de intentos anteriores para no acumular disco.</summary>
    private static void LimpiarDescargasAnteriores(string excepto)
    {
        try
        {
            foreach (var archivo in Directory.EnumerateFiles(CarpetaDescargas))
            {
                if (!string.Equals(archivo, excepto, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(archivo);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void Descartar(string ruta)
    {
        try
        {
            if (File.Exists(ruta))
            {
                File.Delete(ruta);
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

/// <summary>Resultado de consultar el manifiesto.</summary>
public sealed class ResultadoComprobacion
{
    private ResultadoComprobacion(EstadoComprobacion estado) => Estado = estado;

    public EstadoComprobacion Estado { get; }

    /// <summary>Manifiesto descargado, solo si la comprobación tuvo éxito.</summary>
    public ManifiestoActualizacion? Manifiesto { get; private init; }

    /// <summary>Motivo comprensible del fallo, solo si falló.</summary>
    public string Mensaje { get; private init; } = string.Empty;

    public static ResultadoComprobacion Correcta(ManifiestoActualizacion manifiesto)
        => new(EstadoComprobacion.Correcta) { Manifiesto = manifiesto };

    public static ResultadoComprobacion Fallida(string mensaje)
        => new(EstadoComprobacion.Fallida) { Mensaje = mensaje };

    /// <summary>Paquete del programa si la versión publicada es posterior a la instalada.</summary>
    public PaqueteActualizacion? AppPendiente(out VersionSemantica version)
    {
        version = default;

        if (Manifiesto?.App is not { } app || !app.EsValido(out var publicada))
        {
            return null;
        }

        version = publicada;

        // Estrictamente mayor: si el equipo ya tiene esa version, no se vuelve
        // a descargar; y nunca se ofrece una anterior a la instalada.
        return publicada > ServicioActualizaciones.VersionInstalada ? app : null;
    }

    /// <summary>Paquete de plantillas si hay una versión posterior a la instalada.</summary>
    public PaqueteActualizacion? PlantillasPendientes(
        VersionSemantica instalada,
        out VersionSemantica version)
    {
        version = default;

        if (Manifiesto?.Plantillas is not { } plantillas || !plantillas.EsValido(out var publicada))
        {
            return null;
        }

        version = publicada;
        return publicada > instalada ? plantillas : null;
    }
}

/// <summary>Cómo terminó el intento de lanzar el instalador.</summary>
public enum ResultadoInstalacion
{
    /// <summary>El instalador arrancó; la aplicación debe cerrarse.</summary>
    Lanzado,

    /// <summary>El usuario no autorizó la elevación de privilegios.</summary>
    ElevacionRechazada,

    /// <summary>El archivo cambió después de verificarse: se descartó.</summary>
    Manipulado,

    /// <summary>Cualquier otro fallo.</summary>
    Fallo,
}

public enum EstadoComprobacion
{
    /// <summary>El manifiesto se leyó correctamente.</summary>
    Correcta,

    /// <summary>No se pudo comprobar (sin red, GitHub no responde, etc.).</summary>
    Fallida,
}

/// <summary>Estado de la descarga que se muestra en la ventana de progreso.</summary>
public readonly struct EstadoDescarga
{
    private EstadoDescarga(string texto, double fraccion, bool indeterminado)
    {
        Texto = texto;
        Fraccion = fraccion;
        Indeterminado = indeterminado;
    }

    /// <summary>Texto de la etapa actual.</summary>
    public string Texto { get; }

    /// <summary>Avance entre 0 y 1.</summary>
    public double Fraccion { get; }

    /// <summary>True mientras no se conoce el avance exacto.</summary>
    public bool Indeterminado { get; }

    public static EstadoDescarga Etapa(string texto) => new(texto, 0, indeterminado: true);

    public static EstadoDescarga Descargando(double fraccion, long escrito, long total)
    {
        var texto = total > 0
            ? string.Format(
                CultureInfo.CurrentCulture,
                "Descargando… {0:N1} MB de {1:N1} MB",
                escrito / 1024d / 1024d,
                total / 1024d / 1024d)
            : "Descargando…";

        return new EstadoDescarga(texto, Math.Clamp(fraccion, 0, 1), indeterminado: false);
    }
}
