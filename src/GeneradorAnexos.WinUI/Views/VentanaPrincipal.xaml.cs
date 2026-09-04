using GaSync = GeneradorAnexos.Application.Sync;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GeneradorAnexos.Domain.Formatting;
using GeneradorAnexos.WinUI.Services;
using GeneradorAnexos.WinUI.Services.Actualizaciones;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace GeneradorAnexos.WinUI.Views;

/// <summary>
/// Ventana principal: barra de título integrada, navegación lateral y pila de
/// páginas (TDR, Anexos y Registros guardados).
/// </summary>
/// <remarks>
/// Comportamiento del rediseño respecto a la versión anterior:
/// <list type="bullet">
///   <item>La ventana es redimensionable y se adapta a la resolución y al
///         escalado del equipo; un tamaño mínimo impide que los controles
///         queden cortados.</item>
///   <item>El contenido se extiende sobre la barra de título, como en las
///         aplicaciones actuales de Windows, y se aplica el material Mica
///         cuando el sistema lo admite.</item>
///   <item>Se conservan la fecha automática del día, los atajos
///         Ctrl+L / Ctrl+P / Ctrl+S y el autoguardado cifrado cada 45 s.</item>
/// </list>
/// </remarks>
public sealed partial class VentanaPrincipal : Window
{
    private const int WindowLongWindowProcedure = -4;
    private const uint MensajeMinMaxInfo = 0x0024;

    /// <summary>Tamaño inicial en píxeles lógicos (se escala según el PPP).</summary>
    private const int AnchoObjetivo = 1180;

    private const int AltoObjetivo = 760;

    /// <summary>
    /// Tamaño mínimo en píxeles lógicos. Por debajo de estos valores los
    /// formularios de dos columnas empezarían a apretarse, así que Windows no
    /// permite reducir más la ventana.
    /// </summary>
    private const int AnchoMinimo = 940;

    private const int AltoMinimo = 620;

    /// <summary>Margen respecto al área útil del escritorio.</summary>
    private const int MargenEscritorio = 24;

    /// <summary>
    /// Primera compilación de Windows 11. El material Mica solo existe a partir
    /// de aquí; en Windows 10 hay que pintar superficies opacas.
    /// </summary>
    private const int CompilacionWindows11 = 22000;

    /// <summary>
    /// DWMWA_USE_IMMERSIVE_DARK_MODE. Permite que la barra de título estándar
    /// de Windows 10 siga el tema oscuro cuando el sistema no admite la
    /// personalización de barra de título del App SDK.
    /// </summary>
    private const int AtributoModoOscuro = 20;

    /// <summary>Ancho de reserva cuando el sistema no informa del inset real.</summary>
    private const double ReservaBotonesPorDefecto = 148;

    /// <summary>Periodo del autoguardado.</summary>
    private static readonly TimeSpan PeriodoAutoguardado = TimeSpan.FromSeconds(45);

    /// <summary>Título y descripción que muestra la cabecera de cada sección.</summary>
    private static readonly (string Titulo, string Descripcion)[] Secciones =
    {
        ("Términos de referencia",
         "Complete los datos del servicio para generar el TDR. Los campos marcados con * son obligatorios."),
        ("Anexos N° 06 al 09",
         "Complete los datos del proveedor para generar los anexos. Los campos marcados con * son obligatorios."),
        ("Registros guardados",
         "Guarde el formulario completo y recupérelo cuando lo necesite."),
        ("Configuración",
         "Apariencia, actualizaciones, datos y diagnóstico del programa."),
    };

    /// <summary>Índice de la sección Configuración dentro de <see cref="Secciones"/>.</summary>
    private const int SeccionConfiguracion = 3;

    /// <summary>
    /// Espera antes de comprobar actualizaciones al iniciar. Da tiempo a que la
    /// ventana termine de dibujarse: la comprobación nunca debe notarse en el
    /// arranque.
    /// </summary>
    private static readonly TimeSpan RetardoComprobacion = TimeSpan.FromSeconds(6);

    private readonly DispatcherTimer _temporizadorAutoguardado = new();
    private static readonly SemaphoreSlim SemaforoNuevoRegistro = new(1, 1);

    /// <summary>
    /// Vigila si el formulario cambió respecto a lo último guardado. Dos
    /// segundos es suficiente para que el aviso se sienta inmediato y el coste
    /// es despreciable: serializar el formulario son unos pocos kilobytes.
    /// </summary>
    private readonly DispatcherTimer _temporizadorCambios = new()
    {
        Interval = TimeSpan.FromSeconds(2),
    };

    /// <summary>Huella del formulario tal como quedó en el último guardado.</summary>
    private string _huellaGuardada = string.Empty;

    /// <summary>True cuando el usuario ya confirmó que quiere cerrar.</summary>
    private bool _cierreAutorizado;
    private bool _cierreEnCurso;
    private bool _huellaInvalida;
    private bool _borradorRecuperadoPendiente;

    /// <summary>
    /// Preferencias visuales de Windows. Se crea de forma perezosa porque en
    /// algunos equipos su construcción es lenta y no debe retrasar el arranque.
    /// </summary>
    private readonly Lazy<Windows.UI.ViewManagement.UISettings> _ajustesUi =
        new(() => new Windows.UI.ViewManagement.UISettings());

    /// <summary>Devuelve la tarjeta del registro activo a su estado normal.</summary>
    private readonly DispatcherTimer _temporizadorAvisoGuardado = new()
    {
        Interval = TimeSpan.FromSeconds(3),
    };

    private DateOnly _fechaDocumento = DateOnly.FromDateTime(DateTime.Now);

    /// <summary>
    /// Fecha con la que se guardó el registro que está abierto, si la tiene.
    /// </summary>
    /// <remarks>
    /// Es información histórica del registro, no la fecha del documento que se
    /// va a emitir. Se conserva solo para ofrecer «usar la fecha del registro»
    /// cuando se está reimprimiendo un documento tal como salió en su día.
    /// </remarks>
    private DateOnly? _fechaDelRegistro;
    private int _pestanaActiva;
    private bool _actualizandoNavegacion;
    private WindowProcedure? _windowProcedure;
    private nint _previousWindowProcedure;
    private readonly nint _manejador;

    public VentanaPrincipal()
    {
        InitializeComponent();

        Registro.Configurar();

        _manejador = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Se fija antes que nada: la barra de título y el fondo consultan
        // colores de la paleta, y esos colores dependen del tema activo.
        ServicioTema.Inicializar(Raiz);

        Title = Constantes.AppNombre;
        TituloApp.Text = Constantes.AppNombre;
        DescriptorApp.Text = Constantes.AppDescriptor;
        FechaValor.Text = DocumentFormatting.FormatPeruvianDate(_fechaDocumento);

        AplicarIcono();
        AplicarGeometriaInicial();
        AplicarFondoDelSistema();
        ConfigurarBarraTitulo();
        LimitarTamanoMinimo();
        EscucharCambiosDeWindows();

        if (AppWindow.Presenter is OverlappedPresenter presentador)
        {
            presentador.IsResizable = true;
            presentador.IsMaximizable = true;
            presentador.IsMinimizable = true;
        }

        Estado = new GaSync.EstadoCompartido();
        PaginaTdrVista.Inicializar(this, Estado);
        PaginaAnexosVista.Inicializar(this, Estado);
        PaginaUsuariosVista.Inicializar(this);

        ConectarSincronizacion();
        ConfigurarAtajos();
        CambiarPestana(0);
        ActualizarRegistroActivo(null);
        ProgramarComprobacionActualizaciones();

        _temporizadorAvisoGuardado.Tick += (_, _) =>
        {
            _temporizadorAvisoGuardado.Stop();
            RestaurarEtiquetaRegistro();
        };

        _huellaGuardada = HuellaFormulario();
        _temporizadorCambios.Tick += (_, _) => RefrescarAvisoDeCambios();
        _temporizadorCambios.Start();

        AppWindow.Closing += AlIntentarCerrar;

        _temporizadorAutoguardado.Interval = PeriodoAutoguardado;
        _temporizadorAutoguardado.Tick += async (_, _) => await AutoguardarAsync();
        _temporizadorAutoguardado.Start();

        Closed += AlCerrar;
    }

    /// <summary>Estado compartido TDR ↔ Anexos.</summary>
    public GaSync.EstadoCompartido Estado { get; }

    /// <summary>
    /// Fecha con la que se generan los documentos.
    /// </summary>
    /// <remarks>
    /// De forma predeterminada es la del día. Al cargar un registro guardado se
    /// restaura la fecha con la que se guardó, que es lo correcto para reimprimir
    /// un documento tal como se emitió. Pero si lo que se quiere es reutilizar
    /// ese registro para un trámite nuevo, esa fecha ya no sirve: por eso, cuando
    /// la fecha no es la de hoy, aparece el enlace «Actualizar a hoy».
    /// </remarks>
    public DateOnly FechaDocumento
    {
        get => _fechaDocumento;
        set
        {
            _fechaDocumento = value;
            FechaValor.Text = DocumentFormatting.FormatPeruvianDate(value);
            ActualizarAvisoDeFecha();
        }
    }

    /// <summary>
    /// Ofrece cambiar la fecha del documento, en el sentido que corresponda.
    /// </summary>
    /// <remarks>
    /// Al abrir un registro guardado, el documento toma la fecha de hoy, porque
    /// reutilizar un registro casi siempre significa emitir algo nuevo. El
    /// enlace ofrece entonces volver a la fecha del registro, que es lo que hace
    /// falta en el caso menos frecuente: reimprimir un documento tal como se
    /// emitió.
    ///
    /// Si por lo que sea la fecha del documento no es la de hoy, el enlace
    /// ofrece lo contrario. Un solo control cubre las dos situaciones y solo
    /// aparece cuando hay algo que ofrecer.
    /// </remarks>
    private void ActualizarAvisoDeFecha()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        if (_fechaDocumento != hoy)
        {
            BotonFechaHoy.Content = "Usar la fecha de hoy";
            BotonFechaHoy.Visibility = Visibility.Visible;
            ToolTipService.SetToolTip(
                TarjetaFecha,
                "El documento se generará con esta fecha, que no es la de hoy.");
            return;
        }

        if (_fechaDelRegistro is { } original && original != hoy)
        {
            BotonFechaHoy.Content =
                $"Usar la fecha del registro ({original:dd/MM/yyyy})";
            BotonFechaHoy.Visibility = Visibility.Visible;
            ToolTipService.SetToolTip(
                TarjetaFecha,
                $"El registro se guardó el {original:dd/MM/yyyy}. El documento nuevo "
                + "llevará la fecha de hoy; use el enlace si necesita reimprimirlo "
                + "con la fecha original.");
            return;
        }

        BotonFechaHoy.Visibility = Visibility.Collapsed;
        ToolTipService.SetToolTip(TarjetaFecha, "Fecha con la que se generarán los documentos.");
    }

    /// <summary>Alterna entre la fecha de hoy y la del registro abierto.</summary>
    private void AlActualizarFechaAHoy(object sender, RoutedEventArgs e)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        if (_fechaDocumento == hoy && _fechaDelRegistro is { } original)
        {
            FechaDocumento = original;
            Registro.Info("DOCUMENT_DATE_SET_RECORD");
            return;
        }

        FechaDocumento = hoy;
        Registro.Info("DOCUMENT_DATE_SET_TODAY");
    }

    // ═══════════════════════ Ventana, tema y fondo ═══════════════════════

    /// <summary>Factor de escala de la pantalla donde está la ventana.</summary>
    private double Escala()
    {
        try
        {
            var ppp = GetDpiForWindow(_manejador);
            return ppp <= 0 ? 1.0 : ppp / 96.0;
        }
        catch (EntryPointNotFoundException)
        {
            return 1.0;
        }
    }

    /// <summary>
    /// Tamaño inicial proporcional al escalado del equipo y acotado al área
    /// útil, de modo que la ventana entra igual en 1366x768 que en 4K.
    /// </summary>
    private void AplicarGeometriaInicial()
    {
        var escala = Escala();
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var disponible = area.WorkArea;

        var ancho = Math.Min(
            (int)(AnchoObjetivo * escala),
            Math.Max(1, disponible.Width - MargenEscritorio));
        var alto = Math.Min(
            (int)(AltoObjetivo * escala),
            Math.Max(1, disponible.Height - MargenEscritorio));

        AppWindow.Resize(new Windows.Graphics.SizeInt32(ancho, alto));
    }

    /// <summary>True solo en Windows 11.</summary>
    private static bool EsWindows11
        => Environment.OSVersion.Platform == PlatformID.Win32NT
           && Environment.OSVersion.Version.Major >= 10
           && Environment.OSVersion.Version.Build >= CompilacionWindows11;

    /// <summary>
    /// Aplica —o retira— el material del sistema según lo que permita el
    /// equipo y según lo que el usuario tenga configurado en Windows.
    /// </summary>
    /// <remarks>
    /// Se exigen tres condiciones, y las tres importan:
    ///
    /// <list type="number">
    ///   <item><b>Windows 11.</b> Comprobar solo
    ///   <c>MicaController.IsSupported()</c> no basta: en Windows 10 devuelve
    ///   true porque las API de composición existen desde la versión 1809,
    ///   aunque el material no llegue a dibujarse nunca.</item>
    ///
    ///   <item><b>Soporte real del equipo</b>, vía
    ///   <c>MicaController.IsSupported()</c>.</item>
    ///
    ///   <item><b>Efectos de transparencia activados por el usuario</b>, vía
    ///   <c>UISettings.AdvancedEffectsEnabled</c>. Es la misma preferencia que
    ///   Windows expone en Configuración › Personalización › Colores, y que
    ///   también se desactiva sola con «Reducir animaciones y efectos». Si el
    ///   usuario la apagó, la aplicación no debe forzar transparencias.</item>
    /// </list>
    ///
    /// Cuando no se aplica material, la raíz conserva el fondo declarado en el
    /// XAML con ThemeResource, que sigue al tema claro u oscuro por sí solo.
    /// El método es idempotente y se vuelve a llamar cuando el usuario cambia
    /// la preferencia con la aplicación abierta.
    /// </remarks>
    private void AplicarFondoDelSistema()
    {
        var permitido = EsWindows11 && EfectosDeTransparenciaActivados();

        try
        {
            if (permitido && MicaController.IsSupported())
            {
                if (SystemBackdrop is null)
                {
                    SystemBackdrop = new MicaBackdrop();
                }

                // Solo ahora se retira el fondo opaco: hay material que lo
                // sustituye.
                Raiz.Background = null;
                return;
            }
        }
        catch (Exception excepcion)
        {
            Registro.Error("BACKDROP_NOT_APPLIED", excepcion);
        }

        // Sin material: se devuelve el fondo del XAML (ThemeResource), que es
        // la superficie de ventana del propio Windows.
        SystemBackdrop = null;
        Raiz.ClearValue(Panel.BackgroundProperty);
    }

    /// <summary>
    /// Preferencia de Windows «Efectos de transparencia». Ante cualquier duda
    /// se asume activada, que es el valor predeterminado del sistema.
    /// </summary>
    private bool EfectosDeTransparenciaActivados()
    {
        try
        {
            return _ajustesUi.Value.AdvancedEffectsEnabled;
        }
        catch (Exception excepcion)
        {
            Registro.Advertencia("UISETTINGS_" + excepcion.GetType().Name);
            return true;
        }
    }

    /// <summary>
    /// Escucha los cambios de configuración visual de Windows para aplicarlos
    /// sin reiniciar: transparencia y color de acento.
    /// </summary>
    /// <remarks>
    /// Los eventos de <c>UISettings</c> llegan en un hilo secundario, así que
    /// el trabajo se devuelve a la cola de la interfaz antes de tocar nada.
    /// El cambio de tema claro/oscuro no necesita nada de esto: WinUI ya lo
    /// propaga solo a través de <c>ActualThemeChanged</c>.
    /// </remarks>
    private void EscucharCambiosDeWindows()
    {
        try
        {
            var ajustes = _ajustesUi.Value;

            ajustes.AdvancedEffectsEnabledChanged += (_, _) =>
                DispatcherQueue.TryEnqueue(() =>
                {
                    AplicarFondoDelSistema();
                    Registro.Info("TRANSPARENCY_PREFERENCE_CHANGED");
                });

            ajustes.ColorValuesChanged += (_, _) =>
                DispatcherQueue.TryEnqueue(() =>
                {
                    AplicarTemaBarraTitulo();
                    Registro.Info("SYSTEM_COLORS_CHANGED");
                });
        }
        catch (Exception excepcion)
        {
            Registro.Advertencia("UISETTINGS_EVENTS_" + excepcion.GetType().Name);
        }
    }

    private void AplicarIcono()
    {
        try
        {
            AppWindow.SetIcon("Assets\\logo.ico");
        }
        catch (ArgumentException)
        {
            Registro.Advertencia("APP_ICON_NOT_FOUND");
        }
    }

    /// <summary>
    /// Extiende el contenido sobre la barra de título y deja los botones de
    /// ventana en manos de Windows.
    /// </summary>
    private void ConfigurarBarraTitulo()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            // Windows 10 antiguo: se conserva la barra de título estándar y la
            // franja propia pasa a ser una cabecera más de la aplicación. Se
            // pide a DWM que pinte esa barra en oscuro cuando corresponda, para
            // que no quede con los colores del tema contrario.
            ReservaBotonesVentana.Width = 0;
            AplicarModoOscuroBarraEstandar();
            Raiz.ActualThemeChanged += (_, _) =>
            {
                AplicarModoOscuroBarraEstandar();
                Registro.Info("THEME_CHANGED");
            };
            return;
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(BarraTitulo);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        AplicarTemaBarraTitulo();
        AjustarReservaBotones();

        Raiz.Loaded += (_, _) =>
        {
            AplicarTemaBarraTitulo();
            AjustarReservaBotones();
        };

        Raiz.ActualThemeChanged += (_, _) =>
        {
            AplicarTemaBarraTitulo();
            Registro.Info("THEME_CHANGED");
        };

        Raiz.SizeChanged += (_, _) => AjustarReservaBotones();
    }

    /// <summary>
    /// El fondo de los botones de ventana se deja transparente para que se vea
    /// el material del sistema; el color del glifo sigue al tema activo.
    /// </summary>
    private void AplicarTemaBarraTitulo()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var barra = AppWindow.TitleBar;
        var texto = ColorRecurso("Ga.TextoFuerte");
        var tenue = ColorRecurso("Ga.TextoTenue");

        barra.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        barra.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        barra.ButtonForegroundColor = texto;
        barra.ButtonInactiveForegroundColor = tenue;

        // Los estados hover/pressed quedan en manos del sistema para conservar
        // detalles nativos como el fondo rojo del botón Cerrar.
        barra.ButtonHoverBackgroundColor = null;
        barra.ButtonHoverForegroundColor = null;
        barra.ButtonPressedBackgroundColor = null;
        barra.ButtonPressedForegroundColor = null;
    }

    /// <summary>
    /// Hace que la barra de título estándar siga el tema oscuro en las
    /// versiones de Windows 10 que no admiten la personalización del App SDK.
    /// </summary>
    private void AplicarModoOscuroBarraEstandar()
    {
        try
        {
            var oscuro = Raiz.ActualTheme == ElementTheme.Dark ? 1 : 0;
            _ = DwmSetWindowAttribute(
                _manejador,
                AtributoModoOscuro,
                ref oscuro,
                sizeof(int));
        }
        catch (EntryPointNotFoundException)
        {
            // Compilaciones anteriores a 1809: se conserva la barra clara.
            Registro.Advertencia("TITLEBAR_DARK_MODE_UNAVAILABLE");
        }
        catch (DllNotFoundException)
        {
            Registro.Advertencia("TITLEBAR_DARK_MODE_UNAVAILABLE");
        }
    }

    /// <summary>
    /// Reserva a la derecha de la barra el ancho real que ocupan los botones
    /// de ventana, que cambia con el PPP y con el idioma del sistema.
    /// </summary>
    private void AjustarReservaBotones()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var escala = Raiz.XamlRoot?.RasterizationScale ?? Escala();
        if (escala <= 0)
        {
            escala = 1.0;
        }

        var inset = AppWindow.TitleBar.RightInset / escala;

        // Si el sistema todavía no informa del inset (ocurre antes del primer
        // dibujado y en algunas compilaciones de Windows 10), se reserva un
        // ancho prudente para que el texto no quede bajo los botones.
        ReservaBotonesVentana.Width = inset > 0
            ? inset + 8
            : ReservaBotonesPorDefecto;
    }

    private static Windows.UI.Color ColorRecurso(string clave) => Paleta.Color(clave);

    /// <summary>
    /// Estilo de los recursos de la aplicación. Se aplican estilos y no
    /// pinceles para que el color siga al tema activo.
    /// </summary>
    private static Style Estilo(string clave)
        => (Style)Microsoft.UI.Xaml.Application.Current.Resources[clave];

    /// <summary>
    /// Impone el tamaño mínimo de la ventana interceptando WM_GETMINMAXINFO.
    /// Es la vía que Windows respeta en todos los modos de arrastre y en el
    /// acoplamiento lateral.
    /// </summary>
    private void LimitarTamanoMinimo()
    {
        try
        {
            _windowProcedure = ProcesarMensajeVentana;
            var pointer = Marshal.GetFunctionPointerForDelegate(_windowProcedure);

            Marshal.SetLastPInvokeError(0);
            _previousWindowProcedure = SetWindowLongPtrW(
                _manejador,
                WindowLongWindowProcedure,
                pointer);

            if (_previousWindowProcedure == 0 && Marshal.GetLastPInvokeError() != 0)
            {
                _windowProcedure = null;
                Registro.Advertencia("MIN_SIZE_HOOK_FAILED");
            }
        }
        catch (Exception excepcion)
        {
            _windowProcedure = null;
            Registro.Error("MIN_SIZE_HOOK_FAILED", excepcion);
        }
    }

    private nint ProcesarMensajeVentana(
        nint window,
        uint message,
        nuint wParam,
        nint lParam)
    {
        if (message == MensajeMinMaxInfo && lParam != 0)
        {
            var escala = Escala();
            var informacion = Marshal.PtrToStructure<InformacionMinMax>(lParam);
            informacion.MinimoArrastre.X = (int)(AnchoMinimo * escala);
            informacion.MinimoArrastre.Y = (int)(AltoMinimo * escala);
            Marshal.StructureToPtr(informacion, lParam, false);
            return 0;
        }

        return _previousWindowProcedure != 0
            ? CallWindowProcW(_previousWindowProcedure, window, message, wParam, lParam)
            : DefWindowProcW(window, message, wParam, lParam);
    }

    // ═══════════════════════ Navegación ═══════════════════════

    private void AlCambiarSeccion(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (_actualizandoNavegacion)
        {
            return;
        }

        if (args.IsSettingsSelected)
        {
            CambiarPestana(SeccionConfiguracion);
            return;
        }

        if (args.SelectedItem is NavigationViewItem elemento
            && elemento.Tag is string etiqueta
            && int.TryParse(etiqueta, NumberStyles.Integer, CultureInfo.InvariantCulture, out var indice))
        {
            CambiarPestana(indice);
        }
    }

    private void AlInvocarElemento(
        NavigationView sender,
        NavigationViewItemInvokedEventArgs args)
    {
        if (ReferenceEquals(args.InvokedItemContainer, ElementoGuardar))
        {
            _ = PaginaUsuariosVista.GuardarActualAsync();
            return;
        }

        if (ReferenceEquals(args.InvokedItemContainer, ElementoNuevo))
        {
            _ = NuevoRegistroAsync();
        }
    }

    // ═══════════════════════ Registro de trabajo ═══════════════════════

    /// <summary>
    /// Empieza un registro nuevo: solicita su nombre y, solo después de
    /// confirmarlo, limpia los formularios y crea el registro activo.
    /// </summary>
    public async Task NuevoRegistroAsync()
    {
        if (!await SemaforoNuevoRegistro.WaitAsync(0))
        {
            return;
        }

        try
        {
            // Si no hay nada que perder, no se pregunta nada: limpiar un
            // formulario ya guardado es inocuo y una confirmación de más solo
            // estorba.
            if (HayCambiosSinGuardar() && !await ConfirmarDescarteAsync(
                    "empezar un registro nuevo",
                    "Guardar y continuar",
                    "Continuar sin guardar"))
            {
                return;
            }

            await PaginaUsuariosVista.CrearRegistroNuevoAsync(() =>
            {
                PaginaTdrVista.LimpiarSilencioso();
                PaginaAnexosVista.LimpiarSilencioso();
                FechaDocumento = DateOnly.FromDateTime(DateTime.Now);
                CambiarPestana(0);
            });
        }
        finally
        {
            SemaforoNuevoRegistro.Release();
        }
    }

    /// <summary>
    /// Ofrece guardar los cambios pendientes antes de una acción que los
    /// descartaría.
    /// </summary>
    /// <returns><c>true</c> si se puede continuar con la acción.</returns>
    /// <remarks>
    /// Si no existe un registro activo, «Guardar» no crea uno implícitamente;
    /// la acción se detiene y el usuario puede elegir «Nuevo registro».
    /// </remarks>
    private async Task<bool> ConfirmarDescarteAsync(
        string accion,
        string textoGuardar,
        string textoDescartar)
    {
        var respuesta = await ServicioDialogos.PreguntarCambiosPendientesAsync(
            PaginaUsuariosVista.NombreRegistroActivo, accion, textoGuardar, textoDescartar);

        switch (respuesta)
        {
            case RespuestaCambios.Guardar:
                if (PaginaUsuariosVista.IdRegistroCargado is null)
                {
                    await PaginaUsuariosVista.GuardarComoAsync();
                }
                else
                {
                    await PaginaUsuariosVista.GuardarActualAsync();
                }
                return !HayCambiosSinGuardar();

            case RespuestaCambios.Descartar:
                return true;

            default:
                return false;
        }
    }

    /// <summary>Protege cualquier acción que vaya a sustituir el formulario actual.</summary>
    public Task<bool> ConfirmarCambiosAntesDeAsync(string accion)
        => HayCambiosSinGuardar()
            ? ConfirmarDescarteAsync(accion, "Guardar y continuar", "Continuar sin guardar")
            : Task.FromResult(true);

    /// <summary>
    /// Refresca la tarjeta que indica sobre qué registro se está trabajando.
    /// La llama la página de registros cada vez que cambia el registro activo.
    /// </summary>
    public void ActualizarRegistroActivo(string? nombre)
    {
        _temporizadorAvisoGuardado.Stop();
        RestaurarEtiquetaRegistro();

        var sinGuardar = string.IsNullOrWhiteSpace(nombre);

        RegistroActivoValor.Text = sinGuardar ? "Sin registro activo" : nombre!;
        RegistroActivoValor.Style = Estilo(
            sinGuardar ? "Ga.FechaValorTenue" : "Ga.FechaValor");

        ToolTipService.SetToolTip(
            TarjetaRegistroActivo,
            sinGuardar
                ? "Use «Nuevo registro» para crear un registro y asignarle un nombre."
                : $"Los cambios se guardarán en «{nombre}».");
    }

    /// <summary>
    /// Confirmación discreta de guardado: la tarjeta del registro activo pasa
    /// unos segundos a «Guardado» en verde, sin interrumpir al usuario con un
    /// cuadro de diálogo.
    /// </summary>
    public void NotificarGuardado(string nombre)
    {
        ActualizarRegistroActivo(nombre);
        _borradorRecuperadoPendiente = false;
        _huellaGuardada = HuellaFormulario();

        RegistroActivoEtiqueta.Text = "GUARDADO";
        RegistroActivoEtiqueta.Style = Estilo("Ga.FechaEtiquetaOk");
        IconoRegistroActivo.Style = Estilo("Ga.IconoOk");

        _temporizadorAvisoGuardado.Stop();
        _temporizadorAvisoGuardado.Start();
    }

    private void RestaurarEtiquetaRegistro()
    {
        RegistroActivoEtiqueta.Text = "REGISTRO ACTIVO";
        RegistroActivoEtiqueta.Style = Estilo("Ga.FechaEtiqueta");
        IconoRegistroActivo.Style = Estilo("Ga.IconoAcento");
    }

    /// <summary>Muestra la sección indicada (0 = TDR, 1 = Anexos, 2 = Registros).</summary>
    public void CambiarPestana(int indice)
    {
        if (indice < 0 || indice >= Secciones.Length)
        {
            return;
        }

        _pestanaActiva = indice;

        PaginaTdrVista.Visibility = indice == 0 ? Visibility.Visible : Visibility.Collapsed;
        PaginaAnexosVista.Visibility = indice == 1 ? Visibility.Visible : Visibility.Collapsed;
        PaginaUsuariosVista.Visibility = indice == 2 ? Visibility.Visible : Visibility.Collapsed;
        PaginaConfiguracionVista.Visibility =
            indice == SeccionConfiguracion ? Visibility.Visible : Visibility.Collapsed;

        TituloSeccion.Text = Secciones[indice].Titulo;
        DescripcionSeccion.Text = Secciones[indice].Descripcion;

        _actualizandoNavegacion = true;
        try
        {
            Navegacion.SelectedItem = indice switch
            {
                0 => SeccionTdr,
                1 => SeccionAnexos,
                2 => SeccionUsuarios,
                _ => Navegacion.SettingsItem,
            };
        }
        finally
        {
            _actualizandoNavegacion = false;
        }

        if (indice == 1)
        {
            PaginaAnexosVista.ActualizarResumenFormaPago();
        }

        if (indice == 2)
        {
            _ = PaginaUsuariosVista.RefrescarAsync();
        }

        if (indice == SeccionConfiguracion)
        {
            PaginaConfiguracionVista.Refrescar();
        }
    }

    // ═══════════════════════ Cambios sin guardar ═══════════════════════

    /// <summary>
    /// Huella del contenido actual del formulario.
    /// </summary>
    /// <remarks>
    /// Se compara el borrador serializado en lugar de escuchar el evento de
    /// cambio de cada campo: son decenas de campos repartidos en dos páginas,
    /// más tres tablas dinámicas, y bastaría olvidar uno para que el aviso
    /// mintiera. Comparar el resultado completo no puede equivocarse.
    /// </remarks>
    private string HuellaFormulario()
    {
        try
        {
            var huella = System.Text.Json.JsonSerializer.Serialize(RecolectarBorrador());
            _huellaInvalida = false;
            return huella;
        }
        catch (Exception excepcion)
        {
            Registro.Advertencia("FORM_FINGERPRINT_" + excepcion.GetType().Name);
            _huellaInvalida = true;
            return string.Empty;
        }
    }

    /// <summary>True si hay cambios que no se han guardado como registro.</summary>
    public bool HayCambiosSinGuardar()
    {
        var actual = HuellaFormulario();
        return _borradorRecuperadoPendiente || _huellaInvalida ||
               !string.Equals(actual, _huellaGuardada, StringComparison.Ordinal);
    }

    /// <summary>
    /// Marca el estado actual como guardado. Lo llama la página de registros
    /// al guardar o cargar.
    /// </summary>
    public void MarcarFormularioComoGuardado()
    {
        _borradorRecuperadoPendiente = false;
        _huellaGuardada = HuellaFormulario();
        RefrescarAvisoDeCambios();
    }

    /// <summary>Refleja en la cabecera si quedan cambios pendientes.</summary>
    private void RefrescarAvisoDeCambios()
    {
        if (_temporizadorAvisoGuardado.IsEnabled)
        {
            // Se está mostrando «GUARDADO»; no conviene pisarlo.
            return;
        }

        RegistroActivoEtiqueta.Text = HayCambiosSinGuardar()
            ? "REGISTRO ACTIVO · SIN GUARDAR"
            : "REGISTRO ACTIVO";
    }

    // ═══════════════════════ Cierre de la ventana ═══════════════════════

    /// <summary>
    /// Impide cerrar con trabajo sin guardar sin preguntar antes.
    /// </summary>
    /// <remarks>
    /// El autoguardado cada 45 s es una red de emergencia, no un sustituto de
    /// avisar: recupera el borrador al volver a abrir, pero el usuario que
    /// cierra la ventana no sabe que su trabajo se salvó.
    /// </remarks>
    private void AlIntentarCerrar(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_cierreAutorizado)
        {
            return;
        }

        args.Cancel = true;
        if (_cierreEnCurso)
        {
            return;
        }

        _ = ResolverCierreAsync();
    }

    private async Task ResolverCierreAsync()
    {
        _cierreEnCurso = true;
        try
        {
            if (HayCambiosSinGuardar() &&
                !await ConfirmarDescarteAsync("cerrar", "Guardar y cerrar", "Cerrar sin guardar"))
            {
                return;
            }

            await EliminarAutoguardadoAlFinalizarAsync();
            _cierreAutorizado = true;
            Close();
        }
        finally
        {
            if (!_cierreAutorizado)
            {
                _cierreEnCurso = false;
            }
        }
    }

    /// <summary>Comprueba cambios y prepara una salida iniciada por el actualizador.</summary>
    public async Task<bool> PrepararCierreSeguroAsync()
    {
        if (HayCambiosSinGuardar() &&
            !await ConfirmarDescarteAsync(
                "instalar la actualización", "Guardar e instalar", "Instalar sin guardar"))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Finaliza el cierre solo después de que el instalador firmado haya sido
    /// lanzado. Si UAC o la verificación fallan, el autoguardado se conserva.
    /// </summary>
    public async Task AutorizarCierrePorActualizacionAsync()
    {
        await EliminarAutoguardadoAlFinalizarAsync();
        _cierreAutorizado = true;
    }

    // ═══════════════════════ Actualizaciones ═══════════════════════

    /// <summary>
    /// Comprueba en segundo plano si hay una versión más reciente.
    /// </summary>
    /// <remarks>
    /// Tres cuidados deliberados:
    /// <list type="bullet">
    ///   <item>Se retrasa unos segundos para no competir con el arranque.</item>
    ///   <item>Si no hay red, si GitHub no responde o si el manifiesto no es
    ///         válido, no se muestra absolutamente nada: no poder comprobar no
    ///         es un problema del usuario.</item>
    ///   <item>Se respeta la versión que el usuario pidió omitir.</item>
    /// </list>
    /// </remarks>
    private void ProgramarComprobacionActualizaciones()
    {
        var preferencias = new PreferenciasUi();
        if (!preferencias.BuscarActualizaciones)
        {
            return;
        }

        var temporizador = new DispatcherTimer { Interval = RetardoComprobacion };
        temporizador.Tick += async (_, _) =>
        {
            temporizador.Stop();
            await ComprobarActualizacionesEnSegundoPlanoAsync(preferencias);
        };
        temporizador.Start();
    }

    private static async Task ComprobarActualizacionesEnSegundoPlanoAsync(PreferenciasUi preferencias)
    {
        try
        {
            var resultado = await ServicioActualizaciones.ComprobarAsync(CancellationToken.None);
            preferencias.MarcarComprobacion();

            if (resultado.Estado != EstadoComprobacion.Correcta)
            {
                return;
            }

            // El asistente decide qué ofrecer: primero el programa y, si no hay
            // nada pendiente ahí, las plantillas. También respeta la versión
            // que el usuario pidió omitir.
            await AsistenteActualizacion.OfrecerAsync(resultado);
        }
        catch (Exception excepcion)
        {
            // Comprobar es opcional: un fallo aquí nunca debe molestar al usuario.
            Registro.Error("UPDATE_BACKGROUND_FAILED", excepcion);
        }
    }

    // ═══════════════════════ Sincronización ═══════════════════════

    /// <summary>
    /// Conecta el estado compartido: la denominación del TDR alimenta al
    /// objeto, al cuadro y a la descripción de Anexos; el plazo y el número de
    /// pedido se sincronizan en ambos sentidos.
    /// </summary>
    private void ConectarSincronizacion()
    {
        PaginaTdrVista.ConectarEstado();
        PaginaAnexosVista.ConectarEstado();
    }

    // ═══════════════════════ Atajos de teclado ═══════════════════════

    private void ConfigurarAtajos()
    {
        Agregar(VirtualKey.L, () => Accion(AccionRapida.Limpiar));
        Agregar(VirtualKey.P, () => Accion(AccionRapida.VistaPrevia));
        Agregar(VirtualKey.S, () => _ = PaginaUsuariosVista.GuardarActualAsync());
        Agregar(VirtualKey.N, () => _ = NuevoRegistroAsync());
        Agregar(
            VirtualKey.S,
            () => _ = PaginaUsuariosVista.GuardarComoAsync(),
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);

        void Agregar(
            VirtualKey tecla,
            Action accion,
            VirtualKeyModifiers modificadores = VirtualKeyModifiers.Control)
        {
            var atajo = new KeyboardAccelerator
            {
                Key = tecla,
                Modifiers = modificadores,
            };
            atajo.Invoked += (_, args) =>
            {
                args.Handled = true;
                accion();
            };
            Raiz.KeyboardAccelerators.Add(atajo);
        }
    }

    /// <summary>Enruta el atajo a la página activa (TDR = 0, Anexos = 1).</summary>
    private void Accion(AccionRapida accion)
    {
        if (_pestanaActiva == 0)
        {
            switch (accion)
            {
                case AccionRapida.Generar: _ = PaginaTdrVista.GenerarAsync(); break;
                case AccionRapida.Limpiar: _ = PaginaTdrVista.LimpiarAsync(); break;
                case AccionRapida.VistaPrevia: _ = PaginaTdrVista.VistaPreviaAsync(); break;
            }

            return;
        }

        switch (accion)
        {
            case AccionRapida.Generar: _ = PaginaAnexosVista.GenerarAsync(); break;
            case AccionRapida.Limpiar: _ = PaginaAnexosVista.LimpiarAsync(); break;
            case AccionRapida.VistaPrevia: _ = PaginaAnexosVista.VistaPreviaAsync(); break;
        }
    }

    // ═══════════════════════ Autoguardado y cierre ═══════════════════════

    /// <summary>El autoguardado nunca debe interrumpir al usuario.</summary>
    private async Task AutoguardarAsync()
    {
        try
        {
            await PaginaUsuarios.AutoguardarAsync(RecolectarBorrador());
        }
        catch (Exception excepcion)
        {
            Registro.Advertencia("AUTOSAVE_FAILED");
            Registro.Error("AUTOSAVE_DETAIL", excepcion);
        }
    }

    /// <summary>Serializa toda la sesión (Anexos + TDR + estado de sincronización).</summary>
    public Domain.Models.BorradorPayloadV1 RecolectarBorrador() => new()
    {
        Fecha = _fechaDocumento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Anexos = PaginaAnexosVista.ExportarEstado(),
        Tdr = PaginaTdrVista.ExportarEstado(),
        SincronizacionPersonalizada = PaginaTdrVista.EstadoSincronizacion(),
    };

    /// <summary>Aplica un borrador completo a las dos páginas de formulario.</summary>
    /// <param name="conservarFecha">
    /// <c>true</c> al retomar el autoguardado, donde se está continuando el
    /// mismo trabajo y la fecha debe ser la que había. <c>false</c> al abrir un
    /// registro guardado: ahí se reutilizan los datos para emitir un documento
    /// nuevo, así que la fecha pasa a ser la de hoy y la del registro queda
    /// disponible en el enlace de la cabecera.
    /// </param>
    public void AplicarBorrador(
        Domain.Models.BorradorPayloadV1? datos,
        bool marcarComoGuardado = true,
        bool conservarFecha = true)
    {
        if (datos is null)
        {
            return;
        }

        var tieneFecha = DateOnly.TryParseExact(
            datos.Fecha,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var fecha);

        _fechaDelRegistro = tieneFecha ? fecha : null;

        // La fecha histórica del registro no se pisa nunca: vive en el propio
        // registro. Lo que cambia aquí es solo la fecha del documento a emitir.
        FechaDocumento = conservarFecha && tieneFecha
            ? fecha
            : DateOnly.FromDateTime(DateTime.Now);

        PaginaTdrVista.SilenciarSincronizacion(true);
        try
        {
            PaginaAnexosVista.ImportarEstado(datos.Anexos);
            PaginaTdrVista.ImportarEstado(datos.Tdr);
        }
        finally
        {
            PaginaTdrVista.SilenciarSincronizacion(false);
        }

        PaginaTdrVista.AplicarPersonalizado(datos.SincronizacionPersonalizada);
        PaginaAnexosVista.ActualizarResumenFormaPago();
        if (marcarComoGuardado)
        {
            MarcarFormularioComoGuardado();
        }
        else
        {
            _borradorRecuperadoPendiente = true;
            RefrescarAvisoDeCambios();
        }
    }

    private void AlCerrar(object sender, WindowEventArgs args)
    {
        _temporizadorAutoguardado.Stop();
        _temporizadorCambios.Stop();
        PaginaAnexosVista.LimpiarVistasPrevias();
        PaginaTdrVista.LimpiarVistasPrevias();

        AppWindow.Closing -= AlIntentarCerrar;
        if (_previousWindowProcedure != 0)
        {
            try
            {
                _ = SetWindowLongPtrW(
                    _manejador,
                    WindowLongWindowProcedure,
                    _previousWindowProcedure);
            }
            catch (Exception excepcion)
            {
                Registro.Error("MIN_SIZE_HOOK_RESTORE_FAILED", excepcion);
            }

            _previousWindowProcedure = 0;
            _windowProcedure = null;
        }
    }

    private static async Task EliminarAutoguardadoAlFinalizarAsync()
    {
        try
        {
            await ServiciosApp.Borradores.DeleteAutosaveAsync(default);
        }
        catch (Exception excepcion)
        {
            Registro.Error("AUTOSAVE_FINAL_CLEANUP_FAILED", excepcion);
        }
    }

    private enum AccionRapida
    {
        Generar,
        Limpiar,
        VistaPrevia,
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(
        nint window,
        uint message,
        nuint wParam,
        nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct PuntoNativo
    {
        public int X;
        public int Y;
    }

    /// <summary>Equivalente administrado de MINMAXINFO.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct InformacionMinMax
    {
        public PuntoNativo Reservado;
        public PuntoNativo TamanoMaximo;
        public PuntoNativo PosicionMaxima;
        public PuntoNativo MinimoArrastre;
        public PuntoNativo MaximoArrastre;
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint SetWindowLongPtrW(
        nint window,
        int index,
        nint newValue);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint CallWindowProcW(
        nint previousProcedure,
        nint window,
        uint message,
        nuint wParam,
        nint lParam);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint DefWindowProcW(
        nint window,
        uint message,
        nuint wParam,
        nint lParam);

    [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmSetWindowAttribute(
        nint window,
        int attribute,
        ref int value,
        int size);

    [DllImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetDpiForWindow(nint window);
}
