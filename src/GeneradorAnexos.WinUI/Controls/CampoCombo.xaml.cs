using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace GeneradorAnexos.WinUI.Controls;

/// <summary>
/// Equivalente de <c>ui/widgets.py: CampoCombo</c> y <c>CampoComboEditable</c>.
/// </summary>
public sealed partial class CampoCombo : UserControl, ICampo
{
    private readonly List<string> _opciones = new();
    private bool _silenciado;

    public CampoCombo()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RefrescarEtiqueta();
            AplicarModo();
        };
    }

    public event EventHandler? Cambiado;

    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(CampoCombo),
            new PropertyMetadata(string.Empty, (d, _) => ((CampoCombo)d).RefrescarEtiqueta()));

    /// <summary>Texto mostrado cuando no hay seleccion (PLACEHOLDER_BANCO).</summary>
    public string Marcador
    {
        get => (string)GetValue(MarcadorProperty);
        set => SetValue(MarcadorProperty, value);
    }

    public static readonly DependencyProperty MarcadorProperty =
        DependencyProperty.Register(nameof(Marcador), typeof(string), typeof(CampoCombo),
            new PropertyMetadata(string.Empty, (d, e) =>
            {
                var campo = (CampoCombo)d;
                campo.Lista.PlaceholderText = e.NewValue as string ?? string.Empty;
                campo.Sugerencias.PlaceholderText = e.NewValue as string ?? string.Empty;
            }));

    public string Ayuda
    {
        get => (string)GetValue(AyudaProperty);
        set => SetValue(AyudaProperty, value);
    }

    public static readonly DependencyProperty AyudaProperty =
        DependencyProperty.Register(nameof(Ayuda), typeof(string), typeof(CampoCombo),
            new PropertyMetadata(string.Empty, (d, _) =>
            {
                var campo = (CampoCombo)d;
                campo.AplicarEstado(EstadoCampo.Neutro);
                campo.AplicarAccesibilidad();
            }));

    /// <summary>Longitud máxima de un valor escrito manualmente.</summary>
    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    /// <remarks>
    /// AutoSuggestBox no expone MaxLength: esa propiedad pertenece al TextBox
    /// que lleva dentro de su plantilla, al que no se puede llegar de forma
    /// fiable. El límite se aplica recortando el texto en cuanto el usuario
    /// escribe, que además cubre el pegado desde el portapapeles.
    /// </remarks>
    public static readonly DependencyProperty MaxLengthProperty =
        DependencyProperty.Register(nameof(MaxLength), typeof(int), typeof(CampoCombo),
            new PropertyMetadata(500, (d, _) => ((CampoCombo)d).AplicarLimite()));

    public bool Obligatorio
    {
        get => (bool)GetValue(ObligatorioProperty);
        set => SetValue(ObligatorioProperty, value);
    }

    public static readonly DependencyProperty ObligatorioProperty =
        DependencyProperty.Register(nameof(Obligatorio), typeof(bool), typeof(CampoCombo),
            new PropertyMetadata(true, (d, _) => ((CampoCombo)d).RefrescarEtiqueta()));

    /// <summary>True = autocompletado inteligente; False = lista cerrada.</summary>
    public bool Editable
    {
        get => (bool)GetValue(EditableProperty);
        set => SetValue(EditableProperty, value);
    }

    public static readonly DependencyProperty EditableProperty =
        DependencyProperty.Register(nameof(Editable), typeof(bool), typeof(CampoCombo),
            new PropertyMetadata(false, (d, _) => ((CampoCombo)d).AplicarModo()));

    public string? Icono
    {
        get => (string?)GetValue(IconoProperty);
        set => SetValue(IconoProperty, value);
    }

    public static readonly DependencyProperty IconoProperty =
        DependencyProperty.Register(nameof(Icono), typeof(string), typeof(CampoCombo),
            new PropertyMetadata(null, (d, e) => ((CampoCombo)d).AplicarIcono(e.NewValue as string)));

    public string MensajeError { get; set; } = "Seleccione una opción.";

    public Func<string, bool>? Validacion { get; set; }

    public bool EsValido { get; private set; }

    public string Valor
    {
        get => Editable
            ? Sugerencias.Text.Trim()
            : (Lista.SelectedItem as string ?? string.Empty).Trim();
        set => EstablecerValor(value);
    }

    public void EstablecerOpciones(IEnumerable<string> opciones)
    {
        _opciones.Clear();
        _opciones.AddRange(opciones);
        Lista.ItemsSource = _opciones.ToList();
    }

    public void EstablecerValor(string? texto)
    {
        texto ??= string.Empty;
        if (Editable)
        {
            Sugerencias.Text = texto;
        }
        else
        {
            Lista.SelectedItem = _opciones.FirstOrDefault(
                o => string.Equals(o, texto, StringComparison.Ordinal));
        }

        Validar();
    }

    public void EstablecerValorSilencioso(string? texto)
    {
        _silenciado = true;
        try
        {
            EstablecerValor(texto);
        }
        finally
        {
            _silenciado = false;
        }
    }

    public void Limpiar()
    {
        _silenciado = true;
        try
        {
            Lista.SelectedIndex = -1;
            Sugerencias.Text = string.Empty;
        }
        finally
        {
            _silenciado = false;
        }

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

    /// <summary>Destello verde de sincronización (ui/estilos.py: destello_sync).</summary>
    public void DestellarSincronizacion()
    {
        if (Editable)
        {
            EfectoDestello.Aplicar(Sugerencias);
            return;
        }

        EfectoDestello.Aplicar(Lista);
    }

    public void Enfocar()
    {
        if (Editable)
        {
            Sugerencias.Focus(FocusState.Programmatic);
        }
        else
        {
            Lista.Focus(FocusState.Programmatic);
        }
    }

    // ──────────────────────────── Internos ────────────────────────────

    private void RefrescarEtiqueta()
    {
        Etiqueta.Text = Obligatorio ? $"{Titulo}  *" : Titulo;
        AplicarAccesibilidad();
    }

    private void AplicarModo()
    {
        Lista.Visibility = Editable ? Visibility.Collapsed : Visibility.Visible;
        Sugerencias.Visibility = Editable ? Visibility.Visible : Visibility.Collapsed;
        AplicarLimite();
        AplicarIcono(Icono);
        AplicarAccesibilidad();
    }

    /// <summary>
    /// Recorta el texto del autocompletado al máximo permitido.
    /// </summary>
    /// <remarks>
    /// Se hace aquí y no con la propiedad MaxLength del control porque
    /// AutoSuggestBox no la tiene. Recortar en el evento cubre tanto lo que se
    /// teclea como lo que se pega de golpe.
    /// </remarks>
    private void AplicarLimite()
    {
        var maximo = Math.Max(1, MaxLength);
        if (Sugerencias.Text.Length > maximo)
        {
            Sugerencias.Text = Sugerencias.Text[..maximo];
        }
    }

    private void AplicarAccesibilidad()
    {
        var nombre = string.IsNullOrWhiteSpace(Titulo) ? "Campo de selección" : Titulo;
        AutomationProperties.SetName(Lista, nombre);
        AutomationProperties.SetName(Sugerencias, nombre);
        AutomationProperties.SetHelpText(Lista, Ayuda ?? string.Empty);
        AutomationProperties.SetHelpText(Sugerencias, Ayuda ?? string.Empty);
    }

    /// <summary>
    /// El icono acompana a la etiqueta, de modo que se muestra igual en la
    /// lista cerrada y en la lista con autocompletado, y ningun control
    /// necesita relleno calculado a mano.
    /// </summary>
    private void AplicarIcono(string? nombre)
    {
        var valido = !string.IsNullOrWhiteSpace(nombre);
        IconoLider.Visibility = valido ? Visibility.Visible : Visibility.Collapsed;
        if (valido)
        {
            IconoLider.Nombre = nombre!;
        }
    }

    private void AlCambiarSeleccion(object sender, SelectionChangedEventArgs e)
    {
        Validar();
        if (!_silenciado)
        {
            Cambiado?.Invoke(this, EventArgs.Empty);
        }
    }

    private void AlEscribirSugerencia(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            AplicarLimite();
            sender.ItemsSource = Filtrar(sender.Text);
        }

        Validar();
        if (!_silenciado)
        {
            Cambiado?.Invoke(this, EventArgs.Empty);
        }
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Los controladores declarados en XAML deben ser miembros de instancia.")]
    private void AlElegirSugerencia(
        AutoSuggestBox sender,
        AutoSuggestBoxSuggestionChosenEventArgs args)
        => sender.Text = args.SelectedItem as string ?? string.Empty;

    /// <summary>
    /// Filtrado equivalente al del original: sin distinguir mayusculas ni
    /// tildes, priorizando las coincidencias por prefijo sobre las internas.
    /// </summary>
    private List<string> Filtrar(string consulta)
    {
        var normalizada = Normalizar(consulta);
        if (string.IsNullOrEmpty(normalizada))
        {
            return _opciones.ToList();
        }

        var prefijo = new List<string>();
        var interna = new List<string>();

        foreach (var opcion in _opciones)
        {
            var candidata = Normalizar(opcion);
            if (candidata.StartsWith(normalizada, StringComparison.Ordinal))
            {
                prefijo.Add(opcion);
            }
            else if (candidata.Contains(normalizada, StringComparison.Ordinal))
            {
                interna.Add(opcion);
            }
        }

        prefijo.AddRange(interna);
        return prefijo;
    }

    /// <summary>Equivalente de <c>ui/widgets.py: _normalizar_busqueda</c>.</summary>
    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var descompuesto = texto.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var salida = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                salida.Append(caracter);
            }
        }

        return salida.ToString().Normalize(NormalizationForm.FormC);
    }

    private void Validar()
    {
        var texto = Valor;
        if (string.IsNullOrEmpty(texto))
        {
            EsValido = !Obligatorio;
            AplicarEstado(EstadoCampo.Neutro);
            return;
        }

        var ok = Validacion?.Invoke(texto) ?? true;
        EsValido = ok;
        AplicarEstado(ok ? EstadoCampo.Valido : EstadoCampo.Invalido,
                      ok ? "Correcto" : MensajeError);
    }

    private void AplicarEstado(EstadoCampo estado, string? mensaje = null)
    {
        mensaje ??= Ayuda;

        if (estado == EstadoCampo.Invalido)
        {
            var borde = CampoTexto.Recurso("Ga.Error");
            Lista.BorderBrush = borde;
            Sugerencias.BorderBrush = borde;
        }
        else
        {
            // Devuelve el borde a cada control para que conserve sus estados
            // nativos de puntero, foco y deshabilitado.
            Lista.ClearValue(Control.BorderBrushProperty);
            Sugerencias.ClearValue(Control.BorderBrushProperty);
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
