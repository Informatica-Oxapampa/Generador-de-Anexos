using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneradorAnexos.WinUI.Services;
using GeneradorAnexos.WinUI.Services.Actualizaciones;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeneradorAnexos.WinUI.Views;

/// <summary>
/// Sección Configuración: apariencia, actualizaciones, datos y diagnóstico, y
/// «Acerca de».
/// </summary>
/// <remarks>
/// Todas las opciones se guardan en cuanto se cambian; no hay botón de
/// «Aplicar», igual que en la Configuración de Windows.
/// </remarks>
public sealed partial class PaginaConfiguracion : UserControl
{
    private readonly PreferenciasUi _preferencias = new();

    private bool _cargando = true;

    public PaginaConfiguracion()
    {
        InitializeComponent();

        VersionActual.Text = "v" + Constantes.AppVersion;
        AcercaNombre.Text = Constantes.AppNombre;
        AcercaVersion.Text = "v" + Constantes.AppVersion;
        AcercaEntidad.Text = Constantes.AppEntidad;
        AcercaOficina.Text = Constantes.AppOrganizacion;
        RutaDatos.Text = PreferenciasUi.RutaCarpeta;

        Loaded += (_, _) => Refrescar();
    }

    /// <summary>Relee las preferencias y actualiza lo que se muestra.</summary>
    public void Refrescar()
    {
        _cargando = true;
        try
        {
            OpcionesTema.SelectedIndex = ServicioTema.Modo switch
            {
                PreferenciasUi.TemaClaro => 1,
                PreferenciasUi.TemaOscuro => 2,
                _ => 0,
            };

            InterruptorAutomatico.IsOn = _preferencias.BuscarActualizaciones;
            UltimaComprobacion.Text = _preferencias.UltimaComprobacionLegible();
            VersionPlantillas.Text = ServicioPlantillas.VersionInstaladaTexto();

            if (string.IsNullOrWhiteSpace(EstadoActualizacion.Text))
            {
                EstadoActualizacion.Text = "Sin comprobar en esta sesión.";
            }

            ActualizarEstadoRegistro();
        }
        finally
        {
            _cargando = false;
        }
    }

    private void ActualizarEstadoRegistro()
    {
        var bytes = Registro.TamanoTotal();
        var kilos = bytes / 1024d;

        EstadoRegistro.Text = bytes <= 0
            ? "Todavía no se ha escrito ningún evento."
            : string.Format(
                CultureInfo.CurrentCulture,
                "{0:N0} KB. Rota automáticamente y conserva como máximo los cuatro archivos más recientes.",
                kilos);
    }

    // ═══════════════════════ Apariencia ═══════════════════════

    private void AlCambiarTema(object sender, SelectionChangedEventArgs e)
    {
        if (_cargando)
        {
            return;
        }

        var modo = OpcionesTema.SelectedIndex switch
        {
            1 => PreferenciasUi.TemaClaro,
            2 => PreferenciasUi.TemaOscuro,
            _ => PreferenciasUi.TemaSistema,
        };

        ServicioTema.Aplicar(modo);
    }

    // ═══════════════════════ Actualizaciones ═══════════════════════

    private void AlCambiarBusquedaAutomatica(object sender, RoutedEventArgs e)
    {
        if (_cargando)
        {
            return;
        }

        _preferencias.BuscarActualizaciones = InterruptorAutomatico.IsOn;
    }

    private async void AlBuscarActualizaciones(object sender, RoutedEventArgs e)
        => await BuscarAsync();

    /// <summary>
    /// Comprobación manual: siempre informa del resultado, incluso cuando no
    /// hay nada nuevo, porque el usuario acaba de pedirla expresamente.
    /// </summary>
    private async Task BuscarAsync()
    {
        BotonBuscar.IsEnabled = false;
        AnilloBusqueda.Visibility = Visibility.Visible;
        AnilloBusqueda.IsActive = true;
        EstadoActualizacion.Text = "Comprobando…";

        try
        {
            var resultado = await ServicioActualizaciones.ComprobarAsync(CancellationToken.None);

            _preferencias.MarcarComprobacion();
            UltimaComprobacion.Text = _preferencias.UltimaComprobacionLegible();

            if (resultado.Estado != EstadoComprobacion.Correcta)
            {
                EstadoActualizacion.Text = "No se pudo comprobar.";
                await ServicioDialogos.MostrarAdvertenciaAsync(
                    "No se pudo comprobar",
                    resultado.Mensaje + Environment.NewLine + Environment.NewLine
                    + "Puede seguir usando la aplicación con normalidad.");
                return;
            }

            var app = resultado.AppPendiente(out _);
            var plantillas = resultado.PlantillasPendientes(
                ServicioPlantillas.VersionInstalada(), out var versionPlantillas);

            if (app is null && plantillas is null)
            {
                EstadoActualizacion.Text = "La aplicación está actualizada.";
                await ServicioDialogos.MostrarInformacionAsync(
                    "Sin actualizaciones",
                    "Ya tiene la versión más reciente del programa ("
                    + Constantes.AppVersion + ") y de las plantillas ("
                    + ServicioPlantillas.VersionInstalada() + ").");
                return;
            }

            EstadoActualizacion.Text = app is not null
                ? "Actualización disponible: v" + app.Version
                : "Plantillas disponibles: v" + versionPlantillas;

            await AsistenteActualizacion.OfrecerAsync(resultado);
            VersionPlantillas.Text = ServicioPlantillas.VersionInstaladaTexto();
        }
        finally
        {
            AnilloBusqueda.IsActive = false;
            AnilloBusqueda.Visibility = Visibility.Collapsed;
            BotonBuscar.IsEnabled = true;
        }
    }

    private void AlVerNotas(object sender, RoutedEventArgs e)
        => Abrir(ConfiguracionActualizaciones.UrlVersiones);

    // ═══════════════════════ Datos y diagnóstico ═══════════════════════

    private void AlAbrirCarpetaDatos(object sender, RoutedEventArgs e)
        => Abrir(PreferenciasUi.RutaCarpeta);

    private async void AlAbrirRegistro(object sender, RoutedEventArgs e)
    {
        var ruta = Registro.RutaArchivo;

        if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta))
        {
            await ServicioDialogos.MostrarInformacionAsync(
                "Registro de diagnóstico",
                "Todavía no se ha escrito ningún evento en el registro.");
            return;
        }

        Abrir(ruta);
    }

    private async void AlVaciarRegistro(object sender, RoutedEventArgs e)
    {
        if (!await ServicioDialogos.PreguntarSiNoAsync(
                "Vaciar registro",
                "Se borrará el contenido del registro de diagnóstico."
                + Environment.NewLine + Environment.NewLine
                + "Es útil antes de reproducir un problema, para que el registro "
                + "contenga solo lo relacionado con él. ¿Desea continuar?"))
        {
            return;
        }

        if (Registro.Vaciar())
        {
            ActualizarEstadoRegistro();
            return;
        }

        await ServicioDialogos.MostrarAdvertenciaAsync(
            "No se pudo vaciar",
            "El registro está en uso por otro programa. Ciérrelo e inténtelo de nuevo.");
    }

    /// <summary>
    /// Comprueba la integridad de la base de registros.
    /// </summary>
    /// <remarks>
    /// Si detecta daño no intenta reparar por su cuenta: dirige al usuario a
    /// los respaldos, que es la vía segura. Una reparación automática sobre un
    /// archivo dañado puede consolidar la pérdida en lugar de evitarla.
    /// </remarks>
    private async void AlComprobarIntegridad(object sender, RoutedEventArgs e)
    {
        BotonIntegridad.IsEnabled = false;

        try
        {
            var problema = await ServiciosApp.Registros.ComprobarIntegridadAsync(CancellationToken.None);

            if (string.IsNullOrEmpty(problema))
            {
                Registro.Info("DB_INTEGRITY_OK");
                await ServicioDialogos.MostrarInformacionAsync(
                    "Base de datos correcta",
                    "La base de registros no presenta daños.");
                return;
            }

            Registro.Advertencia("DB_INTEGRITY_FAILED");
            await ServicioDialogos.MostrarErrorAsync(
                "La base de datos presenta problemas",
                "La comprobación encontró lo siguiente:" + Environment.NewLine + Environment.NewLine
                + problema + Environment.NewLine + Environment.NewLine
                + "Sus registros podrían estar dañados. Contacte con la Oficina de "
                + "Tecnología de la Información: existe una copia de seguridad reciente "
                + "en la carpeta de datos desde la que se puede recuperar la información.");
        }
        catch (Exception excepcion)
        {
            Registro.Error("DB_INTEGRITY_CHECK_FAILED", excepcion);
            await ServicioDialogos.MostrarAdvertenciaAsync(
                "No se pudo comprobar",
                "La base de datos está en uso o no se pudo abrir. Cierre otras "
                + "instancias del programa e inténtelo de nuevo.");
        }
        finally
        {
            BotonIntegridad.IsEnabled = true;
        }
    }

    private async void AlRestaurarPlantillas(object sender, RoutedEventArgs e)
    {
        if (!await ServicioDialogos.PreguntarSiNoAsync(
                "Restaurar plantillas incluidas",
                "Se eliminarán las plantillas descargadas y se volverá a usar las que "
                + "trajo el instalador." + Environment.NewLine + Environment.NewLine
                + "Es la salida si una actualización de plantillas diera problemas. "
                + "¿Desea continuar?"))
        {
            return;
        }

        if (ServicioPlantillas.RestaurarIncluidas())
        {
            VersionPlantillas.Text = ServicioPlantillas.VersionInstaladaTexto();
            await ServicioDialogos.MostrarInformacionAsync(
                "Plantillas restauradas",
                "Se están usando las plantillas incluidas con el programa.");
            return;
        }

        await ServicioDialogos.MostrarAdvertenciaAsync(
            "No se pudieron restaurar",
            "Alguna plantilla está abierta en Word. Ciérrela e inténtelo de nuevo.");
    }

    private async void AlRestablecer(object sender, RoutedEventArgs e)
    {
        if (!await ServicioDialogos.PreguntarSiNoAsync(
                "Restablecer preferencias",
                "Se devolverán a sus valores iniciales el tema, las carpetas recordadas "
                + "y las opciones de actualización." + Environment.NewLine + Environment.NewLine
                + "Sus registros guardados, respaldos y documentos NO se eliminan."
                + Environment.NewLine + Environment.NewLine + "¿Desea continuar?"))
        {
            return;
        }

        _preferencias.Restablecer();
        ServicioTema.Aplicar(PreferenciasUi.TemaSistema);
        Refrescar();

        await ServicioDialogos.MostrarInformacionAsync(
            "Preferencias restablecidas",
            "Las preferencias volvieron a sus valores iniciales.");
    }

    /// <summary>
    /// Abre una carpeta, un archivo o una dirección con la aplicación
    /// predeterminada de Windows. Nunca lanza: si falla, solo se registra.
    /// </summary>
    private static void Abrir(string destino)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = destino,
                UseShellExecute = true,
            });
        }
        catch (Exception excepcion)
        {
            Registro.Error("SHELL_OPEN_FAILED", excepcion);
        }
    }
}
