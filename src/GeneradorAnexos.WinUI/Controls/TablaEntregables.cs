using System;
using System.Collections.Generic;
using System.Linq;
using GeneradorAnexos.Domain.Documents;
using GeneradorAnexos.Domain.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using GeneradorAnexos.WinUI.Services;

namespace GeneradorAnexos.WinUI.Controls;

/// <summary>
/// Equivalente de <c>ui/tablas_tdr.py: TablaEntregables</c>.
/// </summary>
/// <remarks>
/// Con <see cref="Unico"/> muestra una sola fila fija ("ÚNICO ENTREGABLE"), sin
/// botones de agregar ni eliminar. En modo múltiple exige al menos dos filas y
/// solo permite eliminar cuando hay más de dos, igual que el original.
/// </remarks>
public sealed class TablaEntregables : UserControl
{
    private readonly List<FilaEntregable> _filas = new();
    private readonly Grid _rejilla;
    private readonly Button _botonAgregar;

    public TablaEntregables()
    {
        _rejilla = new Grid { ColumnSpacing = 1, RowSpacing = 1 };

        // Proporciones de columna del original: 22 / 46 / 32.
        foreach (var peso in new[] { 22, 46, 32 })
        {
            _rejilla.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(peso, GridUnitType.Star) });
        }

        _botonAgregar = new Button
        {
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["Ga.BotonSecundarioCompacto"],
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = ContenidoBoton("plus", "Agregar entregable"),
        };
        _botonAgregar.Click += (_, _) => Agregar();

        var raiz = new StackPanel { Spacing = 12 };
        raiz.Children.Add(CeldaTabla.Marco(_rejilla));
        raiz.Children.Add(_botonAgregar);
        Content = raiz;

        Loaded += (_, _) => Reconstruir();
    }

    /// <summary>Alta o baja de filas (dispara la sincronización de pagos).</summary>
    public event EventHandler? Cambio;

    /// <summary>Índice de la fila eliminada, para retirar el pago asociado.</summary>
    public event EventHandler<int>? FilaEliminada;

    /// <summary>Modo de fila única fija.</summary>
    public bool Unico { get; init; }

    public int Cantidad => _filas.Count;

    public void Inicializar()
    {
        if (Unico && _filas.Count == 0)
        {
            _filas.Add(new FilaEntregable());
        }

        Reconstruir();
    }

    public void Agregar()
    {
        _filas.Add(new FilaEntregable());
        Reconstruir();
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    public List<EntregablePayload?> Exportar() => _filas
        .Select(f => (EntregablePayload?)new EntregablePayload
        {
            Descripcion = f.Descripcion,
            Plazo = f.Plazo,
        })
        .ToList();

    /// <summary>Reemplaza las filas por las de la lista (mínimo una, o dos en múltiple).</summary>
    public void Importar(IReadOnlyList<EntregablePayload?>? lista)
    {
        _filas.Clear();

        var objetivo = new List<EntregablePayload?>(lista ?? new List<EntregablePayload?>());
        if (Unico)
        {
            if (objetivo.Count == 0)
            {
                objetivo.Add(null);
            }

            objetivo = objetivo.Take(1).ToList();
        }
        else
        {
            while (objetivo.Count < 2)
            {
                objetivo.Add(null);
            }
        }

        foreach (var datos in objetivo)
        {
            var fila = new FilaEntregable();
            fila.Cargar(datos);
            _filas.Add(fila);
        }

        Reconstruir();
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    public bool Validar()
    {
        var minimo = Unico ? 1 : 2;
        // Lista, no cortocircuito: deben resaltarse todas las celdas faltantes.
        var resultados = _filas.Select(f => f.Validar()).ToList();
        return resultados.All(r => r) && _filas.Count >= minimo;
    }

    public void Limpiar()
    {
        _filas.Clear();
        if (Unico)
        {
            _filas.Add(new FilaEntregable());
        }

        Reconstruir();
    }

    private void Eliminar(FilaEntregable fila)
    {
        if (Unico || _filas.Count <= 2)
        {
            return;
        }

        var indice = _filas.IndexOf(fila);
        _filas.RemoveAt(indice);
        Reconstruir();
        FilaEliminada?.Invoke(this, indice);
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    private void Reconstruir()
    {
        _rejilla.Children.Clear();
        _rejilla.RowDefinitions.Clear();
        _rejilla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var cabeceras = new[] { "ENTREGABLE", "DESCRIPCIÓN", "PLAZO MÁXIMO DEL SERVICIO" };
        for (var c = 0; c < cabeceras.Length; c++)
        {
            var cabecera = CeldaTabla.Cabecera(cabeceras[c]);
            Grid.SetRow(cabecera, 0);
            Grid.SetColumn(cabecera, c);
            _rejilla.Children.Add(cabecera);
        }

        // Solo se puede eliminar cuando quedan más de dos entregables.
        var borrable = !Unico && _filas.Count > 2;

        for (var i = 0; i < _filas.Count; i++)
        {
            _rejilla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var fila = _filas[i];
            fila.EstablecerEtiqueta(Unico ? "ÚNICO ENTREGABLE" : TdrLabels.EtiquetaEntregable(i));
            fila.EstablecerBorrable(borrable, () => Eliminar(fila));

            Agregar(fila.CeldaEtiqueta, i + 1, 0);
            Agregar(fila.EditorDescripcion, i + 1, 1);
            Agregar(fila.EditorPlazo, i + 1, 2);
        }

        _botonAgregar.Visibility = Unico ? Visibility.Collapsed : Visibility.Visible;

        void Agregar(FrameworkElement elemento, int f, int c)
        {
            Grid.SetRow(elemento, f);
            Grid.SetColumn(elemento, c);
            _rejilla.Children.Add(elemento);
        }
    }

    internal static StackPanel ContenidoBoton(string icono, string texto)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(new Icono
        {
            Nombre = icono,
            Tamano = 14,
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["Ga.IconoAcento"],
        });
        panel.Children.Add(new TextBlock { Text = texto, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    /// <summary>Widgets de una fila de la tabla de entregables.</summary>
    private sealed class FilaEntregable
    {
        private readonly TextBlock _etiqueta = CeldaTabla.Etiqueta();
        private readonly Button _botonEliminar;

        public FilaEntregable()
        {
            _botonEliminar = new Button
            {
                Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["Ga.BotonIconoEliminar"],
                HorizontalAlignment = HorizontalAlignment.Right,
                Content = new Icono { Nombre = "trash", Tamano = 14 },
            };
            ToolTipService.SetToolTip(_botonEliminar, "Eliminar entregable");

            // El original apila: botón arriba a la derecha, luego el rótulo
            // centrado vertical y horizontalmente en el resto de la celda.
            var contenido = new Grid();
            contenido.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contenido.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(_botonEliminar, 0);
            contenido.Children.Add(_botonEliminar);

            _etiqueta.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(_etiqueta, 1);
            contenido.Children.Add(_etiqueta);

            CeldaEtiqueta = CeldaTabla.Envolver(contenido);
            CeldaEtiqueta.MinHeight = CeldaTabla.AltoCelda;
            CeldaEtiqueta.Padding = new Thickness(8, 6, 8, 8);
            EditorDescripcion = CeldaTabla.Editor(
                TdrLabels.DescripcionCartaDefecto, "Descripción del entregable…");
            EditorPlazo = CeldaTabla.Editor(string.Empty, TdrLabels.PlazoMarcador);
            EditorPlazo.AcceptsReturn = false;
            EditorPlazo.TextAlignment = TextAlignment.Center;
            EditorPlazo.VerticalContentAlignment = VerticalAlignment.Center;
            EditorPlazo.Padding = new Thickness(10, 0, 10, 0);
            EditorPlazo.BeforeTextChanging += (_, e) =>
                e.Cancel = e.NewText.Length > 3 || e.NewText.Any(c => !char.IsDigit(c));

            var alcance = new InputScope();
            alcance.Names.Add(new InputScopeName(InputScopeNameValue.Number));
            EditorPlazo.InputScope = alcance;

        }

        public Border CeldaEtiqueta { get; }

        public TextBox EditorDescripcion { get; }

        public TextBox EditorPlazo { get; }

        public string Descripcion => EditorDescripcion.Text.Trim();

        public string Plazo => TdrLabels.DiasConSufijo(EditorPlazo.Text);

        public void EstablecerEtiqueta(string texto) => _etiqueta.Text = texto;

        public void EstablecerBorrable(bool borrable, Action alEliminar)
        {
            _botonEliminar.Visibility = borrable ? Visibility.Visible : Visibility.Collapsed;
            _botonEliminar.Click -= Manejador;
            _botonEliminar.Click += Manejador;

            void Manejador(object s, RoutedEventArgs e) => alEliminar();
        }

        public void Cargar(EntregablePayload? datos)
        {
            EditorDescripcion.Text = datos?.Descripcion ?? string.Empty;
            EditorPlazo.Text = TdrLabels.ExtraerCantidadDias(datos?.Plazo);
        }

        public bool Validar()
        {
            var descripcionVacia = string.IsNullOrWhiteSpace(Descripcion);
            var plazoVacio = string.IsNullOrWhiteSpace(Plazo);
            CeldaTabla.Marcar(EditorDescripcion, descripcionVacia);
            CeldaTabla.Marcar(EditorPlazo, plazoVacio);
            return !descripcionVacia && !plazoVacio;
        }
    }
}
