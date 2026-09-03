using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
        }
    }

    // ═══════════════════════ Listado ═══════════════════════

    /// <summary>Equivalente de <c>refrescar</c>.</summary>
    public async Task RefrescarAsync()
    {
        try
        {
            var registros = await ServiciosApp.Registros.ListAsync(default);
            var vistas = new List<RegistroVista>(registros.Count);

            foreach (var resumen in registros)
            {
                // El original inspecciona el contenido de cada registro para
                // saber qué documentos puede generar; sin esto los botones
                // «TDR» y «Anexo» quedarían habilitados por igual.
                BorradorPayloadV1? contenido = null;
                try
                {
                    contenido = await ServiciosApp.Registros.GetAsync(resumen.Id, default);
                }
                catch (Exception excepcion)
                {
                    // Un registro cuyo contenido no pueda abrirse debe seguir
                    // visible para que el usuario pueda reemplazarlo o borrarlo.
                    // Antes, una sola fila dañada vaciaba toda la sección.
                    Registro.Error($"RECORD_CONTENT_READ_FAILED_{resumen.Id}", excepcion);
                }

                vistas.Add(new RegistroVista
                {
                    Id = resumen.Id,
                    Nombre = resumen.Name,
                    Actualizado = resumen.UpdatedAt,
                    TieneTdr = ContenidoRegistro.TieneTdr(contenido),
                    TieneAnexo = ContenidoRegistro.TieneAnexo(contenido),
                });
            }

            _todos = vistas;
        }
        catch (Exception excepcion)
        {
            Registro.Error("DB_LIST_FAILED", excepcion);
            _todos = new List<RegistroVista>();
        }

        AplicarFiltro(Buscador.Text);
        await ActualizarEstadoRespaldoAsync();
    }

    private void AlBuscar(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            AplicarFiltro(sender.Text);
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

    /// <summary>Nombre propuesto al guardar: razón social, o área del TDR.</summary>
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
    /// Acción «Guardar» (botón del panel y Ctrl+S).
    /// </summary>
    /// <remarks>
    /// Si ya hay un registro activo, los cambios se escriben directamente sobre
    /// él sin preguntar nada. Solo se pide un nombre la primera vez, o si el
    /// registro activo ya no existe porque se eliminó durante la sesión.
    /// </remarks>
    public async Task GuardarActualAsync()
    {
        if (_ventana is null)
        {
            return;
        }

        if (_registroCargadoId is { } id)
        {
            if (await ExisteRegistroAsync(id))
            {
                await ActualizarRegistroAsync(id, _registroCargadoNombre);
                return;
            }

            // El registro activo desapareció: se vuelve a tratar como nuevo.
            EstablecerRegistroActivo(null, string.Empty);
            Registro.Advertencia("RECORD_ACTIVE_MISSING");
        }

        await GuardarComoAsync();
    }

    /// <summary>
    /// Acción «Guardar como…» (Ctrl+Mayús+S) y también primer guardado.
    /// </summary>
    /// <remarks>
    /// Pide siempre un nombre. Si todavía no hay registro activo, es el primer
    /// guardado. Si ya lo hay, crea una copia con otro nombre: el registro
    /// original queda intacto y la copia pasa a ser el registro activo, que es
    /// como se comporta cualquier aplicación de escritorio.
    /// </remarks>
    public async Task GuardarComoAsync()
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
            await CargarRegistroAsync(vista);
            _ventana?.CambiarPestana(0);
        }
    }

    private async void AlGenerarTdr(object sender, RoutedEventArgs e)
    {
        if (RegistroDe(sender) is { } vista && _ventana is not null)
        {
            await CargarRegistroAsync(vista);
            await _ventana.PaginaTdrVista.GenerarAsync();
        }
    }

    private async void AlGenerarAnexo(object sender, RoutedEventArgs e)
    {
        if (RegistroDe(sender) is { } vista && _ventana is not null)
        {
            await CargarRegistroAsync(vista);
            _ventana.CambiarPestana(1);
            await _ventana.PaginaAnexosVista.GenerarAsync();
        }
    }

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

    private async Task CargarRegistroAsync(RegistroVista vista)
    {
        try
        {
            var datos = await ServiciosApp.Registros.GetAsync(vista.Id, default);
            if (datos is null)
            {
                await ServicioDialogos.MostrarAdvertenciaAsync(
                    "Cargar registro", "El registro ya no existe.");
                await RefrescarAsync();
                return;
            }

            _ventana?.AplicarBorrador(datos);
            EstablecerRegistroActivo(vista.Id, vista.Nombre);
            await RefrescarAsync();
        }
        catch (Exception excepcion)
        {
            Registro.Error("RECORD_LOAD_FAILED", excepcion);
            await ServicioDialogos.MostrarErrorAsync(
                "Cargar registro",
                "No se pudo abrir el registro guardado." + Environment.NewLine + Environment.NewLine
                + "Puede que se haya dañado o que se guardara con otra cuenta de "
                + "Windows. Existe una copia de seguridad en la carpeta de datos.");
        }
    }

    private static RegistroVista? RegistroDe(object sender)
        => (sender as FrameworkElement)?.DataContext as RegistroVista;

    // ═══════════════════════ Autoguardado ═══════════════════════

    /// <summary>Guarda el borrador cifrado de la sesión (cada 45 s).</summary>
    public static void Autoguardar(BorradorPayloadV1 borrador)
        => _ = ServiciosApp.Borradores.SaveAutosaveAsync(borrador, default);

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
                _ventana?.AplicarBorrador(resultado?.Payload);
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
}
