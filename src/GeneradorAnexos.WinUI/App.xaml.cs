using System;
using System.Runtime.InteropServices;
using System.Threading;
using GeneradorAnexos.WinUI.Services;
using GeneradorAnexos.WinUI.Views;
using Microsoft.UI.Xaml;

namespace GeneradorAnexos.WinUI;

/// <summary>
/// Punto de entrada. Equivalente a <c>main.py</c> del aplicativo Python:
/// configura el registro, instala el manejo global de errores no controlados y
/// verifica las plantillas antes de mostrar la ventana principal.
/// </summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    /// <summary>
    /// Nombre del mutex que declara el instalador en <c>AppMutex</c>. Mientras
    /// el programa esté abierto, el instalador y el desinstalador lo detectan y
    /// piden cerrarlo, en lugar de tropezar con archivos bloqueados y dejar
    /// restos en la carpeta de instalación. También impide una segunda instancia.
    /// </summary>
    private const string NombreMutex = "GeneradorAnexos.MPO.OTI";

    private const uint MensajeAceptar = 0x00000000;
    private const uint IconoInformacion = 0x00000040;
    private const uint PrimerPlano = 0x00010000;

    /// <summary>Se conserva viva durante todo el proceso a propósito.</summary>
    private static Mutex? _mutexInstancia;

    private VentanaPrincipal? _ventana;

    public App()
    {
        if (!ReclamarInstanciaUnica())
        {
            AvisarInstanciaExistente();
            Environment.Exit(0);
            return;
        }

        InitializeComponent();

        // main.py: sys.excepthook -> registra y avisa sin cerrar en silencio.
        UnhandledException += AlErrorNoControlado;
    }

    /// <summary>Ventana principal activa; la usan los dialogos para su XamlRoot.</summary>
    public static VentanaPrincipal? Ventana { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Registro.Configurar();
        Registro.Info("APP_START");

        // Restos de vistas previas de sesiones que terminaron de forma anormal.
        GestorVistasPrevias.LimpiarHuerfanos();

        // Debe ir antes de generar cualquier documento: decide si se usan las
        // plantillas actualizadas del usuario o las incluidas en la instalación.
        Services.Actualizaciones.ServicioPlantillas.Inicializar();

        _ventana = new VentanaPrincipal();
        Ventana = _ventana;
        _ventana.Activate();
    }

    /// <summary>
    /// Toma el mutex con nombre. Si otra instancia ya lo posee, no arranca.
    /// Si el mutex falla por otra causa, se permite el arranque: no debe
    /// impedir el uso del programa.
    /// </summary>
    private static bool ReclamarInstanciaUnica()
    {
        try
        {
            _mutexInstancia = new Mutex(initiallyOwned: true, NombreMutex, out var creada);
            if (creada)
            {
                return true;
            }

            _mutexInstancia.Dispose();
            _mutexInstancia = null;
            return false;
        }
        catch (AbandonedMutexException)
        {
            // La instancia anterior murió sin soltar el mutex. Esta lo hereda.
            return true;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static void AvisarInstanciaExistente()
    {
        try
        {
            _ = MessageBoxW(
                IntPtr.Zero,
                "Generador de Anexos ya está abierto. Use esa ventana.",
                "Generador de Anexos",
                MensajeAceptar | IconoInformacion | PrimerPlano);
        }
        catch (Exception)
        {
            // Sin escritorio no hay aviso, pero igual no se abre otra instancia.
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int MessageBoxW(IntPtr hWnd, string texto, string titulo, uint tipo);

    private void AlErrorNoControlado(
        object sender,
        Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Registro.Error("Error no controlado", e.Exception);

        e.Handled = true;
        _ = ServicioDialogos.MostrarErrorAsync(
            "Error inesperado",
            "Ocurrió un error inesperado y la acción no pudo completarse." + Environment.NewLine +
            Environment.NewLine +
            "El detalle quedó registrado para soporte. Si el problema persiste, " +
            "comuníquese con la OTI.");
    }
}
