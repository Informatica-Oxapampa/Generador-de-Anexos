using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace GeneradorAnexos.WinUI.Services;

/// <summary>
/// Enumera las impresoras instaladas y envía documentos a la elegida.
/// </summary>
/// <remarks>
/// <b>Por qué no se compone la impresión dentro de la aplicación.</b> Hacerlo
/// obligaría a reproducir la paginación de Word: márgenes, tablas que se parten
/// entre páginas, encabezados, pies y saltos. Cualquier diferencia haría que el
/// papel no coincida con el documento oficial, y eso no puede pasar en un
/// expediente de contratación.
///
/// Por eso se genera primero el .docx y después se envía al sistema de
/// impresión de Windows mediante el verbo <c>printto</c>, indicando la
/// impresora que eligió el usuario. Quien pagina e imprime es Word, de modo que
/// el resultado en papel es idéntico al documento.
///
/// La lista de impresoras se obtiene de <c>winspool.drv</c>, el mismo servicio
/// de cola de impresión que usa Windows. Incluye las locales y las conectadas
/// por red, que es lo habitual en una municipalidad.
/// </remarks>
public static class ServicioImpresion
{
    /// <summary>Resultado del intento de imprimir o abrir.</summary>
    public enum Resultado
    {
        /// <summary>Se envió a la aplicación asociada.</summary>
        Enviado,

        /// <summary>El archivo ya no está donde se generó.</summary>
        ArchivoNoEncontrado,

        /// <summary>No hay aplicación registrada para imprimir documentos de Word.</summary>
        SinAplicacionAsociada,

        /// <summary>No se encontró ninguna impresora instalada.</summary>
        SinImpresoras,

        /// <summary>Cualquier otro fallo.</summary>
        Fallo,
    }

    private const uint EnumerarLocales = 0x00000002;
    private const uint EnumerarConexiones = 0x00000004;
    private const uint NivelInformacion4 = 4;
    private const int BufferInsuficiente = 122;

    /// <summary>
    /// Impresoras instaladas en el equipo, locales y de red.
    /// </summary>
    /// <remarks>
    /// Nunca lanza: si la cola de impresión no responde, devuelve una lista
    /// vacía y la interfaz lo explica. Un problema del servicio de impresión no
    /// debe impedir seguir trabajando con el programa.
    /// </remarks>
    public static IReadOnlyList<string> Listar()
    {
        try
        {
            var banderas = EnumerarLocales | EnumerarConexiones;

            // Primera llamada: se pregunta cuánta memoria hace falta.
            EnumPrintersW(banderas, null, NivelInformacion4, IntPtr.Zero, 0,
                          out var necesario, out _);

            if (necesario == 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != BufferInsuficiente)
                {
                    Registro.Advertencia("PRINTERS_ENUM_EMPTY");
                }

                return Array.Empty<string>();
            }

            var buffer = Marshal.AllocHGlobal((int)necesario);
            try
            {
                if (!EnumPrintersW(banderas, null, NivelInformacion4, buffer, necesario,
                                   out _, out var cantidad))
                {
                    Registro.Advertencia("PRINTERS_ENUM_FAILED");
                    return Array.Empty<string>();
                }

                var tamano = Marshal.SizeOf<InfoImpresora4>();
                var nombres = new List<string>((int)cantidad);

                for (var i = 0; i < cantidad; i++)
                {
                    var info = Marshal.PtrToStructure<InfoImpresora4>(buffer + i * tamano);
                    if (!string.IsNullOrWhiteSpace(info.Nombre))
                    {
                        nombres.Add(info.Nombre);
                    }
                }

                nombres.Sort(StringComparer.CurrentCultureIgnoreCase);
                return nombres;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception excepcion)
        {
            Registro.Error("PRINTERS_ENUM_FAILED", excepcion);
            return Array.Empty<string>();
        }
    }

    /// <summary>Impresora predeterminada de Windows, o cadena vacía.</summary>
    public static string Predeterminada()
    {
        try
        {
            var longitud = 0;
            GetDefaultPrinterW(null, ref longitud);

            if (longitud <= 0)
            {
                return string.Empty;
            }

            var nombre = new StringBuilder(longitud);
            return GetDefaultPrinterW(nombre, ref longitud) ? nombre.ToString() : string.Empty;
        }
        catch (Exception excepcion)
        {
            Registro.Advertencia("DEFAULT_PRINTER_" + excepcion.GetType().Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Envía el documento a la impresora indicada.
    /// </summary>
    /// <remarks>
    /// Si la aplicación asociada no registra el verbo <c>printto</c> —cosa que
    /// ocurre con algunos visores alternativos de documentos— se reintenta con
    /// <c>print</c>, que imprime en la predeterminada. Es preferible imprimir
    /// en otra bandeja a no imprimir, y la interfaz avisa de lo ocurrido.
    /// </remarks>
    public static Resultado Imprimir(string ruta, string impresora)
    {
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
        {
            Registro.Advertencia("PRINT_FILE_MISSING");
            return Resultado.ArchivoNoEncontrado;
        }

        if (!string.IsNullOrWhiteSpace(impresora))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ruta,
                    Verb = "printto",

                    // El nombre va entre comillas: casi todas las impresoras de
                    // red llevan espacios.
                    Arguments = "\"" + impresora + "\"",
                    UseShellExecute = true,
                });

                Registro.Info("PRINT_SENT_TO_SELECTED");
                return Resultado.Enviado;
            }
            catch (Exception excepcion)
            {
                Registro.Advertencia("PRINTTO_UNAVAILABLE_" + excepcion.GetType().Name);
            }
        }

        return ImprimirEnPredeterminada(ruta);
    }

    /// <summary>Imprime en la impresora predeterminada.</summary>
    public static Resultado ImprimirEnPredeterminada(string ruta)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ruta,
                Verb = "print",
                UseShellExecute = true,
            });

            Registro.Info("PRINT_SENT_DEFAULT");
            return Resultado.Enviado;
        }
        catch (System.ComponentModel.Win32Exception excepcion)
            when (excepcion.NativeErrorCode == 1155)
        {
            Registro.Advertencia("PRINT_NO_HANDLER");
            return Resultado.SinAplicacionAsociada;
        }
        catch (Exception excepcion)
        {
            Registro.Error("PRINT_FAILED", excepcion);
            return Resultado.Fallo;
        }
    }

    /// <summary>
    /// Abre un documento o una carpeta con la aplicación predeterminada.
    /// </summary>
    /// <remarks>
    /// Antes esta llamada no controlaba errores: si el archivo se había movido
    /// o el equipo no tenía Word, la excepción se perdía en una tarea sin
    /// observar y al usuario no le pasaba nada al pulsar el botón.
    /// </remarks>
    public static Resultado Abrir(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta)
            || (!File.Exists(ruta) && !Directory.Exists(ruta)))
        {
            Registro.Advertencia("SHELL_OPEN_MISSING");
            return Resultado.ArchivoNoEncontrado;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ruta,
                UseShellExecute = true,
            });

            return Resultado.Enviado;
        }
        catch (System.ComponentModel.Win32Exception excepcion)
            when (excepcion.NativeErrorCode == 1155)
        {
            Registro.Advertencia("SHELL_OPEN_NO_HANDLER");
            return Resultado.SinAplicacionAsociada;
        }
        catch (Exception excepcion)
        {
            Registro.Error("SHELL_OPEN_FAILED", excepcion);
            return Resultado.Fallo;
        }
    }

    /// <summary>Equivalente administrado de PRINTER_INFO_4W.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct InfoImpresora4
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Nombre;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string Servidor;

        public uint Atributos;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool EnumPrintersW(
        uint flags,
        string? nombre,
        uint nivel,
        IntPtr buffer,
        uint tamanoBuffer,
        out uint necesario,
        out uint devueltas);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool GetDefaultPrinterW(StringBuilder? nombre, ref int longitud);
}
