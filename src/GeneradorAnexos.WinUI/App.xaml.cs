using System;
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
    /// restos en la carpeta de instalación.
    /// </summary>
    private const string NombreMutex = "GeneradorAnexos.MPO.OTI";

    /// <summary>Se conserva viva durante todo el proceso a propósito.</summary>
    private static Mutex? _mutexInstancia;

    private VentanaPrincipal? _ventana;

    public App()
    {
        InitializeComponent();

        DeclararInstanciaEnEjecucion();

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
    /// Publica un mutex con nombre para que el instalador sepa que la
    /// aplicación está en ejecución. Si algo falla, se ignora: es solo una
    /// ayuda para el instalador y nunca debe impedir que el programa arranque.
    /// </summary>
    private static void DeclararInstanciaEnEjecucion()
    {
        try
        {
            _mutexInstancia = new Mutex(initiallyOwned: false, NombreMutex);
        }
        catch (Exception excepcion)
        {
            Registro.Error("APP_MUTEX_FAILED", excepcion);
        }
    }

    private void AlErrorNoControlado(
        object sender,
        Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Registro.Error("Error no controlado", e.Exception);

        // El original mostraba un QMessageBox critico con un texto fijo.
        e.Handled = true;
        _ = ServicioDialogos.MostrarErrorAsync(
            "Error inesperado",
            "Ocurrió un error inesperado y la acción no pudo completarse." + Environment.NewLine +
            Environment.NewLine +
            "El detalle quedó registrado para soporte. Si el problema persiste, " +
            "comuníquese con la OTI.");
    }
}
