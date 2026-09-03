using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeneradorAnexos.WinUI.Controls;

/// <summary>Equivalente de <c>ui/widgets.py: CampoArea</c>.</summary>
public sealed partial class CampoArea : UserControl, ICampo
{
    private bool _silenciado;

    public CampoArea()
    {
        InitializeComponent();
        Loaded += (_, _) => RefrescarEtiqueta();
    }

    /// <summary>Edicion del usuario (no se emite al escribir por codigo).</summary>
    public event EventHandler? Cambiado;

    /// <summary>Cualquier cambio de texto, incluido el programatico.</summary>
    public event EventHandler? TextoCambiado;

    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(CampoArea),
            new PropertyMetadata(string.Empty, (d, _) => ((CampoArea)d).RefrescarEtiqueta()));

    public string Marcador
    {
        get => (string)GetValue(MarcadorProperty);
        set => SetValue(MarcadorProperty, value);
    }

    public static readonly DependencyProperty MarcadorProperty =
        DependencyProperty.Register(nameof(Marcador), typeof(string), typeof(CampoArea),
            new PropertyMetadata(string.Empty,
                (d, e) => ((CampoArea)d).Entrada.PlaceholderText =
                    e.NewValue as string ?? string.Empty));

    public string Ayuda
    {
        get => (string)GetValue(AyudaProperty);
        set => SetValue(AyudaProperty, value);
    }

    public static readonly DependencyProperty AyudaProperty =
        DependencyProperty.Register(nameof(Ayuda), typeof(string), typeof(CampoArea),
            new PropertyMetadata(string.Empty, (d, _) => ((CampoArea)d).AplicarEstado(EstadoCampo.Neutro)));

    public bool Obligatorio
    {
        get => (bool)GetValue(ObligatorioProperty);
        set => SetValue(ObligatorioProperty, value);
    }

    public static readonly DependencyProperty ObligatorioProperty =
        DependencyProperty.Register(nameof(Obligatorio), typeof(bool), typeof(CampoArea),
            new PropertyMetadata(true, (d, _) => ((CampoArea)d).RefrescarEtiqueta()));

    /// <summary>
    /// Alto minimo del area. Se usa alto minimo y no alto fijo para que el
    /// cuadro pueda crecer con el tamano de texto configurado en Windows.
    /// </summary>
    public double Altura
    {
        get => Entrada.MinHeight;
        set => Entrada.MinHeight = value;
    }

    public TextBox Caja => Entrada;

    public bool EsValido { get; private set; }

    public string Valor
    {
        get => Entrada.Text.Trim();
        set => Entrada.Text = value ?? string.Empty;
    }

    public void EstablecerValorSilencioso(string? texto)
    {
        _silenciado = true;
        try
        {
            Entrada.Text = texto ?? string.Empty;
        }
        finally
        {
            _silenciado = false;
        }

        Validar();
    }

    public void Limpiar()
    {
        Entrada.Text = string.Empty;
        EsValido = !Obligatorio;
        AplicarEstado(EstadoCampo.Neutro);
    }

    public bool ForzarValidacion()
    {
        if (Obligatorio && string.IsNullOrWhiteSpace(Valor))
        {
            EsValido = false;
            AplicarEstado(EstadoCampo.Invalido, "Este campo es obligatorio.");
            return false;
        }

        Validar();
        return EsValido;
    }

    public void Enfocar() => Entrada.Focus(FocusState.Programmatic);

    public void DestellarSincronizacion() => EfectoDestello.Aplicar(Entrada);

    private void RefrescarEtiqueta()
        => Etiqueta.Text = Obligatorio ? $"{Titulo}  *" : Titulo;

    private void AlCambiarTexto(object sender, TextChangedEventArgs e)
    {
        Validar();
        TextoCambiado?.Invoke(this, EventArgs.Empty);
        if (!_silenciado)
        {
            Cambiado?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Validar()
    {
        if (string.IsNullOrEmpty(Valor))
        {
            EsValido = !Obligatorio;
            AplicarEstado(EstadoCampo.Neutro);
            return;
        }

        EsValido = true;
        AplicarEstado(EstadoCampo.Valido, "Correcto");
    }

    private void AplicarEstado(EstadoCampo estado, string? mensaje = null)
    {
        mensaje ??= Ayuda;

        if (estado == EstadoCampo.Invalido)
        {
            Entrada.BorderBrush = CampoTexto.Recurso("Ga.Error");
        }
        else
        {
            Entrada.ClearValue(Control.BorderBrushProperty);
        }

        Mensaje.Style = estado switch
        {
            EstadoCampo.Valido => (Style)Microsoft.UI.Xaml.Application.Current.Resources["Ga.MensajeOk"],
            EstadoCampo.Invalido => (Style)Microsoft.UI.Xaml.Application.Current.Resources["Ga.MensajeError"],
            _ => (Style)Microsoft.UI.Xaml.Application.Current.Resources["Ga.MensajeAyuda"],
        };

        IconoOk.Visibility = estado == EstadoCampo.Valido ? Visibility.Visible : Visibility.Collapsed;
        Mensaje.Text = mensaje;
        FilaMensaje.Visibility = string.IsNullOrEmpty(mensaje) ? Visibility.Collapsed : Visibility.Visible;
    }
}
