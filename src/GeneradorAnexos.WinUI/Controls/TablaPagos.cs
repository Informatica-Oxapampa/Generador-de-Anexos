using System;
using System.Collections.Generic;
using System.Linq;
using GeneradorAnexos.Domain.Documents;
using GeneradorAnexos.Domain.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using GeneradorAnexos.WinUI.Services;

namespace GeneradorAnexos.WinUI.Controls;

/// <summary>
/// Equivalente de <c>ui/tablas_tdr.py: TablaPagos</c>.
/// </summary>
/// <remarks>
/// Tabla de forma de pago con fila TOTAL PORCENTAJE, que se muestra en verde
/// cuando suma 100 % y en rojo en caso contrario, y botón «Distribuir 100%».
/// </remarks>
public sealed class TablaPagos : UserControl
{
    private readonly List<FilaPago> _filas = new();
    private readonly Grid _rejilla;
    private readonly TextBlock _totalValor;
    private readonly Border _celdaTotal;

    public TablaPagos()
    {
        _rejilla = new Grid { ColumnSpacing = 1, RowSpacing = 1 };

        // Proporciones de columna del original: 22 / 56 / 22.
        foreach (var peso in new[] { 22, 56, 22 })
        {
            _rejilla.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(peso, GridUnitType.Star) });
        }

        _totalValor = new TextBlock
        {
            Text = "0%",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _celdaTotal = CeldaTabla.Envolver(_totalValor);

        var botonDistribuir = new Button
        {
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["Ga.BotonSecundarioCompacto"],
            Content = "Distribuir 100%",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        botonDistribuir.Click += (_, _) => Distribuir();

        var raiz = new StackPanel { Spacing = 12 };
        raiz.Children.Add(CeldaTabla.Marco(_rejilla));
        raiz.Children.Add(botonDistribuir);
        Content = raiz;

        Loaded += (_, _) => Reconstruir();
    }

    /// <summary>Cambio del total, para refrescar el resumen en Anexos.</summary>
    public event EventHandler? TotalCambiado;

    public int Cantidad => _filas.Count;

    public int Total => _filas.Sum(f => f.Porcentaje);

    /// <summary>
    /// Ajusta la cantidad de pagos y, si cambió, reparte nuevamente el 100 %.
    /// Al importar un registro, sus valores guardados se cargan inmediatamente
    /// después y por eso no se pierden los porcentajes persistidos.
    /// </summary>
    public void EstablecerCantidad(int cantidad)
    {
        cantidad = Math.Max(0, cantidad);
        var cambioCantidad = _filas.Count != cantidad;

        while (_filas.Count < cantidad)
        {
            var fila = new FilaPago(ActualizarTotal);
            fila.EstablecerCondicionDefecto(_filas.Count);
            _filas.Add(fila);
        }

        while (_filas.Count > cantidad)
        {
            _filas.RemoveAt(_filas.Count - 1);
        }

        Reconstruir();
        if (cambioCantidad)
        {
            Distribuir();
        }
    }

    /// <summary>
    /// Elimina el pago del entregable retirado y redistribuye el 100 %.
    /// Las condiciones que aún eran automáticas se renumeran; las editadas a
    /// mano se conservan.
    /// </summary>
    public void Eliminar(int indice)
    {
        if (indice < 0 || indice >= _filas.Count)
        {
            return;
        }

        var automaticas = _filas
            .Select((fila, i) => fila.UsaCondicionDefecto(i))
            .ToList();

        _filas.RemoveAt(indice);
        automaticas.RemoveAt(indice);

        for (var i = 0; i < _filas.Count; i++)
        {
            if (automaticas[i])
            {
                _filas[i].EstablecerCondicionDefecto(i);
            }
        }

        Reconstruir();
        Distribuir();
    }

    public void Distribuir()
    {
        var valores = TdrLabels.DistribuirPorcentajes(_filas.Count);
        for (var i = 0; i < _filas.Count; i++)
        {
            _filas[i].Porcentaje = valores[i];
        }

        ActualizarTotal();
    }

    public List<PagoPayload?> Exportar() => _filas
        .Select(f => (PagoPayload?)new PagoPayload
        {
            Condicion = f.Condicion,
            Porcentaje = f.Porcentaje,
        })
        .ToList();

    public void Importar(IReadOnlyList<PagoPayload?>? lista)
    {
        var datos = lista ?? new List<PagoPayload?>();
        EstablecerCantidad(datos.Count);
        for (var i = 0; i < _filas.Count; i++)
        {
            _filas[i].Cargar(datos[i]);
        }

        ActualizarTotal();
    }

    public bool Validar()
    {
        var resultados = _filas.Select(f => f.Validar()).ToList();
        return resultados.All(r => r);
    }

    public void Limpiar()
    {
        _filas.Clear();
        Reconstruir();
    }

    private void Reconstruir()
    {
        _rejilla.Children.Clear();
        _rejilla.RowDefinitions.Clear();
        _rejilla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var cabeceras = new[] { "N° DE PAGO", "CONDICIÓN", "PORCENTAJE" };
        for (var c = 0; c < cabeceras.Length; c++)
        {
            var cabecera = CeldaTabla.Cabecera(cabeceras[c]);
            Grid.SetRow(cabecera, 0);
            Grid.SetColumn(cabecera, c);
            _rejilla.Children.Add(cabecera);
        }

        for (var i = 0; i < _filas.Count; i++)
        {
            _rejilla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var fila = _filas[i];
            fila.EstablecerIndice(i);

            Colocar(fila.CeldaEtiqueta, i + 1, 0);
            Colocar(fila.EditorCondicion, i + 1, 1);
            Colocar(fila.CeldaPorcentaje, i + 1, 2);
        }

        // Fila TOTAL PORCENTAJE: rótulo combinado en las dos primeras columnas.
        var filaTotal = _filas.Count + 1;
        _rejilla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var rotulo = CeldaTabla.Envolver(new TextBlock
        {
            Text = "TOTAL PORCENTAJE",
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["Ga.CeldaEtiqueta"],
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetRow(rotulo, filaTotal);
        Grid.SetColumn(rotulo, 0);
        Grid.SetColumnSpan(rotulo, 2);
        _rejilla.Children.Add(rotulo);

        // Se reutiliza la celda completa. Crear otro Border con _totalValor
        // fallaba porque el TextBlock seguia siendo hijo del Border anterior,
        // aunque ese Border ya se hubiera retirado de la rejilla.
        Grid.SetRow(_celdaTotal, filaTotal);
        Grid.SetColumn(_celdaTotal, 2);
        _rejilla.Children.Add(_celdaTotal);

        ActualizarTotal();

        void Colocar(FrameworkElement elemento, int f, int c)
        {
            Grid.SetRow(elemento, f);
            Grid.SetColumn(elemento, c);
            _rejilla.Children.Add(elemento);
        }
    }

    private void ActualizarTotal()
    {
        var total = Total;
        _totalValor.Text = $"{total}%";
        _totalValor.Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources[
            total == 100 ? "Ga.Ok" : "Ga.Error"];
        TotalCambiado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Widgets de una fila de la tabla de pagos.</summary>
    private sealed class FilaPago
    {
        private readonly TextBlock _etiqueta = CeldaTabla.Etiqueta();
        private readonly CampoPorcentaje _porcentaje;

        public FilaPago(Action alCambiarPorcentaje)
        {
            CeldaEtiqueta = CeldaTabla.Envolver(_etiqueta);
            CeldaEtiqueta.MinHeight = CeldaTabla.AltoCelda;
            CeldaEtiqueta.Padding = new Thickness(8, 6, 8, 8);
            EditorCondicion = CeldaTabla.Editor(string.Empty, "Condición del pago…");

            _porcentaje = new CampoPorcentaje();
            _porcentaje.Cambiado += (_, _) => alCambiarPorcentaje();
            CeldaPorcentaje = CeldaTabla.Envolver(_porcentaje);
        }

        public Border CeldaEtiqueta { get; }

        public TextBox EditorCondicion { get; }

        public Border CeldaPorcentaje { get; }

        public string Condicion => EditorCondicion.Text.Trim();

        public int Porcentaje
        {
            get => _porcentaje.Valor;
            set => _porcentaje.Valor = value;
        }

        public void EstablecerIndice(int indice) => _etiqueta.Text = TdrLabels.EtiquetaPago(indice);

        public void EstablecerCondicionDefecto(int indice)
            => EditorCondicion.Text = TdrLabels.CondicionPagoDefecto(indice);

        /// <summary>True si la condición conserva exactamente el texto autogenerado.</summary>
        public bool UsaCondicionDefecto(int indice)
            => Condicion == TdrLabels.CondicionPagoDefecto(indice);

        public void Cargar(PagoPayload? datos)
        {
            EditorCondicion.Text = datos?.Condicion ?? string.Empty;
            Porcentaje = datos?.Porcentaje ?? 0;
        }

        public bool Validar()
        {
            var vacio = string.IsNullOrWhiteSpace(Condicion);
            CeldaTabla.Marcar(EditorCondicion, vacio);
            return !vacio;
        }
    }
}

/// <summary>Equivalente de <c>ui/tablas_tdr.py: CampoPorcentaje</c>.</summary>
public sealed class CampoPorcentaje : UserControl
{
    private readonly TextBox _entrada;
    private string _anterior = "0";

    public CampoPorcentaje()
    {
        _entrada = new TextBox
        {
            Text = "0",
            Width = 62,
            TextAlignment = TextAlignment.Center,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,

            // Estilo de caja de texto estándar: hereda del control nativo de
            // WinUI, así que sigue al tema y conserva sus estados de puntero,
            // foco y deshabilitado.
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["Ga.CajaTexto"],
        };

        _entrada.TextChanged += AlCambiar;

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        panel.Children.Add(_entrada);
        panel.Children.Add(new TextBlock
        {
            Text = "%",
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["Ga.EtiquetaCampo"],
            VerticalAlignment = VerticalAlignment.Center,
        });

        Content = panel;
    }

    public event EventHandler? Cambiado;

    public int Valor
    {
        get => int.TryParse(_entrada.Text, out var v) ? v : 0;
        set => _entrada.Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Equivalente del <c>QIntValidator(0, 100)</c> del original.</summary>
    private void AlCambiar(object sender, TextChangedEventArgs e)
    {
        var texto = _entrada.Text;
        var valido = texto.Length == 0 ||
                     (texto.All(char.IsDigit) && int.TryParse(texto, out var v) && v is >= 0 and <= 100);

        if (!valido)
        {
            var posicion = Math.Max(0, _entrada.SelectionStart - 1);
            _entrada.TextChanged -= AlCambiar;
            _entrada.Text = _anterior;
            _entrada.SelectionStart = Math.Min(posicion, _entrada.Text.Length);
            _entrada.TextChanged += AlCambiar;
            return;
        }

        _anterior = texto;
        Cambiado?.Invoke(this, EventArgs.Empty);
    }
}
