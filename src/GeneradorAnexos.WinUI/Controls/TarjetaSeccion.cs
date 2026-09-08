using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeneradorAnexos.WinUI.Controls;

/// <summary>
/// Equivalente de <c>ui/widgets.py: TarjetaSeccion</c>.
/// </summary>
/// <remarks>
/// Tarjeta con encabezado (icono en placa tenue + titulo + descripcion),
/// separador de 1 px y una rejilla de dos columnas. Igual que el original,
/// por debajo de <see cref="AnchoReflujo"/> px la rejilla colapsa a una sola
/// columna sin recrear los controles, de modo que ningun enlace se pierde.
/// </remarks>
public sealed partial class TarjetaSeccion : ContentControl
{
    /// <summary>ui/widgets.py: ANCHO_REFLOW_TARJETA.</summary>
    public const double AnchoReflujo = 520;

    private readonly List<ElementoRejilla> _elementos = new();
    private Grid? _rejilla;
    private bool? _modoEstrecho;

    public TarjetaSeccion()
    {
        DefaultStyleKey = typeof(TarjetaSeccion);
        SizeChanged += AlCambiarTamano;
    }

    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(TarjetaSeccion),
            new PropertyMetadata(string.Empty));

    public string Descripcion
    {
        get => (string)GetValue(DescripcionProperty);
        set => SetValue(DescripcionProperty, value);
    }

    public static readonly DependencyProperty DescripcionProperty =
        DependencyProperty.Register(nameof(Descripcion), typeof(string), typeof(TarjetaSeccion),
            new PropertyMetadata(string.Empty));

    /// <summary>Nombre del icono del catalogo (parametro <c>icono</c> del original).</summary>
    public string Icono
    {
        get => (string)GetValue(IconoProperty);
        set => SetValue(IconoProperty, value);
    }

    public static readonly DependencyProperty IconoProperty =
        DependencyProperty.Register(nameof(Icono), typeof(string), typeof(TarjetaSeccion),
            new PropertyMetadata("file-check"));

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _rejilla = GetTemplateChild("PartRejilla") as Grid;
        _modoEstrecho = null;
        Reorganizar(ActualWidth > 0 && ActualWidth < AnchoReflujo);
    }

    /// <summary>Equivalente de <c>TarjetaSeccion.agregar</c>.</summary>
    public void Agregar(FrameworkElement widget, int fila, int columna = 0, bool anchoCompleto = false)
    {
        _elementos.Add(new ElementoRejilla(widget, fila, columna, anchoCompleto));
        if (_rejilla is not null && !_rejilla.Children.Contains(widget))
        {
            _rejilla.Children.Add(widget);
        }

        _modoEstrecho = null;
        Reorganizar(ActualWidth > 0 && ActualWidth < AnchoReflujo);
    }

    private void AlCambiarTamano(object sender, SizeChangedEventArgs e)
        => Reorganizar(e.NewSize.Width < AnchoReflujo);

    /// <summary>Refluye sin recrear controles ni perder enlaces.</summary>
    private void Reorganizar(bool estrecho)
    {
        if (_rejilla is null || _modoEstrecho == estrecho)
        {
            return;
        }

        _modoEstrecho = estrecho;
        AsegurarFilas();

        if (estrecho)
        {
            var ordenados = new List<ElementoRejilla>(_elementos);
            ordenados.Sort(static (a, b) =>
                a.Fila != b.Fila ? a.Fila.CompareTo(b.Fila) : a.Columna.CompareTo(b.Columna));

            for (var i = 0; i < ordenados.Count; i++)
            {
                Grid.SetRow(ordenados[i].Widget, i);
                Grid.SetColumn(ordenados[i].Widget, 0);
                Grid.SetColumnSpan(ordenados[i].Widget, 2);
            }

            return;
        }

        foreach (var elemento in _elementos)
        {
            Grid.SetRow(elemento.Widget, elemento.Fila);
            Grid.SetColumn(elemento.Widget, elemento.Columna);
            Grid.SetColumnSpan(elemento.Widget, elemento.AnchoCompleto ? 2 : 1);
        }
    }

    private void AsegurarFilas()
    {
        if (_rejilla is null)
        {
            return;
        }

        // En modo estrecho cada elemento ocupa su propia fila.
        var necesarias = _elementos.Count;
        foreach (var elemento in _elementos)
        {
            if (elemento.Fila + 1 > necesarias)
            {
                necesarias = elemento.Fila + 1;
            }
        }

        while (_rejilla.RowDefinitions.Count < necesarias)
        {
            _rejilla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
    }

    private readonly record struct ElementoRejilla(
        FrameworkElement Widget, int Fila, int Columna, bool AnchoCompleto);
}
