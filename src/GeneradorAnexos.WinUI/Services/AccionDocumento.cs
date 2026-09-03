using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GeneradorAnexos.WinUI.Services;

/// <summary>
/// Acciones sobre un documento ya generado, con el mensaje que corresponde a
/// cada resultado.
/// </summary>
/// <remarks>
/// Existe para que las dos páginas (TDR y Anexos) no dupliquen el manejo de
/// errores, y para que ningún fallo quede en silencio: antes, abrir un
/// documento era una llamada sin control de excepciones dentro de una tarea sin
/// observar, de modo que si el archivo se había movido o el equipo no tenía
/// Word, al usuario no le pasaba absolutamente nada al pulsar el botón.
/// </remarks>
public static class AccionDocumento
{
    /// <summary>
    /// Pregunta en qué impresora imprimir y envía el documento.
    /// </summary>
    /// <remarks>
    /// Flujo: se listan las impresoras instaladas, el usuario elige una —queda
    /// preseleccionada la última que usó, o la predeterminada de Windows— y el
    /// documento se envía a esa. La impresora elegida se recuerda para la
    /// próxima vez.
    /// </remarks>
    public static async Task ImprimirAsync(string ruta)
    {
        if (!File.Exists(ruta))
        {
            await ServicioDialogos.MostrarAdvertenciaAsync(
                "No se encontró el documento",
                "El archivo ya no está en la carpeta donde se generó. "
                + "Vuelva a generarlo e inténtelo de nuevo.");
            return;
        }

        var impresoras = ServicioImpresion.Listar();

        if (impresoras.Count == 0)
        {
            await ServicioDialogos.MostrarAdvertenciaAsync(
                "No hay impresoras",
                "Windows no reporta ninguna impresora instalada en este equipo."
                + Environment.NewLine + Environment.NewLine
                + "Compruebe que la impresora esté encendida y agregada en "
                + "Configuración de Windows › Bluetooth y dispositivos › Impresoras.");
            return;
        }

        var preferencias = new PreferenciasUi();
        var sugerida = preferencias.UltimaImpresora;

        if (string.IsNullOrWhiteSpace(sugerida) || !impresoras.Contains(sugerida))
        {
            sugerida = ServicioImpresion.Predeterminada();
        }

        var elegida = await ServicioDialogos.PedirImpresoraAsync(impresoras, sugerida);
        if (string.IsNullOrWhiteSpace(elegida))
        {
            return;
        }

        preferencias.UltimaImpresora = elegida;

        switch (ServicioImpresion.Imprimir(ruta, elegida))
        {
            case ServicioImpresion.Resultado.Enviado:
                return;

            case ServicioImpresion.Resultado.SinAplicacionAsociada:
                await ServicioDialogos.MostrarAdvertenciaAsync(
                    "No se pudo imprimir",
                    "Este equipo no tiene un programa asociado para imprimir documentos "
                    + "de Word." + Environment.NewLine + Environment.NewLine
                    + "Abra el documento con Microsoft Word e imprímalo desde ahí.");
                return;

            case ServicioImpresion.Resultado.ArchivoNoEncontrado:
                await ServicioDialogos.MostrarAdvertenciaAsync(
                    "No se encontró el documento",
                    "El archivo ya no está en la carpeta donde se generó.");
                return;

            default:
                await ServicioDialogos.MostrarAdvertenciaAsync(
                    "No se pudo imprimir",
                    "Windows no pudo enviar el documento a «" + elegida + "»."
                    + Environment.NewLine + Environment.NewLine
                    + "Compruebe que la impresora esté disponible, o abra el documento "
                    + "e imprímalo desde Word.");
                return;
        }
    }

    /// <summary>Abre el documento o la carpeta con la aplicación asociada.</summary>
    public static async Task AbrirAsync(string ruta)
    {
        switch (ServicioImpresion.Abrir(ruta))
        {
            case ServicioImpresion.Resultado.Enviado:
                return;

            case ServicioImpresion.Resultado.ArchivoNoEncontrado:
                await ServicioDialogos.MostrarAdvertenciaAsync(
                    "No se encontró el documento",
                    "El archivo ya no está en la carpeta donde se generó.");
                return;

            case ServicioImpresion.Resultado.SinAplicacionAsociada:
                await ServicioDialogos.MostrarAdvertenciaAsync(
                    "No se pudo abrir",
                    "Este equipo no tiene un programa asociado para abrir documentos "
                    + "de Word. Instale Microsoft Word o abra el archivo manualmente.");
                return;

            default:
                await ServicioDialogos.MostrarAdvertenciaAsync(
                    "No se pudo abrir",
                    "Windows no pudo abrir el archivo. Búsquelo en la carpeta donde "
                    + "lo guardó.");
                return;
        }
    }
}
