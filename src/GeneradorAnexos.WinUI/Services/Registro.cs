using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace GeneradorAnexos.WinUI.Services;

/// <summary>
/// Registro de diagnóstico en la carpeta de datos del usuario.
/// </summary>
/// <remarks>
/// <para><b>Sin datos personales.</b> Se escriben códigos de evento, no
/// contenido: nunca nombres, DNI, RUC, montos, rutas de documentos del usuario
/// ni credenciales. Es la razón por la que todos los métodos reciben un código
/// y no un mensaje libre.</para>
///
/// <para><b>Rotación.</b> Al superar el tamaño máximo, el archivo activo pasa a
/// ser <c>.1</c>, el <c>.1</c> pasa a <c>.2</c> y el más antiguo se elimina. El
/// registro nunca puede crecer sin límite.</para>
///
/// <para><b>Nunca interrumpe.</b> Cualquier fallo al escribir se ignora en
/// silencio: un problema de disco no debe impedir trabajar.</para>
/// </remarks>
public static class Registro
{
    /// <summary>Tamaño máximo del archivo activo antes de rotar.</summary>
    private const long TamanoMaximo = 512 * 1024;

    /// <summary>Número de archivos históricos que se conservan.</summary>
    private const int HistoricosConservados = 3;

    private static readonly object Candado = new();
    private static string? _ruta;

    /// <summary>Carpeta donde se guardan los archivos de registro.</summary>
    public static string Carpeta { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GeneradorAnexos",
        "logs");

    /// <summary>Ruta del archivo activo, o vacío si aún no se configuró.</summary>
    public static string RutaArchivo => _ruta ?? string.Empty;

    public static void Configurar()
    {
        try
        {
            Directory.CreateDirectory(Carpeta);
            _ruta = Path.Combine(Carpeta, "generador_anexos.log");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static void Info(string codigo) => Escribir("INFO ", codigo, null);

    public static void Advertencia(string codigo) => Escribir("WARN ", codigo, null);

    public static void Error(string codigo, Exception? excepcion = null)
        => Escribir("ERROR", codigo, excepcion);

    /// <summary>
    /// Error del que la aplicación no puede recuperarse por sí sola. Se
    /// distingue del error normal para poder localizarlo rápido en el archivo.
    /// </summary>
    public static void Critico(string codigo, Exception? excepcion = null)
        => Escribir("FATAL", codigo, excepcion);

    /// <summary>
    /// Vacía el registro. Lo usa Configuración › Diagnóstico cuando el usuario
    /// quiere empezar de cero antes de reproducir un problema.
    /// </summary>
    public static bool Vaciar()
    {
        if (_ruta is null)
        {
            return false;
        }

        try
        {
            lock (Candado)
            {
                File.WriteAllText(_ruta, string.Empty);

                for (var i = 1; i <= HistoricosConservados; i++)
                {
                    var historico = _ruta + "." + i.ToString(CultureInfo.InvariantCulture);
                    if (File.Exists(historico))
                    {
                        File.Delete(historico);
                    }
                }
            }

            Info("LOG_CLEARED");
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
    }

    /// <summary>Tamaño total ocupado por el registro y su histórico, en bytes.</summary>
    public static long TamanoTotal()
    {
        if (_ruta is null)
        {
            return 0;
        }

        long total = 0;

        try
        {
            if (File.Exists(_ruta))
            {
                total += new FileInfo(_ruta).Length;
            }

            for (var i = 1; i <= HistoricosConservados; i++)
            {
                var historico = _ruta + "." + i.ToString(CultureInfo.InvariantCulture);
                if (File.Exists(historico))
                {
                    total += new FileInfo(historico).Length;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return total;
    }

    private static void Escribir(string nivel, string codigo, Exception? excepcion)
    {
        var linea = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {nivel} {codigo}");

        if (excepcion is not null)
        {
            linea += Environment.NewLine + excepcion;
        }

        Debug.WriteLine(linea);

        if (_ruta is null)
        {
            return;
        }

        try
        {
            lock (Candado)
            {
                RotarSiHaceFalta();
                File.AppendAllText(_ruta, linea + Environment.NewLine);
            }
        }
        catch (IOException)
        {
            // El registro nunca debe interrumpir al usuario.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Rota los archivos cuando el activo supera el tamaño máximo. Debe
    /// llamarse dentro del candado.
    /// </summary>
    private static void RotarSiHaceFalta()
    {
        if (_ruta is null || !File.Exists(_ruta))
        {
            return;
        }

        if (new FileInfo(_ruta).Length < TamanoMaximo)
        {
            return;
        }

        try
        {
            // El más antiguo se descarta.
            var masAntiguo = _ruta + "." + HistoricosConservados.ToString(CultureInfo.InvariantCulture);
            if (File.Exists(masAntiguo))
            {
                File.Delete(masAntiguo);
            }

            for (var i = HistoricosConservados - 1; i >= 1; i--)
            {
                var origen = _ruta + "." + i.ToString(CultureInfo.InvariantCulture);
                var destino = _ruta + "." + (i + 1).ToString(CultureInfo.InvariantCulture);

                if (File.Exists(origen))
                {
                    File.Move(origen, destino, overwrite: true);
                }
            }

            File.Move(_ruta, _ruta + ".1", overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
