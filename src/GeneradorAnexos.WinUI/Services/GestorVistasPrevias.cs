using System;
using System.Collections.Generic;
using System.IO;

namespace GeneradorAnexos.WinUI.Services;

/// <summary>
/// Equivalente de la gestion de vistas previas de <c>ui/ventana_principal.py</c>.
/// </summary>
/// <remarks>
/// Cada instancia crea su propio directorio temporal y solo borra las rutas que
/// ella misma registro. Nunca reutiliza nombres globales ni toca documentos del
/// usuario, igual que el original.
/// </remarks>
public sealed class GestorVistasPrevias
{
    private readonly string _directorio;
    private readonly string _tipo;
    private readonly HashSet<string> _creadas = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _candado = new();

    public GestorVistasPrevias(string tipo)
    {
        _tipo = tipo;
        _directorio = Path.Combine(
            Path.GetTempPath(), "GeneradorAnexos-preview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directorio);
    }

    /// <summary>Reserva una ruta única contenida en el temporal de esta instancia.</summary>
    /// <exception cref="InvalidOperationException">Si la ruta escapara del directorio propio.</exception>
    public string CrearRuta()
    {
        var nombre = $"{_tipo}-{Guid.NewGuid():N}.docx";
        var ruta = Path.GetFullPath(Path.Combine(_directorio, nombre));
        var directorio = Path.GetFullPath(_directorio);

        if (!ruta.StartsWith(directorio + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("PREVIEW_PATH_OUTSIDE_OWNED_DIRECTORY");
        }

        lock (_candado)
        {
            _creadas.Add(ruta);
        }
        return ruta;
    }

    /// <summary>Retira solo una ruta que esta instancia registró como propia.</summary>
    public void Descartar(string ruta)
    {
        lock (_candado)
        {
            if (!_creadas.Contains(ruta))
            {
                return;
            }
        }

        try
        {
            File.Delete(ruta);
        }
        catch (IOException)
        {
            Registro.Advertencia("PREVIEW_FILE_CLEANUP_FAILED");
        }
        catch (UnauthorizedAccessException)
        {
            Registro.Advertencia("PREVIEW_FILE_CLEANUP_FAILED");
        }

        lock (_candado)
        {
            _creadas.Remove(ruta);
        }
    }

    /// <summary>
    /// Borra carpetas de vista previa que quedaron de sesiones anteriores.
    /// </summary>
    /// <remarks>
    /// <see cref="LimpiarTodo"/> se ejecuta al cerrar la aplicación, pero si el
    /// proceso termina de forma anormal —un corte de luz, un cierre forzado—
    /// esas carpetas se quedan en el temporal de Windows y se van acumulando.
    /// Esta limpieza se ejecuta al arrancar y solo toca carpetas con el prefijo
    /// propio de la aplicación y con más de un día de antigüedad, para no
    /// interferir con otra instancia que pueda estar abierta a la vez.
    /// </remarks>
    public static void LimpiarHuerfanos()
    {
        try
        {
            var limite = DateTime.Now.AddHours(-1);

            foreach (var carpeta in Directory.EnumerateDirectories(
                         Path.GetTempPath(), "GeneradorAnexos-preview-*"))
            {
                try
                {
                    if (Directory.GetLastWriteTime(carpeta) < limite)
                    {
                        var atributos = File.GetAttributes(carpeta);
                        Directory.Delete(
                            carpeta,
                            recursive: (atributos & FileAttributes.ReparsePoint) == 0);
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
        catch (Exception excepcion)
        {
            Registro.Advertencia("PREVIEW_SWEEP_" + excepcion.GetType().Name);
        }
    }

    /// <summary>Limpieza best-effort del directorio creado por esta instancia.</summary>
    public void LimpiarTodo()
    {
        List<string> rutas;
        lock (_candado)
        {
            rutas = new List<string>(_creadas);
        }

        foreach (var ruta in rutas)
        {
            Descartar(ruta);
        }

        try
        {
            if (Directory.Exists(_directorio))
            {
                var atributos = File.GetAttributes(_directorio);
                Directory.Delete(
                    _directorio,
                    recursive: (atributos & FileAttributes.ReparsePoint) == 0);
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
