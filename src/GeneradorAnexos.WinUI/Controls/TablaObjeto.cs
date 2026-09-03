using System;
using System.Globalization;
using System.Linq;
using GeneradorAnexos.Domain.Models;
using GeneradorAnexos.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeneradorAnexos.WinUI.Controls;

/// <summary>
/// Equivalente de <c>ui/tablas_tdr.py: TablaObjeto</c>.
/// </summary>
/// <remarks>
/// Cuadro del objeto idéntico al del Word: ITEM (fijo "1"), CANTIDAD y UNIDAD
/// DE MEDIDA editables y DESCRIPCIÓN DEL SERVICIO, que se sincroniza con la
/// denominación de Datos Generales pero puede editarse a mano.
/// </remarks>
public sealed class TablaObjeto : UserControl
{
    private readonly TextBox _cantidad;
    private readonly TextBox _unidad;
    private readonly TextBox _descripcion;

    public TablaObjeto()
    {
        var rejilla = new Grid { ColumnSpacing = 1, RowSpacing = 1 };

        // Proporciones de columna del original: 10 / 16 / 26 / 48.
        foreach (var peso in new[] { 10, 16, 26, 48 })
        {
            rejilla.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(peso, GridUnitType.Star) });
        }

        rejilla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rejilla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var cabeceras = new[] { "ITEM", "CANTIDAD", "UNIDAD DE MEDIDA", "DESCRIPCIÓN DEL SERVICIO" };
        for (var c = 0; c < cabeceras.Length; c++)
        {
            var cabecera = CeldaTabla.Cabecera(cabeceras[c]);
            Grid.SetRow(cabecera, 0);
            Grid.SetColumn(cabecera, c);
            rejilla.Children.Add(cabecera);
        }

        // ITEM: valor fijo "1".
        var item = CeldaTabla.Envolver(CeldaTabla.Etiqueta("1"));
        Grid.SetRow(item, 1);
        Grid.SetColumn(item, 0);
        rejilla.Children.Add(item);

        _cantidad = CeldaTabla.Campo("1");
        var celdaCantidad = CeldaTabla.Envolver(_cantidad);
        Grid.SetRow(celdaCantidad, 1);
        Grid.SetColumn(celdaCantidad, 1);
        rejilla.Children.Add(celdaCantidad);

        _unidad = CeldaTabla.Campo(Constantes.UnidadMedidaDefecto);
        var celdaUnidad = CeldaTabla.Envolver(_unidad);
        Grid.SetRow(celdaUnidad, 1);
        Grid.SetColumn(celdaUnidad, 2);
        rejilla.Children.Add(celdaUnidad);

        _descripcion = CeldaTabla.Editor(string.Empty, "Descripción del servicio…");
        _descripcion.TextChanged += (_, _) => Cambiado?.Invoke(this, EventArgs.Empty);
        Grid.SetRow(_descripcion, 1);
        Grid.SetColumn(_descripcion, 3);
        rejilla.Children.Add(_descripcion);

        Content = CeldaTabla.Marco(rejilla);
    }

    /// <summary>Cambio del texto de la descripción (receptor de sincronización).</summary>
    public event EventHandler? Cambiado;

    public string Descripcion
    {
        get => _descripcion.Text.Trim();
        set => _descripcion.Text = value ?? string.Empty;
    }

    public string Cantidad => _cantidad.Text.Trim();

    public string Unidad => _unidad.Text.Trim();

    public void EstablecerUnidad(string? texto)
    {
        if (!string.IsNullOrWhiteSpace(texto))
        {
            _unidad.Text = texto.Trim();
        }
    }

    public void DestellarSincronizacion() => EfectoDestello.Aplicar(_descripcion);

    public ObjetoServicioPayload Exportar() => new()
    {
        Cantidad = Cantidad,
        Unidad = Unidad,
        Descripcion = Descripcion,
    };

    public void Importar(ObjetoServicioPayload? datos)
    {
        _cantidad.Text = datos?.Cantidad ?? "1";
        _unidad.Text = string.IsNullOrWhiteSpace(datos?.Unidad)
            ? Constantes.UnidadMedidaDefecto
            : datos!.Unidad!;
        _descripcion.Text = datos?.Descripcion ?? string.Empty;
    }

    public void Limpiar()
    {
        _cantidad.Text = "1";
        _unidad.Text = Constantes.UnidadMedidaDefecto;
        _descripcion.Text = string.Empty;
    }

    public bool Validar()
    {
        var cantidadOk = int.TryParse(
                Cantidad,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var cantidad)
            && cantidad > 0;
        CeldaTabla.Marcar(_cantidad, !cantidadOk);

        var unidadOk = Unidad.Length > 0;
        CeldaTabla.Marcar(_unidad, !unidadOk);

        var descripcionOk = Descripcion.Length > 0;
        CeldaTabla.Marcar(_descripcion, !descripcionOk);

        return cantidadOk && unidadOk && descripcionOk;
    }
}
