using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneradorAnexos.Application.Abstractions.Persistence;
using GeneradorAnexos.Domain.Documents;
using GeneradorAnexos.Domain.Models;
using GeneradorAnexos.WinUI.Services;
using GeneradorAnexos.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeneradorAnexos.WinUI.Views;

/// <summary>
/// Equivalente de <c>ui/tab_usuarios.py: PaginaUsuarios</c>.
/// </summary>
public sealed partial class PaginaUsuarios : UserControl
{
    private const int MaximoCaracteresBusqueda = 120;
    private static readonly SemaphoreSlim SemaforoAutoguardado = new(1, 1);
    private static readonly SemaphoreSlim SemaforoRefresco = new(1, 1);
    private static readonly SemaphoreSlim SemaforoOperacionesRegistro = new(1, 1);
    private readonly ObservableCollection<RegistroVista> _visibles = new();
    private List<RegistroVista> _todos = new();

    private VentanaPrincipal? _ventana;
    private long? _registroCargadoId;
    private string _registroCargadoNombre = string.Empty;

    public PaginaUsuarios()
    {
        InitializeComponent();
        ListaRegistros.ItemsSource = _visibles;
    }

    public void Inicializar(VentanaPrincipal ventana)
    {
        _ventana = ventana;
        _ = InicializarBaseAsync();
    }

    private async Task InicializarBaseAsync()
    {
        try
        {
            await ServiciosApp.Registros.InitializeAsync(default);
            await RefrescarAsync();
            await OfrecerRecuperarAsync();
        }
        catch (Exception excepcion)
        {
            Registro.Error("DB_INIT_FAILED", excepcion);
            await ServicioDialogos.MostrarErrorAsync(
                "Registros no disponibles",
                "No se pudo abrir o migrar la base de registros. No se guardarán cambios " +
                "hasta resolver el problema. Revise Datos y diagnóstico o contacte con la OTI.");
        }
    }

    // ═══════════════════════ Listado ═══════════════════════

    /// <summary>Equivalente de <c>refrescar</c>.</summary>
    public async Task RefrescarAsync()
    {
        await SemaforoRefresco.WaitAsync();
        try
        {
            var registros = await ServiciosApp.Registros.ListAsync(default);
            var vistas = new List<RegistroVista>(registros.Count);

            foreach (var resumen in registros)
            {
                vistas.Add(new RegistroVista
                {
                    Id = resumen.Id,
                    Nombre = resumen.Name,
                    Actualizado = resumen.UpdatedAt,
                    TieneTdr = resumen.HasTdr,
                    TieneAnexo = resumen.HasAnnex,
                });
            }

            _todos = vistas;
        }
        catch (Exception excepcion)
        {
            Registro.Error("DB_LIST_FAILED", excepcion);
            _todos = new List<RegistroVista>();
        }
        finally
        {
            SemaforoRefresco.Release();
        }

        AplicarFiltro(Buscador.Text);
        await ActualizarEstadoRespaldoAsync();
    }

    private void AlBuscar(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var consulta = sender.Text ?? string.Empty;
            if (consulta.Length > MaximoCaracteresBusqueda)
            {
                consulta = consulta[..MaximoCaracteresBusqueda];
                sender.Text = consulta;
            }

            AplicarFiltro(consulta);
        }
    }

    private void AplicarFiltro(string? consulta)
    {
        _visibles.Clear();

        var filtro = (consulta ?? string.Empty).Trim();
        var coincidentes = string.IsNullOrEmpty(filtro)
            ? _todos
            : _todos.Where(r => r.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var registro in coincidentes)
        {
            _visibles.Add(registro);
        }

        Contador.Text = _todos.Count == 1 ? "1 registro" : $"{_todos.Count} registros";
        EstadoVacio.Visibility = _todos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task ActualizarEstadoRespaldoAsync()
    {
        try
        {
            var info = await ServiciosApp.Respaldos.GetStatusAsync(default);
            EstadoRespaldo.Text = info.LastBackupAt is null
                ? $"Respaldos: {info.UniqueBackupCount}"
                : $"Respaldos: {info.UniqueBackupCount} · último {info.LastBackupAt:dd/MM/yyyy HH:mm}";

            if (!info.LastOperationSucceeded)
            {
                EstadoRespaldo.Text += " · ERROR EN EL ÚLTIMO RESPALDO";
            }
        }
        catch (Exception excepcion)
        {
            Registro.Advertencia("BACKUP_STATUS_FAILED");
            Registro.Error("BACKUP_STATUS_DETAIL", excepcion);
            EstadoRespaldo.Text = string.Empty;
        }
    }

    // ═══════════════════════ Registro cargado ═══════════════════════

    public long? IdRegistroCargado => _registroCargadoId;

    public string NombreRegistroCargado => _registroCargadoNombre;

    /// <summary>Nombre del registro activo, o cadena vacía si no hay ninguno.</summary>
    public string NombreRegistroActivo => _registroCargadoNombre;

    /// <summary>Desvincula el registro cargado (al empezar un registro nuevo).</summary>
    public void OlvidarRegistroCargado() => EstablecerRegistroActivo(null, string.Empty);

    /// <summary>
    /// Fija el registro sobre el que trabaja la sesión y lo refleja en la
    /// cabecera de la ventana. Es el único punto que toca este estado, para que
    /// no pueda quedar desincronizado con lo que ve el usuario.
    /// </summary>
    private void EstablecerRegistroActivo(long? id, string nombre)
    {
        _registroCargadoId = id;
        _registroCargadoNombre = id is null ? string.Empty : nombre;
        _ventana?.ActualizarRegistroActivo(_registroCargadoNombre);
    }

    /// <summary>Nombre propuesto al guardar una copia: razón social, o área del TDR.</summary>
    private string NombreSugerido()
    {
        var anexos = _ventana?.PaginaAnexosVista.ExportarEstado();
        var baseNombre = anexos?.NombreProveedor;

        if (string.IsNullOrWhiteSpace(baseNombre))
        {
            baseNombre = _ventana?.PaginaTdrVista.Oficina;
        }

        baseNombre = (baseNombre ?? "Registro").Trim();
        if (baseNombre.Length > 60)
        {
            baseNombre = baseNombre[..60];
        }

        return baseNombre.Length == 0 ? "Registro" : baseNombre;
    }

    // ═══════════════════════ Acciones ═══════════════════════

    /// <summary>
    /// Acción «Guardar» (botón del panel y Ctrl+S): actualiza exclusivamente
    /// el registro activo y nunca solicita un nombre.
    /// </summary>
    /// <remarks>
    /// El nombre se solicita al ejecutar «Nuevo registro». Si no hay un registro
    /// activo, se informa al usuario sin abrir el diálogo de nombre.
    /// </remarks>
    public async Task GuardarActualAsync()
    {
        if (!await SemaforoOperacionesRegistro.WaitAsync(0))
        {
            return;
        }

        try
        {
            await GuardarActualCoreAsync();
        }
        finally
        {
            SemaforoOperacionesRegistro.Release();
        }
    }

    private async Task GuardarActualCoreAsync()
    {
        if (_ventana is null)
        {
            return;
        }

        if (_registroCargadoId is not { } id)
        {
            await ServicioDialogos.MostrarAdvertenciaAsync(
                "Guardar registro",
                "No hay un registro activo. Use «Nuevo registro» para crear uno y asignarle un nombre.");
            return;
        }

        if (!await ExisteRegistroAsync(id))
        {
            EstablecerRegistroActivo(null, string.Empty);
            Registro.Advertencia("RECORD_ACTIVE_MISSING");
            await ServicioDialogos.MostrarAdvertenciaAsync(
                "Guardar registro",
                "El registro activo ya no existe. Use «Nuevo registro» para crear uno nuevo.");
            return;
        }

        await ActualizarRegistroAsync(id, _registroCargadoNombre);
    }

    /// <summary>
    /// Solicita el nombre y crea el registro que iniciará «Nuevo registro».
    /// El formulario solo se limpia después de que el usuario confirma un
    /// nombre disponible.
    /// </summary>
    /// <param name="prepararFormulario">
    /// Acción que deja ambos formularios en blanco antes de capturar el
    /// contenido inicial del nuevo registro.
    /// </param>
    /// <returns><c>true</c> si el registro fue creado.</returns>
    public async Task<bool> CrearRegistroNuevoAsync(Action prepararFormulario)
    {
        if (!await SemaforoOperacionesRegistro.WaitAsync(0))
        {
            return false;
        }

        try
        {
            return await CrearRegistroNuevoCoreAsync(prepararFormulario);
        }
        finally
        {
            SemaforoOperacionesRegistro.Release();
        }
    }

    private async Task<bool> CrearRegistroNuevoCoreAsync(Action prepararFormulario)
    {
        if (_ventana is null)
        {
            return false;
        }

        var sugerido = "Registro";

        while (true)
        {
            var nombre = await ServicioDialogos.PedirTextoAsync(
                "Guardar registro", "Nombre del registro:", sugerido);

            if (nombre is null)
            {
                return false;
            }

            nombre = nombre.Trim();
            if (nombre.Length == 0)
            {
                await ServicioDialogos.MostrarAdvertenciaAsync(
                    "Nombre requerido", "Escriba un nombre para crear el registro.");
                continue;
            }

            try
            {
                if (await ServiciosApp.Registros.FindIdByNameAsync(nombre, null, default) is not null)
                {
                    await ServicioDialogos.MostrarAdvertenciaAsync(
                        "Nombre en uso",
                        $"Ya existe un registro llamado «{nombre}». Elija otro nombre.");
                    sugerido = nombre;
                    continue;
                }

                var estadoAnterior = _ventana.RecolectarBorrador();
                var idAnterior = _registroCargadoId;
                var nombreAnterior = _registroCargadoNombre;
                var habiaCambios = _ventana.HayCambiosSinGuardar();

                long id;
                try
                {
                    prepararFormulario();
                    id = await ServiciosApp.Registros.CreateAsync(
                        nombre, _ventana.RecolectarBorrador(), default);
                }
                catch
                {
                    // El formulario ya fue limpiado para formar el registro
                    // nuevo. Si falla el guardado, se restaura exactamente el
                    // trabajo anterior y su estado pendiente.
                    _ventana.AplicarBorrador(
                        estadoAnterior,
                        marcarComoGuardado: !habiaCambios);
                    EstablecerRegistroActivo(idAnterior, nombreAnterior);
                    throw;
                }

                EstablecerRegistroActivo(id, nombre);
                await EliminarAutoguardadoConfirmadoAsync();
                _ventana.NotificarGuardado(nombre);
                Registro.Info("RECORD_CREATE_OK");
                await RefrescarAsync();
                return true;
            }
            catch (Exception excepcion)
            {
                await AvisarFalloGuardadoAsync(excepcion);
                return false;
            }
        }
    }

    /// <summary>
    /// Acción «Guardar como…» (Ctrl+Mayús+S).
    /// </summary>
    /// <remarks>
    /// Pide siempre un nombre y crea una copia. El registro original queda
    /// intacto y la copia pasa a ser el registro activo.
    /// </remarks>
    public async Task GuardarComoAsync()
    {
        if (!await SemaforoOperacionesRegistro.WaitAsync(0))
        {
            return;
        }

        try
        {
            await GuardarComoCoreAsync();
        }
        finally
        {
            SemaforoOperacionesRegistro.Release();
        }
    }

    private async Task GuardarComoCoreAsync()
    {
        if (_ventana is null)
        {
            return;
        }

        var esCopia = _registroCargadoId is not null;

        var nombre = await ServicioDialogos.PedirTextoAsync(
            esCopia ? "Guardar como" : "Guardar registro",
            "Nombre del registro:",
            esCopia ? $"{_registroCargadoNombre} (copia)" : NombreSugerido());

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return;
        }

        nombre = nombre.Trim();

        try
        {
            var existente = await ServiciosApp.Registros.FindIdByNameAsync(nombre, null, default);

            if (existente is not null)
            {
                if (!await ServicioDialogos.PreguntarSiNoAsync(
                        "Registro existente",
                        $"Ya existe un registro llamado «{nombre}».{Environment.NewLine}" +
                        "¿Desea reemplazarlo con los datos actuales?"))
                {
                    return;
                }

                await ActualizarRegistroAsync(existente.Value, nombre);
                return;
            }

            var id = await ServiciosApp.Registros.CreateAsync(
                nombre, _ventana.RecolectarBorrador(), default);

            EstablecerRegistroActivo(id, nombre);
            await EliminarAutoguardadoConfirmadoAsync();
            _ventana.NotificarGuardado(nombre);
            Registro.Info("RECORD_CREATE_OK");
            await RefrescarAsync();
        }
        catch (Exception excepcion)
        {
            await AvisarFalloGuardadoAsync(excepcion);
        }
    }

    /// <summary>Escribe el formulario actual sobre un registro ya existente.</summary>
    private async Task ActualizarRegistroAsync(long id, string nombre)
    {
        if (_ventana is null)
        {
            return;
        }

        try
        {
            await ServiciosApp.Registros.UpdateAsync(
                id, nombre, _ventana.RecolectarBorrador(), default);

            EstablecerRegistroActivo(id, nombre);
            await EliminarAutoguardadoConfirmadoAsync();
            _ventana.NotificarGuardado(nombre);
            Registro.Info("RECORD_UPDATE_OK");
            await RefrescarAsync();
        }
        catch (Exception excepcion)
        {
            await AvisarFalloGuardadoAsync(excepcion);
        }
    }

    /// <summary>Comprueba que el registro activo siga existiendo en la base.</summary>
    private static async Task<bool> ExisteRegistroAsync(long id)
    {
        try
        {
            return await ServiciosApp.Registros.GetAsync(id, default) is not null;
        }
        catch (Exception excepcion)
        {
            Registro.Error("RECORD_ACTIVE_CHECK_FAILED", excepcion);
            return false;
        }
    }

    /// <summary>
    /// Informa de un fallo al guardar sin exponer el detalle técnico.
    /// </summary>
    /// <remarks>
    /// El mensaje de la excepción puede traer rutas internas o texto de SQLite
    /// que no significa nada para quien está usando el programa, y que además
    /// revela cómo está montado por dentro. El detalle queda en el registro de
    /// diagnóstico, que es donde sirve.
    /// </remarks>
    private static async Task AvisarFalloGuardadoAsync(Exception excepcion)
    {
        Registro.Error("RECORD_SAVE_FAILED", excepcion);
        await ServicioDialogos.MostrarErrorAsync(
            "Guardar registro",
            "No se pudo guardar el registro." + Environment.NewLine + Environment.NewLine
            + "Compruebe que el programa no esté abierto en otra ventana e inténtelo "
            + "de nuevo. Si el problema continúa, revise el registro de errores desde "
            + "Configuración › Datos y diagnóstico.");
    }

    private async void AlCargar(object sender, RoutedEventArgs e)
    {
        if (RegistroDe(sender) is { } vista)
        {
            if (await CargarRegistroAsync(vista))
            {
                _ventana?.CambiarPestana(0);
            }
        }
    }

    /// <summary>
    /// Abre el registro en la sección de TDR, sin generar nada.
    /// </summary>
    /// <remarks>
    /// Antes estos botones cargaban el registro <i>y</i> generaban el documento
    /// de inmediato, así que al pulsarlos aparecía sin aviso el cuadro de
    /// Windows «Guardar como». El usuario pedía abrir un registro y se
    /// encontraba guardando un archivo.
    ///
    /// Ahora solo cargan y llevan a la sección correspondiente. Generar sigue
    /// siendo una decisión explícita, con su botón, después de revisar los
    /// datos.
    /// </remarks>
    private async void AlAbrirEnTdr(object sender, RoutedEventArgs e)
    {
        if (RegistroDe(sender) is { } vista && _ventana is not null
            && await CargarRegistroAsync(vista))
        {
            _ventana.CambiarPestana(0);
        }
    }

    /// <summary>Abre el registro en la sección de Anexos, sin generar nada.</summary>
    private async void AlAbrirEnAnexos(object sender, RoutedEventArgs e)
    {
        if (RegistroDe(sender) is { } vista && _ventana is not null
            && await CargarRegistroAsync(vista))
        {
            _ventana.CambiarPestana(1);
        }
    }

    /// <summary>
    /// Crea una copia de un registro guardado, con otro nombre.
    /// </summary>
    /// <remarks>
    /// Sustituye al antiguo «Guardar como…» del panel lateral, que se confundía
    /// con «Guardar». Aquí actúa sobre el registro de la fila, no sobre el
    /// formulario abierto: el original queda intacto y el registro activo no
    /// cambia. Para trabajar sobre la copia basta con cargarla.
    /// </remarks>
    private async void AlDuplicar(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not RegistroVista vista)
        {
            return;
        }

        var nombre = await ServicioDialogos.PedirTextoAsync(
            "Duplicar registro", "Nombre de la copia:", $"{vista.Nombre} (copia)");

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return;
        }

        nombre = nombre.Trim();

        try
        {
            if (await ServiciosApp.Registros.FindIdByNameAsync(nombre, null, default) is not null)
            {
                await ServicioDialogos.MostrarAdvertenciaAsync(
                    "Nombre en uso",
                    $"Ya existe un registro llamado «{nombre}». Elija otro nombre.");
                return;
            }

            var datos = await ServiciosApp.Registros.GetAsync(vista.Id, default);
            if (datos is null)
            {
                await ServicioDialogos.MostrarAdvertenciaAsync(
                    "Duplicar registro",
                    "No se pudo leer el registro original. Actualice la lista e inténtelo de nuevo.");
                return;
            }

            await ServiciosApp.Registros.CreateAsync(nombre, datos, default);
            Registro.Info("RECORD_DUPLICATED");
            await RefrescarAsync();
        }
        catch (Exception excepcion)
        {
            Registro.Error("RECORD_DUPLICATE_FAILED", excepcion);
            await ServicioDialogos.MostrarErrorAsync(
                "Duplicar registro",
                "No se pudo crear la copia. Inténtelo de nuevo.");
        }
    }

    /// <summary>Guarda el formulario abierto como un registro nuevo.</summary>
    private async void AlGuardarComoNuevo(object sender, RoutedEventArgs e)
        => await GuardarComoAsync();

    private async void AlRenombrar(object sender, RoutedEventArgs e)
    {
        if (RegistroDe(sender) is not { } vista)
        {
            return;
        }

        var nombre = await ServicioDialogos.PedirTextoAsync(
            "Renombrar registro", "Nuevo nombre:", vista.Nombre);

        if (string.IsNullOrWhiteSpace(nombre) || nombre == vista.Nombre)
        {
            return;
        }

        try
        {
            if (await ServiciosApp.Registros.FindIdByNameAsync(nombre, vista.Id, default) is not null)
            {
                await ServicioDialogos.MostrarAdvertenciaAsync(
                    "Nombre en uso",
                    $"Ya existe un registro llamado «{nombre}». Elija otro nombre.");
                return;
            }

            await ServiciosApp.Registros.RenameAsync(vista.Id, nombre, default);
            if (_registroCargadoId == vista.Id)
            {
                EstablecerRegistroActivo(vista.Id, nombre);
            }

            await RefrescarAsync();
        }
        catch (Exception excepcion)
        {
            Registro.Error("RECORD_RENAME_FAILED", excepcion);
            await ServicioDialogos.MostrarErrorAsync(
                "Renombrar registro",
                "No se pudo cambiar el nombre del registro. Inténtelo de nuevo.");
        }
    }

    private async void AlEliminar(object sender, RoutedEventArgs e)
    {
        if (RegistroDe(sender) is not { } vista)
        {
            return;
        }

        if (!await ServicioDialogos.PreguntarSiNoAsync(
                "Eliminar registro",
                $"¿Desea eliminar «{vista.Nombre}»?{Environment.NewLine}Esta acción no se puede deshacer."))
        {
            return;
        }

        try
        {
            await ServiciosApp.Registros.DeleteAsync(vista.Id, default);
            if (_registroCargadoId == vista.Id)
            {
                OlvidarRegistroCargado();
            }

            await RefrescarAsync();
        }
        catch (Exception excepcion)
        {
            Registro.Error("RECORD_DELETE_FAILED", excepcion);
            await ServicioDialogos.MostrarErrorAsync(
                "Eliminar registro",
                "No se pudo eliminar el registro. Inténtelo de nuevo.");
        }
    }

    private async Task<bool> CargarRegistroAsync(RegistroVista vista)
    {
        if (_ventana is null)
        {
            return false;
        }

        if (_registroCargadoId != vista.Id && _ventana.HayCambiosSinGuardar() &&
            !await _ventana.ConfirmarCambiosAntesDeAsync("abrir otro registro"))
        {
            return false;
        }

        try
        {
            var datos = await ServiciosApp.Registros.GetAsync(vista.Id, default);
            if (datos is null)
            {
                await ServicioDialogos.MostrarAdvertenciaAsync(
                    "Cargar registro", "El registro ya no existe.");
                await RefrescarAsync();
                return false;
            }

            // conservarFecha: false → el documento nuevo lleva la fecha de hoy.
            _ventana.AplicarBorrador(datos, marcarComoGuardado: true, conservarFecha: false);
            EstablecerRegistroActivo(vista.Id, vista.Nombre);
            await RefrescarAsync();
            return true;
        }
        catch (Exception excepcion)
        {
            Registro.Error("RECORD_LOAD_FAILED", excepcion);
            await ServicioDialogos.MostrarErrorAsync(
                "Cargar registro",
                "No se pudo abrir el registro guardado." + Environment.NewLine + Environment.NewLine
                + "Puede que se haya dañado o que se guardara con otra cuenta de "
                + "Windows. Existe una copia de seguridad en la carpeta de datos.");
            return false;
        }
    }

    private static RegistroVista? RegistroDe(object sender)
        => (sender as FrameworkElement)?.DataContext as RegistroVista;

    // ═══════════════════════ Autoguardado ═══════════════════════

    /// <summary>Guarda el borrador cifrado de la sesión (cada 45 s).</summary>
    public static async Task AutoguardarAsync(BorradorPayloadV1 borrador)
    {
        if (!await SemaforoAutoguardado.WaitAsync(0))
        {
            return;
        }

        try
        {
            await ServiciosApp.Borradores.SaveAutosaveAsync(borrador, default);
        }
        finally
        {
            SemaforoAutoguardado.Release();
        }
    }

    /// <summary>Ofrece recuperar el trabajo si quedó un borrador de la última sesión.</summary>
    private async Task OfrecerRecuperarAsync()
    {
        try
        {
            if (!await ServiciosApp.Borradores.AutosaveExistsAsync(default))
            {
                return;
            }

            var recuperar = await ServicioDialogos.PreguntarSiNoAsync(
                "Recuperar trabajo",
                "Se encontró el trabajo de la última sesión (es posible que el programa " +
                $"se haya cerrado de forma inesperada).{Environment.NewLine}{Environment.NewLine}" +
                "¿Desea recuperarlo?",
                defectoSi: true);

            if (recuperar)
            {
                var resultado = await ServiciosApp.Borradores.ReadAutosaveAsync(default);
                if (resultado?.Payload is null)
                {
                    await ServicioDialogos.MostrarAdvertenciaAsync(
                        "Recuperar trabajo", "El borrador está vacío o no tiene un formato válido.");
                    return;
                }

                OlvidarRegistroCargado();
                _ventana?.AplicarBorrador(resultado.Payload, marcarComoGuardado: false);
            }
            else
            {
                await ServiciosApp.Borradores.DeleteAutosaveAsync(default);
            }
        }
        catch (Exception excepcion)
        {
            Registro.Error("AUTOSAVE_RECOVER_FAILED", excepcion);
            await ServicioDialogos.MostrarAdvertenciaAsync(
                "Recuperar trabajo", "No se pudo recuperar el borrador anterior.");
        }
    }

    private static async Task EliminarAutoguardadoConfirmadoAsync()
    {
        try
        {
            await ServiciosApp.Borradores.DeleteAutosaveAsync(default);
        }
        catch (Exception excepcion)
        {
            Registro.Error("AUTOSAVE_DELETE_AFTER_SAVE_FAILED", excepcion);
        }
    }
}
