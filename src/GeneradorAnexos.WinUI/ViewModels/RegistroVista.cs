using System;
using System.Globalization;

namespace GeneradorAnexos.WinUI.ViewModels;

/// <summary>Fila de la lista de «Usuarios guardados».</summary>
public sealed class RegistroVista
{
    public required long Id { get; init; }

    public required string Nombre { get; init; }

    public required DateTime Creado { get; init; }

    public required DateTime Actualizado { get; init; }

    /// <summary>
    /// El registro guarda un TDR real (no solo el esqueleto que crea el
    /// formulario). Gobierna si el botón «TDR» está habilitado.
    /// </summary>
    public required bool TieneTdr { get; init; }

    /// <summary>El registro guarda datos de Anexo. Gobierna el botón «Anexo».</summary>
    public required bool TieneAnexo { get; init; }

    /// <summary>Fechas históricas del registro, independientes de la fecha del documento.</summary>
    public string FechaLegible
    {
        get
        {
            var cultura = CultureInfo.GetCultureInfo("es-PE");
            var creadoLocal = Creado.ToLocalTime();
            var actualizadoLocal = Actualizado.ToLocalTime();
            var texto = "Creado: " + creadoLocal.ToString("dd/MM/yyyy HH:mm", cultura);

            return Math.Abs((actualizadoLocal - creadoLocal).TotalSeconds) < 1
                ? texto
                : texto + " · Actualizado: " + actualizadoLocal.ToString("dd/MM/yyyy HH:mm", cultura);
        }
    }
}
