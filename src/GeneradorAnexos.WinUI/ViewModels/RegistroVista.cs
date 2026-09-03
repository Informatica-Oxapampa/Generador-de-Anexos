using System;
using System.Globalization;

namespace GeneradorAnexos.WinUI.ViewModels;

/// <summary>Fila de la lista de «Usuarios guardados».</summary>
public sealed class RegistroVista
{
    public required long Id { get; init; }

    public required string Nombre { get; init; }

    public required DateTime Actualizado { get; init; }

    /// <summary>
    /// El registro guarda un TDR real (no solo el esqueleto que crea el
    /// formulario). Gobierna si el botón «TDR» está habilitado.
    /// </summary>
    public required bool TieneTdr { get; init; }

    /// <summary>El registro guarda datos de Anexo. Gobierna el botón «Anexo».</summary>
    public required bool TieneAnexo { get; init; }

    /// <summary>Equivalente de <c>_fecha_legible</c> del original.</summary>
    public string FechaLegible =>
        "Guardado: " + Actualizado.ToString(
            "dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("es-PE"));
}
