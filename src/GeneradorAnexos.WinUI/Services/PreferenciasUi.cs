using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GeneradorAnexos.WinUI.Services;

/// <summary>
/// Preferencias del usuario, guardadas como JSON en su carpeta de datos.
/// </summary>
/// <remarks>
/// Dos garantías importantes:
///
/// <para><b>Escritura atómica.</b> El archivo se escribe primero en un temporal
/// y después se reemplaza de una sola operación. Un corte de luz a mitad de
/// guardado no puede dejar un archivo truncado o ilegible.</para>
///
/// <para><b>Combinación antes de escribir.</b> Varias partes de la aplicación
/// crean su propia instancia. Antes de guardar se relee el archivo y solo se
/// cambia la clave afectada, de modo que una instancia no pisa las
/// preferencias que otra acaba de escribir.</para>
/// </remarks>
public sealed class PreferenciasUi
{
    private static readonly object BloqueoArchivo = new();

    /// <summary>Tema: seguir a Windows.</summary>
    public const string TemaSistema = "sistema";

    /// <summary>Tema: claro fijo.</summary>
    public const string TemaClaro = "claro";

    /// <summary>Tema: oscuro fijo.</summary>
    public const string TemaOscuro = "oscuro";

    private readonly string _ruta;
    private JsonObject _datos;

    public PreferenciasUi()
    {
        _ruta = Path.Combine(RutaCarpeta, "preferencias.json");
        try
        {
            Directory.CreateDirectory(RutaCarpeta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Las preferencias no son imprescindibles para abrir el programa.
            // Se usan valores seguros en memoria y se deja constancia sin
            // mostrar rutas del perfil del usuario.
            Registro.Advertencia("PREFS_DIRECTORY_UNAVAILABLE");
        }

        lock (BloqueoArchivo)
        {
            _datos = Cargar();
        }
    }

    /// <summary>Carpeta de datos del usuario para esta aplicación.</summary>
    public static string RutaCarpeta { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GeneradorAnexos");

    // ─────────────────────────── Apariencia ───────────────────────────

    /// <summary>
    /// Tema elegido: <see cref="TemaSistema"/>, <see cref="TemaClaro"/> u
    /// <see cref="TemaOscuro"/>. Por omisión sigue a Windows.
    /// </summary>
    public string Tema
    {
        get
        {
            var valor = LeerTexto("tema");
            return valor is TemaClaro or TemaOscuro or TemaSistema ? valor : TemaSistema;
        }
        set => Escribir("tema", value);
    }

    /// <summary>Preferencia de accesibilidad: texto grande.</summary>
    public bool TextoGrande
    {
        get => LeerBooleano("texto_grande");
        set => Escribir("texto_grande", value);
    }

    // ─────────────────────────── Actualizaciones ───────────────────────────

    /// <summary>Comprobar actualizaciones al iniciar. Activado por omisión.</summary>
    public bool BuscarActualizaciones
    {
        get => LeerBooleano("buscar_actualizaciones", predeterminado: true);
        set => Escribir("buscar_actualizaciones", value);
    }

    /// <summary>Versión que el usuario pidió no volver a ver.</summary>
    public string VersionOmitida
    {
        get => LeerTexto("version_omitida");
        set => Escribir("version_omitida", value);
    }

    /// <summary>
    /// Versión más alta que se ha visto publicada alguna vez.
    /// </summary>
    /// <remarks>
    /// Impide el retroceso de versión: si alguien reenviara un manifiesto
    /// antiguo —autentico, pero de una versión con un fallo ya corregido— se
    /// descarta por ser inferior a esta.
    /// </remarks>
    public string VersionMasAltaVista
    {
        get => LeerTexto("version_mas_alta_vista");
        set => Escribir("version_mas_alta_vista", value);
    }

    /// <summary>Fecha y hora de la última comprobación (ISO 8601), o vacío.</summary>
    public string UltimaComprobacion
    {
        get => LeerTexto("ultima_comprobacion");
        set => Escribir("ultima_comprobacion", value);
    }

    /// <summary>Registra que se acaba de comprobar, con la hora local actual.</summary>
    public void MarcarComprobacion()
        => UltimaComprobacion = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>Última comprobación en formato legible, o texto por defecto.</summary>
    public string UltimaComprobacionLegible()
    {
        if (DateTime.TryParse(
                UltimaComprobacion,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var momento))
        {
            return momento.ToString("dd/MM/yyyy · HH:mm", CultureInfo.CurrentCulture);
        }

        return "Nunca";
    }

    // ─────────────────────────── Carpetas recordadas ───────────────────────────

    /// <summary>
    /// Última impresora elegida. En una oficina se imprime casi siempre en la
    /// misma bandeja, así que recordarla ahorra un clic en cada documento.
    /// </summary>
    public string UltimaImpresora
    {
        get => LeerTexto("ultima_impresora");
        set => Escribir("ultima_impresora", value);
    }

    /// <summary>Última carpeta usada al guardar documentos.</summary>
    public string UltimaCarpeta
    {
        get => LeerTexto("ultima_carpeta");
        set => Escribir("ultima_carpeta", value);
    }

    /// <summary>Última carpeta usada al cargar un Pedido SIGA.</summary>
    public string UltimaCarpetaPedido
    {
        get => LeerTexto("ultima_carpeta_pedido");
        set => Escribir("ultima_carpeta_pedido", value);
    }

    /// <summary>Carpeta destino: la recordada si existe, o Documentos.</summary>
    public string CarpetaGuardar()
    {
        var ruta = UltimaCarpeta;
        return !string.IsNullOrEmpty(ruta) && Directory.Exists(ruta)
            ? ruta
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    public void RecordarCarpeta(string rutaArchivo)
    {
        var carpeta = Path.GetDirectoryName(rutaArchivo);
        if (!string.IsNullOrEmpty(carpeta))
        {
            UltimaCarpeta = carpeta;
        }
    }

    // ─────────────────────────── Mantenimiento ───────────────────────────

    /// <summary>
    /// Devuelve las preferencias a sus valores de fábrica. No toca los
    /// registros guardados, los respaldos ni los documentos del usuario.
    /// </summary>
    public void Restablecer()
    {
        lock (BloqueoArchivo)
        {
            _datos = new JsonObject();
            Guardar();
        }

        Registro.Info("PREFS_RESET");
    }

    // ─────────────────────────── Interno ───────────────────────────

    private bool LeerBooleano(string clave, bool predeterminado = false)
    {
        try
        {
            return _datos[clave]?.GetValue<bool>() ?? predeterminado;
        }
        catch (FormatException)
        {
            return predeterminado;
        }
        catch (InvalidOperationException)
        {
            return predeterminado;
        }
    }

    private string LeerTexto(string clave)
    {
        try
        {
            return _datos[clave]?.GetValue<string>() ?? string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Relee el archivo, cambia solo la clave indicada y lo vuelve a guardar.
    /// </summary>
    private void Escribir(string clave, JsonNode? valor)
    {
        lock (BloqueoArchivo)
        {
            _datos = Cargar();
            _datos[clave] = valor;
            Guardar();
        }
    }

    private JsonObject Cargar()
    {
        try
        {
            if (File.Exists(_ruta))
            {
                return JsonNode.Parse(File.ReadAllText(_ruta)) as JsonObject ?? new JsonObject();
            }
        }
        catch (JsonException)
        {
            // Archivo corrupto: se parte de cero en lugar de impedir el arranque.
            Registro.Advertencia("PREFS_PARSE_FAILED");
            RespaldarCorrupto();
        }
        catch (IOException)
        {
            Registro.Advertencia("PREFS_READ_FAILED");
        }
        catch (UnauthorizedAccessException)
        {
            Registro.Advertencia("PREFS_READ_DENIED");
        }
        catch (SecurityException)
        {
            Registro.Advertencia("PREFS_READ_DENIED");
        }

        return new JsonObject();
    }

    /// <summary>
    /// Conserva una copia del archivo ilegible antes de sustituirlo, por si
    /// hiciera falta recuperar algo a mano.
    /// </summary>
    private void RespaldarCorrupto()
    {
        try
        {
            File.Copy(_ruta, _ruta + ".corrupto", overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (SecurityException)
        {
        }
    }

    private void Guardar()
    {
        var temporal = _ruta + ".tmp";

        try
        {
            File.WriteAllText(
                temporal,
                _datos.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            // Reemplazo atómico: o queda el archivo antiguo o el nuevo, nunca
            // uno a medio escribir.
            File.Move(temporal, _ruta, overwrite: true);
        }
        catch (IOException)
        {
            Registro.Advertencia("PREFS_WRITE_FAILED");
            DescartarTemporal(temporal);
        }
        catch (UnauthorizedAccessException)
        {
            Registro.Advertencia("PREFS_WRITE_DENIED");
            DescartarTemporal(temporal);
        }
        catch (SecurityException)
        {
            Registro.Advertencia("PREFS_WRITE_DENIED");
            DescartarTemporal(temporal);
        }
    }

    private static void DescartarTemporal(string ruta)
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
        catch (SecurityException)
        {
        }
    }
}
