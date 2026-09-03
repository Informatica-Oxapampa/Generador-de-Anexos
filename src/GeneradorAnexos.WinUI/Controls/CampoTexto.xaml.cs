using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using GeneradorAnexos.WinUI.Services;

namespace GeneradorAnexos.WinUI.Controls;

/// <summary>
/// Equivalente de <c>ui/widgets.py: CampoTexto</c> (sobre CampoFormulario).
/// </summary>
/// <remarks>
/// Reproduce la maquina de estados del original:
/// <list type="bullet">
///   <item>vacio: sin estado, se muestra el texto de ayuda;</item>
///   <item>valido: borde y fondo verdes, icono check-circle y "Correcto";</item>
///   <item>invalido: borde y fondo rojos y el mensaje de error del campo.</item>
/// </list>
/// El filtro de teclado del original (<c>QValidator</c>) se implementa
/// revirtiendo el texto cuando la escritura no cumple el patron, que es el
/// equivalente mas cercano en WinUI: no existe un <c>Validator</c> nativo.
/// </remarks>
public sealed partial class CampoTexto : UserControl, ICampo
{
    private string _textoAnterior = string.Empty;
    private bool _formateando;
    private bool _silenciado;

    public CampoTexto()
    {
        InitializeComponent();
        Loaded += (_, _) => RefrescarEtiqueta();
    }

    /// <summary>Se dispara cuando el usuario edita el campo (no al escribir por codigo).</summary>
    public event EventHandler? Cambiado;

    /// <summary>Se dispara siempre que cambia el texto, incluso por codigo.</summary>
    public event EventHandler? TextoCambiado;

    // ─────────────────────────── Configuracion ───────────────────────────

    /// <summary>Rotulo del campo. El original le anade "  *" si es obligatorio.</summary>
    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(CampoTexto),
            new PropertyMetadata(string.Empty, (d, _) => ((CampoTexto)d).RefrescarEtiqueta()));

    public string Marcador
    {
        get => (string)GetValue(MarcadorProperty);
        set => SetValue(MarcadorProperty, value);
    }

    public static readonly DependencyProperty MarcadorProperty =
        DependencyProperty.Register(nameof(Marcador), typeof(string), typeof(CampoTexto),
            new PropertyMetadata(string.Empty,
                (d, e) => ((CampoTexto)d).Entrada.PlaceholderText =
                    e.NewValue as string ?? string.Empty));

    /// <summary>Texto de ayuda permanente bajo el campo.</summary>
    public string Ayuda
    {
        get => (string)GetValue(AyudaProperty);
        set => SetValue(AyudaProperty, value);
    }

    public static readonly DependencyProperty AyudaProperty =
        DependencyProperty.Register(nameof(Ayuda), typeof(string), typeof(CampoTexto),
            new PropertyMetadata(string.Empty, (d, _) => ((CampoTexto)d).AplicarEstado(EstadoCampo.Neutro)));

    public bool Obligatorio
    {
        get => (bool)GetValue(ObligatorioProperty);
        set => SetValue(ObligatorioProperty, value);
    }

    public static readonly DependencyProperty ObligatorioProperty =
        DependencyProperty.Register(nameof(Obligatorio), typeof(bool), typeof(CampoTexto),
            new PropertyMetadata(true, (d, _) => ((CampoTexto)d).RefrescarEtiqueta()));

    /// <summary>Nombre del icono lider dentro del campo.</summary>
    public string? Icono
    {
        get => (string?)GetValue(IconoProperty);
        set => SetValue(IconoProperty, value);
    }

    public static readonly DependencyProperty IconoProperty =
        DependencyProperty.Register(nameof(Icono), typeof(string), typeof(CampoTexto),
            new PropertyMetadata(null, (d, e) => ((CampoTexto)d).AplicarIcono(e.NewValue as string)));

    public string MensajeError { get; set; } = "Valor no válido.";

    /// <summary>Filtro de escritura: equivale al <c>QValidator</c> del original.</summary>
    public Func<string, bool>? FiltroTeclado { get; set; }

    /// <summary>Predicado de completitud: equivale a <c>func_validacion</c>.</summary>
    public Func<string, bool>? Validacion { get; set; }

    /// <summary>Reformateo en vivo: equivale a <c>formateador</c> (telefono).</summary>
    public Func<string, string>? Formateador { get; set; }

    public bool SoloLectura
    {
        get => Entrada.IsReadOnly;
        set
        {
            Entrada.IsReadOnly = value;
            Entrada.IsTabStop = !value;
        }
    }

    public int MaximoCaracteres
    {
        get => Entrada.MaxLength;
        set => Entrada.MaxLength = value;
    }

    /// <summary>
    /// Alto minimo opcional del cuadro. Nunca se fija un alto exacto: asi el
    /// campo puede crecer si Windows usa un tamano de texto mayor.
    /// </summary>
    public double AltoEntrada
    {
        get => Entrada.MinHeight;
        set => Entrada.MinHeight = value;
    }

    public TextBox Caja => Entrada;

    /// <summary>Muestra el teclado numérico en dispositivos táctiles de Windows.</summary>
    public void UsarTecladoNumerico()
    {
        var alcance = new InputScope();
        alcance.Names.Add(new InputScopeName(InputScopeNameValue.Number));
        Entrada.InputScope = alcance;
    }

    // ───────────────────────────── Estado ─────────────────────────────

    public bool EsValido { get; private set; }

    public string Valor
    {
        get => Entrada.Text.Trim();
        set => EstablecerValor(value);
    }

    /// <summary>Escribe sin emitir <see cref="Cambiado"/> (carga de borrador).</summary>
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

    public void EstablecerValor(string? texto) => Entrada.Text = texto ?? string.Empty;

    /// <summary>Equivalente de <c>limpiar()</c>.</summary>
    public void Limpiar()
    {
        Entrada.Text = string.Empty;
        EsValido = !Obligatorio;
        AplicarEstado(EstadoCampo.Neutro);
    }

    /// <summary>Equivalente de <c>forzar_validacion()</c>.</summary>
    public bool ForzarValidacion()
    {
        if (SoloLectura)
        {
            return true;
        }

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

    /// <summary>Destello verde de sincronizacion (ui/estilos.py: destello_sync).</summary>
    public void DestellarSincronizacion() => EfectoDestello.Aplicar(Entrada);

    // ──────────────────────────── Internos ────────────────────────────

    private void RefrescarEtiqueta()
        => Etiqueta.Text = Obligatorio ? $"{Titulo}  *" : Titulo;

    /// <summary>
    /// El icono acompana a la etiqueta. Al no ir superpuesto dentro del cuadro
    /// de texto, la entrada conserva el relleno nativo de WinUI y no hay que
    /// calcular margenes que se rompen al cambiar el escalado.
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

    private void AlCambiarTexto(object sender, TextChangedEventArgs e)
    {
        if (_formateando)
        {
            return;
        }

        // Filtro de escritura: revierte si el nuevo texto no cumple el patron.
        if (FiltroTeclado is not null && !FiltroTeclado(Entrada.Text))
        {
            _formateando = true;
            var posicion = Math.Max(0, Entrada.SelectionStart - 1);
            Entrada.Text = _textoAnterior;
            Entrada.SelectionStart = Math.Min(posicion, Entrada.Text.Length);
            _formateando = false;
            return;
        }

        _textoAnterior = Entrada.Text;

        if (Formateador is not null)
        {
            Reformatear();
        }

        Validar();
        TextoCambiado?.Invoke(this, EventArgs.Empty);

        if (!_silenciado)
        {
            Cambiado?.Invoke(this, EventArgs.Empty);
        }
    }

    private void AlPerderFoco(object sender, RoutedEventArgs e)
    {
        if (Formateador is not null)
        {
            Reformatear();
        }
    }

    /// <summary>
    /// Reformatea conservando la posicion del cursor por numero de digitos,
    /// igual que <c>CampoTexto._reformatear</c> del original.
    /// </summary>
    private void Reformatear()
    {
        var texto = Entrada.Text;
        var nuevo = Formateador!(texto);
        if (nuevo == texto)
        {
            return;
        }

        var digitosAntes = 0;
        var limite = Math.Clamp(Entrada.SelectionStart, 0, texto.Length);
        for (var i = 0; i < limite; i++)
        {
            if (char.IsDigit(texto[i]))
            {
                digitosAntes++;
            }
        }

        _formateando = true;
        Entrada.Text = nuevo;

        int posicion = 0, contados = 0;
        for (var i = 0; i < nuevo.Length; i++)
        {
            if (contados >= digitosAntes)
            {
                break;
            }

            posicion = i + 1;
            if (char.IsDigit(nuevo[i]))
            {
                contados++;
            }
        }

        Entrada.SelectionStart = Math.Min(posicion, nuevo.Length);
        _textoAnterior = nuevo;
        _formateando = false;
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

    /// <summary>
    /// Aplica el estado visual. Solo el error pinta el borde; en los demas
    /// estados se devuelve el borde al control con ClearValue para que
    /// conserve sus estados nativos de puntero, foco y deshabilitado. El
    /// acierto se comunica con el icono y el texto, sin tenir el campo: menos
    /// color y una lectura mas sobria.
    /// </summary>
    private void AplicarEstado(EstadoCampo estado, string? mensaje = null)
    {
        mensaje ??= Ayuda;

        if (estado == EstadoCampo.Invalido)
        {
            Entrada.BorderBrush = Recurso("Ga.Error");
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

    /// <summary>
    /// Pincel de la paleta para el tema activo. Pasa por <see cref="Paleta"/>
    /// para que respete el tema fijado en Configuración y no el de Windows.
    /// </summary>
    internal static Brush Recurso(string clave) => Paleta.Pincel(clave);
}

/// <summary>Estados visuales de un campo (propiedad <c>estado</c> del QSS original).</summary>
public enum EstadoCampo
{
    Neutro,
    Valido,
    Invalido,
}

/// <summary>Contrato comun de los campos del formulario.</summary>
public interface ICampo
{
    bool Obligatorio { get; set; }

    bool EsValido { get; }

    string Valor { get; set; }

    void Limpiar();

    bool ForzarValidacion();

    void Enfocar();
}
