using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GeneradorAnexos.Infrastructure.Windows.Documents;

namespace GeneradorAnexos.WinUI.Services;

/// <summary>
/// Catálogos que cambian con la organización municipal: áreas usuarias y
/// entidades bancarias.
/// </summary>
/// <remarks>
/// <b>Por qué no están en el código.</b> Antes eran dos listas fijas en
/// <see cref="Constantes"/>. Cada vez que el ROF creaba una subgerencia o se
/// fusionaba una oficina había que recompilar el programa, generar un
/// instalador nuevo y actualizarlo en todos los equipos, solo para añadir una
/// línea de texto.
///
/// Ahora viven en <c>catalogos.json</c>, que viaja en el mismo paquete que las
/// plantillas de Word. Ese canal pesa kilobytes, no pide permisos de
/// administrador y ya está en funcionamiento: actualizar el organigrama pasa a
/// ser editar un archivo y publicar una versión de plantillas.
///
/// <b>Siempre hay catálogo.</b> Si el archivo falta, está dañado o llega vacío,
/// se usan las listas incluidas en <see cref="Constantes"/>. El programa nunca
/// se queda sin áreas ni sin bancos por un problema del archivo.
/// </remarks>
public static class ServicioCatalogos
{
    private static Catalogos? _cargados;

    /// <summary>Áreas usuarias de la municipalidad.</summary>
    public static IReadOnlyList<string> AreasUsuarias =>
        Cargar().AreasUsuarias is { Count: > 0 } areas ? areas : Constantes.AreasMunicipales;

    /// <summary>Entidades bancarias donde puede abonarse al proveedor.</summary>
    public static IReadOnlyList<string> EntidadesBancarias =>
        Cargar().EntidadesBancarias is { Count: > 0 } bancos ? bancos : Constantes.EntidadesBancarias;

    /// <summary>Origen efectivo de los catálogos, para mostrarlo en Configuración.</summary>
    public static string Origen { get; private set; } = "incluidos con el programa";

    /// <summary>
    /// Vuelve a leer el archivo. Se llama tras instalar una actualización de
    /// plantillas, para que los catálogos nuevos estén disponibles sin
    /// reiniciar.
    /// </summary>
    public static void Recargar()
    {
        _cargados = null;
        _ = Cargar();
    }

    private static Catalogos Cargar()
    {
        if (_cargados is not null)
        {
            return _cargados;
        }

        foreach (var (ruta, origen) in Rutas())
        {
            if (!File.Exists(ruta))
            {
                continue;
            }

            try
            {
                var leido = JsonSerializer.Deserialize<Catalogos>(File.ReadAllText(ruta));
                if (leido is not null
                    && (leido.AreasUsuarias.Count > 0 || leido.EntidadesBancarias.Count > 0))
                {
                    Origen = origen;
                    Registro.Info("CATALOGS_LOADED");
                    return _cargados = leido;
                }

                Registro.Advertencia("CATALOGS_EMPTY");
            }
            catch (JsonException)
            {
                Registro.Advertencia("CATALOGS_PARSE_FAILED");
            }
            catch (IOException)
            {
                Registro.Advertencia("CATALOGS_READ_FAILED");
            }
            catch (UnauthorizedAccessException)
            {
                Registro.Advertencia("CATALOGS_READ_DENIED");
            }
        }

        Origen = "incluidos con el programa";
        return _cargados = new Catalogos();
    }

    /// <summary>
    /// Orden de búsqueda: primero los catálogos actualizados del usuario,
    /// después los que trajo el instalador. Es el mismo criterio que aplica
    /// <see cref="RutasPlantillas"/> con las plantillas de Word.
    /// </summary>
    private static IEnumerable<(string Ruta, string Origen)> Rutas()
    {
        if (RutasPlantillas.CarpetaPreferida is { Length: > 0 } carpeta)
        {
            yield return (Path.Combine(carpeta, "catalogos.json"), "actualizados");
        }

        yield return (Path.Combine(RutasPlantillas.CarpetaIncluida, "catalogos.json"),
                      "incluidos con el programa");
    }

    private sealed class Catalogos
    {
        [JsonPropertyName("areasUsuarias")]
        public List<string> AreasUsuarias { get; set; } = new();

        [JsonPropertyName("entidadesBancarias")]
        public List<string> EntidadesBancarias { get; set; } = new();
    }
}
