using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneradorAnexos.Domain.Documents;
using GeneradorAnexos.Domain.Formatting;
using GeneradorAnexos.Domain.Models;
using GeneradorAnexos.Domain.Payments;
using GeneradorAnexos.Domain.Validation;
using GeneradorAnexos.Infrastructure.Windows.Documents;
using GeneradorAnexos.WinUI.Controls;
using GeneradorAnexos.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeneradorAnexos.WinUI.Views;

/// <summary>
/// Equivalente de <c>ui/tab_tdr.py: PestanaTDR</c>.
/// </summary>
public sealed partial class PaginaTdr : UserControl
{
    private readonly PreferenciasUi _preferencias = new();
    private readonly GestorVistasPrevias _vistasPrevias = new("tdr");

    private readonly TablaObjeto _tablaObjeto = new();
    private readonly TablaEntregables _tablaUnico = new() { Unico = true };
    private readonly TablaEntregables _tablaEntregables = new();
    private readonly TablaPagos _tablaPagos = new();

    private VentanaPrincipal? _ventana;
    private EstadoCompartido? _estado;
    private SincronizadorUnidireccional? _sincronizador;
    private string _rutaPedido = string.Empty;

    public PaginaTdr()
    {
        InitializeComponent();
        EtiquetaVersion.Text = $"v{Constantes.AppVersion}";

        ContenedorTablaObjeto.Content = _tablaObjeto;
        ContenedorUnico.Content = _tablaUnico;
        ContenedorMultiple.Content = _tablaEntregables;
        ContenedorPagoMultiple.Content = _tablaPagos;

        ConfigurarCampos();
        ConstruirRequisitosBase();

        _tablaUnico.Inicializar();
        _tablaEntregables.Cambio += (_, _) => SincronizarPagos();
        _tablaEntregables.FilaEliminada += (_, indice) => _tablaPagos.Eliminar(indice);
        _tablaPagos.TotalCambiado += (_, _) => _ventana?.PaginaAnexosVista.ActualizarResumenFormaPago();

        Selector.Cambiado += (_, modo) => AplicarModo(modo);
        AplicarModo(SelectorModo.ModoUnico);
    }

    public void Inicializar(VentanaPrincipal ventana, EstadoCompartido estado)
    {
        _ventana = ventana;
        _estado = estado;
    }

    /// <summary>Área usuaria; los Anexos la usan como campo OFICINA.</summary>
    public string Oficina => CampoOficina.Valor;

    // ═══════════════════════ Configuración ═══════════════════════

    private void ConfigurarCampos()
    {
        CampoOficina.EstablecerOpciones(ServicioCatalogos.AreasUsuarias);
        CampoOficina.Validacion = FieldValidators.IsNonEmptyText;

        CampoPedido.Validacion = FieldValidators.IsNonEmptyText;
        CampoActividad.Validacion = FieldValidators.IsNonEmptyText;
        CampoFuente.Validacion = FieldValidators.IsNonEmptyText;
        CampoMeta.Validacion = FieldValidators.IsNonEmptyText;

        // validador_clasificador: dígitos y puntos, hasta 20 caracteres.
        CampoClasificador.FiltroTeclado =
            t => t.Length <= 20 && t.All(c => char.IsDigit(c) || c == '.');
        CampoClasificador.Validacion = FieldValidators.IsValidClassifier;

        CampoDias.FiltroTeclado = t => t.Length <= 3 && t.All(char.IsDigit);
        CampoDias.Validacion = FieldValidators.IsPositiveInteger;
        CampoDias.UsarTecladoNumerico();
        CampoDias.Caja.TextAlignment = TextAlignment.Center;

        EditorFormacion.Obligatorio = true;
        EditorExperiencia.Obligatorio = true;
        EditorCapacitaciones.Obligatorio = true;
    }

    /// <summary>Filas de solo lectura de los requisitos incluidos en la plantilla.</summary>
    /// <summary>
    /// Estilo de los recursos de la aplicación. El código aplica estilos y no
    /// pinceles: un pincel es una foto del tema actual y no cambia al alternar
    /// claro/oscuro; un estilo con ThemeResource sí.
    /// </summary>
    private static Style Estilo(string clave)
        => (Style)Microsoft.UI.Xaml.Application.Current.Resources[clave];

    private void ConstruirRequisitosBase()
    {
        foreach (var requisito in Constantes.RequisitosBase)
        {
            var fila = new Grid { ColumnSpacing = 10 };
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var marca = new Icono
            {
                Nombre = "check",
                Tamano = 15,
                Style = Estilo("Ga.IconoOk"),
                VerticalAlignment = VerticalAlignment.Top,
            };
            Grid.SetColumn(marca, 0);
            fila.Children.Add(marca);

            var texto = new TextBlock
            {
                Text = requisito,
                Style = Estilo("Ga.TextoRequisito"),
            };
            Grid.SetColumn(texto, 1);
            fila.Children.Add(texto);

            RequisitosBase.Children.Add(fila);
        }
    }

    // ═══════════════════════ Sincronización ═══════════════════════

    /// <summary>
    /// Configura la sincronización unidireccional: la denominación alimenta al
    /// objeto, al cuadro y a la descripción de Anexos, respetando ediciones
    /// manuales; el plazo y el número de pedido van por el estado compartido.
    /// </summary>
    public void ConectarEstado()
    {
        if (_estado is null || _ventana is null)
        {
            return;
        }

        _sincronizador = new SincronizadorUnidireccional(() => CampoDenominacion.Valor);

        _sincronizador.Agregar("objeto",
            () => CampoObjetivo.Valor,
            texto =>
            {
                CampoObjetivo.EstablecerValorSilencioso(texto);
                CampoObjetivo.DestellarSincronizacion();
            });

        _sincronizador.Agregar("cuadro",
            () => _tablaObjeto.Descripcion,
            texto =>
            {
                _tablaObjeto.Descripcion = texto;
                _tablaObjeto.DestellarSincronizacion();
            });

        var descripcionAnexos = _ventana.PaginaAnexosVista.CampoDescripcionServicio;
        _sincronizador.Agregar("anexos_desc",
            () => descripcionAnexos.Valor,
            texto =>
            {
                descripcionAnexos.EstablecerValorSilencioso(texto);
                descripcionAnexos.DestellarSincronizacion();
            });

        CampoDenominacion.TextoCambiado += (_, _) => _sincronizador.Propagar();
        CampoObjetivo.Cambiado += (_, _) => _sincronizador.NotificarEdicion("objeto");
        _tablaObjeto.Cambiado += (_, _) => _sincronizador.NotificarEdicion("cuadro");
        descripcionAnexos.Cambiado += (_, _) => _sincronizador.NotificarEdicion("anexos_desc");

        // Plazo y número de pedido: bidireccionales por el estado compartido.
        CampoDias.TextoCambiado += (_, _) => _estado.EstablecerPlazo(CampoDias.Valor, CampoDias);
        _estado.PlazoCambiado += (_, cambio) =>
        {
            if (!ReferenceEquals(cambio.Origen, CampoDias))
            {
                CampoDias.EstablecerValorSilencioso(cambio.Texto);
                CampoDias.DestellarSincronizacion();
            }
        };

        CampoPedido.TextoCambiado += (_, _) => _estado.EstablecerNumeroPedido(CampoPedido.Valor, CampoPedido);
        _estado.NumeroPedidoCambiado += (_, cambio) =>
        {
            if (!ReferenceEquals(cambio.Origen, CampoPedido))
            {
                CampoPedido.EstablecerValorSilencioso(cambio.Texto);
                CampoPedido.DestellarSincronizacion();
            }
        };
    }

    public void SilenciarSincronizacion(bool valor) => _sincronizador?.Silenciar(valor);

    public Dictionary<string, bool> EstadoSincronizacion()
        => _sincronizador?.EstadoPersonalizado() ?? new Dictionary<string, bool>();

    public void AplicarPersonalizado(IReadOnlyDictionary<string, bool>? datos)
        => _sincronizador?.AplicarPersonalizado(datos);

    // ═══════════════════════ Modos de entregables ═══════════════════════

    /// <summary>Equivalente de <c>_aplicar_modo</c>.</summary>
    private void AplicarModo(string modo)
    {
        var multiple = modo == SelectorModo.ModoMultiple;

        ContenedorUnico.Visibility = multiple ? Visibility.Collapsed : Visibility.Visible;
        ContenedorMultiple.Visibility = multiple ? Visibility.Visible : Visibility.Collapsed;
        PanelPagoUnico.Visibility = multiple ? Visibility.Collapsed : Visibility.Visible;
        ContenedorPagoMultiple.Visibility = multiple ? Visibility.Visible : Visibility.Collapsed;

        if (multiple && _tablaEntregables.Cantidad == 0)
        {
            _tablaEntregables.Agregar();
            _tablaEntregables.Agregar();
        }

        _ventana?.PaginaAnexosVista.ActualizarResumenFormaPago();
    }

    private void SincronizarPagos()
    {
        _tablaPagos.EstablecerCantidad(_tablaEntregables.Cantidad);
        _ventana?.PaginaAnexosVista.ActualizarResumenFormaPago();
    }

    // ═══════════════════════ Importar Pedido SIGA ═══════════════════════

    private async void AlCargarPedido(object sender, RoutedEventArgs e)
    {
        if (_ventana is null)
        {
            return;
        }

        var ruta = await SelectorArchivos.AbrirAsync(_ventana, ".pdf");
        if (string.IsNullOrEmpty(ruta))
        {
            return;
        }

        _rutaPedido = ruta;
        _preferencias.UltimaCarpetaPedido = Path.GetDirectoryName(ruta) ?? string.Empty;

        ArchivoPedido.Text = Path.GetFileName(ruta);
        ArchivoPedido.Style = Estilo("Ga.TextoArchivo");
        BotonProcesar.IsEnabled = true;
    }

    private async void AlProcesarPedido(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_rutaPedido))
        {
            return;
        }

        BotonProcesar.IsEnabled = false;
        try
        {
            var datos = await ServiciosApp.LectorPedido.ReadFirstPageAsync(_rutaPedido, default);
            var resumen = AplicarDatosPedido(datos);
            await ServicioDialogos.MostrarInformacionAsync("Pedido procesado", resumen);
        }
        catch (Exception excepcion)
        {
            Registro.Error("ORDER_PDF_READ_FAILED", excepcion);
            await ServicioDialogos.MostrarAdvertenciaAsync(
                "Pedido de Servicio",
                excepcion is DocumentoException
                    ? excepcion.Message
                    : "No se pudo leer el archivo." + Environment.NewLine + Environment.NewLine
                      + "Compruebe que sea el PDF original del Pedido de Servicio del "
                      + "SIGA y que no esté dañado ni protegido con contraseña.");
        }
        finally
        {
            BotonProcesar.IsEnabled = true;
        }
    }

    /// <summary>Vuelca en el formulario los campos extraídos del PDF del SIGA.</summary>
    private string AplicarDatosPedido(GeneradorAnexos.Application.Abstractions.Integrations.OrderData datos)
    {
        RellenarCombo(CampoOficina, datos.RequestingOffice);
        RellenarTexto(CampoPedido, datos.Number);
        RellenarTexto(CampoMeta, datos.Meta);
        RellenarTexto(CampoClasificador, datos.Classifier);

        // Igual que Python: el motivo tiene prioridad; la descripción del
        // primer ítem se usa solamente si no se pudo identificar el motivo.
        var denominacion = string.IsNullOrWhiteSpace(datos.Reason) ? datos.Description : datos.Reason;
        if (!string.IsNullOrWhiteSpace(denominacion))
        {
            CampoDenominacion.EstablecerValorSilencioso(denominacion);
            CampoDenominacion.DestellarSincronizacion();
            _sincronizador?.Propagar();
        }

        _tablaObjeto.EstablecerUnidad(datos.Unit);

        var montoAplicado = false;
        if (!string.IsNullOrWhiteSpace(datos.Amount))
        {
            montoAplicado = _ventana?.PaginaAnexosVista.EstablecerMonto(datos.Amount) == true;
        }

        var campos = new (string Nombre, bool Aplicado)[]
        {
            ("N° de pedido", !string.IsNullOrWhiteSpace(datos.Number)),
            ("Dirección solicitante", !string.IsNullOrWhiteSpace(datos.RequestingOffice)),
            ("Denominación del servicio", !string.IsNullOrWhiteSpace(denominacion)),
            ("Meta", !string.IsNullOrWhiteSpace(datos.Meta)),
            ("Clasificador", !string.IsNullOrWhiteSpace(datos.Classifier)),
            ("Unidad de medida", !string.IsNullOrWhiteSpace(datos.Unit)),
            ("Monto de Anexos", montoAplicado),
        };
        var aplicados = campos.Where(c => c.Aplicado).Select(c => c.Nombre).ToList();
        var faltantes = campos.Where(c => !c.Aplicado).Select(c => c.Nombre).ToList();
        var resumen = aplicados.Count > 0
            ? "Campos completados: " + string.Join(", ", aplicados) + "."
            : "No se pudo completar ningún campo.";
        if (faltantes.Count > 0)
        {
            resumen += "\n\nNo se pudieron obtener o aplicar: " + string.Join(", ", faltantes)
                + ". Se conservaron los valores existentes; revíselos manualmente.";
        }
        return resumen;

        static void RellenarTexto(CampoTexto campo, string? valor)
        {
            if (!string.IsNullOrWhiteSpace(valor))
            {
                campo.EstablecerValorSilencioso(valor);
                campo.DestellarSincronizacion();
            }
        }

        static void RellenarCombo(CampoCombo campo, string? valor)
        {
            if (!string.IsNullOrWhiteSpace(valor))
            {
                campo.EstablecerValorSilencioso(valor);
                campo.DestellarSincronizacion();
            }
        }
    }

    // ═══════════════════════ Validación ═══════════════════════

    private ICampo[] CamposGenerales() => new ICampo[]
    {
        CampoOficina, CampoPedido, CampoActividad, CampoFuente, CampoMeta,
        CampoClasificador, CampoDenominacion, CampoObjetivo, CampoFinalidad,
        CampoActividades, CampoDias,
    };

    private bool Validar()
    {
        var campos = CamposGenerales().Select(c => c.ForzarValidacion()).ToList();
        var vinetas = new[] { EditorFormacion, EditorExperiencia, EditorCapacitaciones }
            .Select(e => e.ForzarValidacion())
            .ToList();

        var objeto = _tablaObjeto.Validar();
        var multiple = Selector.Modo == SelectorModo.ModoMultiple;
        var entregables = multiple ? _tablaEntregables.Validar() : _tablaUnico.Validar();
        var pagos = !multiple || (_tablaPagos.Validar() && _tablaPagos.Total == 100);

        return campos.All(r => r) && vinetas.All(r => r) && objeto && entregables && pagos;
    }

    private int ContarFaltantes() => CamposGenerales().Count(c => !c.EsValido);

    // ═══════════════════════ Contexto documental ═══════════════════════

    private Dictionary<string, string> RecolectarContexto()
    {
        var partes = DocumentFormatting.GetDateParts(
            _ventana?.FechaDocumento ?? DateOnly.FromDateTime(DateTime.Now));

        return new Dictionary<string, string>
        {
            ["OFICINA"] = CampoOficina.Valor,
            ["ACTIVIDAD_POI"] = CampoActividad.Valor,
            ["FUENTE_FINANCIAMIENTO"] = CampoFuente.Valor,
            ["META"] = CampoMeta.Valor,
            ["CLASIFICADOR"] = CampoClasificador.Valor,
            ["DENOMINACION_SERVICIO"] = CampoDenominacion.Valor,
            ["OBJETIVO_CONTRATACION"] = CampoObjetivo.Valor,
            ["DESCRIPCION_DE_LA_FINALIDAD_PUBLICA"] = CampoFinalidad.Valor,
            ["ACTIVIDADES_A_DESARROLLAR"] = CampoActividades.Valor,
            ["DIAS_PLAZO"] = string.IsNullOrWhiteSpace(CampoDias.Valor)
                ? string.Empty
                : SpanishNumberConverter.CalendarDaysPhrase(CampoDias.Valor),
            ["CANTIDAD"] = _tablaObjeto.Cantidad,
            ["UNIDAD_MEDIDA"] = _tablaObjeto.Unidad,
            ["DESCRIPCION_SERVICIO"] = _tablaObjeto.Descripcion,
            // Punto 7 de la plantilla: en modo unico el entregable se escribe
            // directamente; en modo multiple lo sustituye la tabla clonada.
            ["DESCRIPCION_PRESENTACION_CARTA"] = DescripcionEntregableUnico(),
            ["PLAZO_SERVICIO"] = PlazoEntregableUnico(),
            [EstadoCompartido.ClaveNumeroPedido] = CampoPedido.Valor,
            ["DIA"] = partes.Dia,
            ["MES"] = partes.Mes,
            ["ANO"] = partes.Anio,
        };
    }

    /// <summary>Descripcion del entregable unico, o vacio en modo multiple.</summary>
    private string DescripcionEntregableUnico()
    {
        if (Selector.Modo == SelectorModo.ModoMultiple)
        {
            return string.Empty;
        }

        return _tablaUnico.Exportar().FirstOrDefault()?.Descripcion ?? string.Empty;
    }

    /// <summary>Plazo del entregable unico, o vacio en modo multiple.</summary>
    private string PlazoEntregableUnico()
    {
        if (Selector.Modo == SelectorModo.ModoMultiple)
        {
            return string.Empty;
        }

        return _tablaUnico.Exportar().FirstOrDefault()?.Plazo ?? string.Empty;
    }

    /// <summary>
    /// Nombre propuesto al guardar el TDR.
    /// </summary>
    /// <remarks>
    /// Incluye el número de pedido y la fecha porque el nombre anterior era
    /// solo el área usuaria: dos TDR de la misma oficina proponían el mismo
    /// archivo y el segundo sobrescribía al primero, o había que renombrarlo a
    /// mano cada vez.
    /// </remarks>
    private string NombreSugerido()
        => NombreDocumento.Componer("TDR", CampoPedido.Valor, CampoOficina.Valor, "Servicio");

    // ═══════════════════════ Acciones ═══════════════════════

    private void AlGenerar(object sender, RoutedEventArgs e) => _ = GenerarAsync();

    private void AlLimpiar(object sender, RoutedEventArgs e) => _ = LimpiarAsync();

    private void AlVistaPrevia(object sender, RoutedEventArgs e) => _ = VistaPreviaAsync();

    public async Task GenerarAsync()
    {
        if (!Validar())
        {
            var n = ContarFaltantes();
            await ServicioDialogos.MostrarAdvertenciaAsync(
                "Datos incompletos",
                n > 0
                    ? $"Faltan {n} campos obligatorios (resaltados en rojo)."
                    : "Revise los campos resaltados en rojo antes de generar el TDR.");
            return;
        }

        PlanPagos plan;
        try
        {
            plan = ConstructorPlanPagos.Construir(ExportarEstado(), null);
        }
        catch (PlanPagosException excepcion)
        {
            await ServicioDialogos.MostrarAdvertenciaAsync(
                "Forma de pago incompleta", excepcion.Message);
            return;
        }

        var ruta = await SelectorArchivos.GuardarComoAsync(
            _ventana!, "Guardar TDR", NombreSugerido(),
            "Documento de Word", ".docx", _preferencias.CarpetaGuardar());

        if (string.IsNullOrEmpty(ruta))
        {
            return;
        }

        try
        {
            await ServiciosApp.Documentos.GenerateTdrAsync(
                RecolectarContexto(), ruta, ExportarEstado(), plan, default);
        }
        catch (Exception excepcion)
        {
            Registro.Error("TDR_GENERATION_FAILED", excepcion);
            await ServicioDialogos.MostrarErrorAsync(
                "Error al generar",
                TextoFallo(excepcion, "generar el documento"));
            return;
        }

        _preferencias.RecordarCarpeta(ruta);
        Registro.Info("TDR_GENERATION_OK");

        switch (await ServicioDialogos.MostrarExitoAsync(ruta))
        {
            case ResultadoExito.Imprimir:
                await AccionDocumento.ImprimirAsync(ruta);
                break;
            case ResultadoExito.AbrirDocumento:
                await AccionDocumento.AbrirAsync(ruta);
                break;
            case ResultadoExito.AbrirCarpeta:
                await AccionDocumento.AbrirAsync(Path.GetDirectoryName(ruta) ?? ruta);
                break;
        }
    }

    public async Task VistaPreviaAsync()
    {
        // La vista previa reproduce el estado parcial tal como está. La
        // exportación final conserva la validación estricta de arriba.
        var estado = ExportarEstado();
        var plan = ConstructorPlanPagos.ConstruirVistaPrevia(estado, null);

        var ruta = _vistasPrevias.CrearRuta();
        try
        {
            await ServiciosApp.Documentos.GenerateTdrAsync(
                RecolectarContexto(), ruta, estado, plan, default);
        }
        catch (Exception excepcion)
        {
            _vistasPrevias.Descartar(ruta);
            Registro.Error("TDR_PREVIEW_FAILED", excepcion);
            await ServicioDialogos.MostrarErrorAsync(
                "Error en la vista previa",
                TextoFallo(excepcion, "generar la vista previa"));
            return;
        }

        await AccionDocumento.AbrirAsync(ruta);
    }

    public async Task LimpiarAsync()
    {
        if (!await ServicioDialogos.PreguntarSiNoAsync(
                "Limpiar formulario", "¿Desea borrar todos los datos del TDR?"))
        {
            return;
        }

        LimpiarSilencioso();
    }

    /// <summary>
    /// Vacía el formulario sin preguntar y sin tocar el registro activo.
    /// La usan «Limpiar» (tras confirmar) y «Nuevo registro».
    /// </summary>
    public void LimpiarSilencioso()
    {
        foreach (var campo in CamposGenerales())
        {
            campo.Limpiar();
        }

        EditorFormacion.Limpiar();
        EditorExperiencia.Limpiar();
        EditorCapacitaciones.Limpiar();
        ListaRequisitos.Limpiar();

        _tablaObjeto.Limpiar();
        _tablaUnico.Limpiar();
        _tablaEntregables.Limpiar();
        _tablaPagos.Limpiar();

        Selector.EstablecerModo(SelectorModo.ModoUnico);
    }

    public void LimpiarVistasPrevias() => _vistasPrevias.LimpiarTodo();

    // ═══════════════════════ Serialización ═══════════════════════

    public TdrPayload ExportarEstado() => new()
    {
        Generales = new CamposGeneralesTdrPayload
        {
            Oficina = CampoOficina.Valor,
            NumeroPedido = CampoPedido.Valor,
            ActividadPoi = CampoActividad.Valor,
            FuenteFinanciamiento = CampoFuente.Valor,
            Meta = CampoMeta.Valor,
            Clasificador = CampoClasificador.Valor,
            DenominacionServicio = CampoDenominacion.Valor,
            ObjetivoContratacion = CampoObjetivo.Valor,
            DescripcionFinalidadPublica = CampoFinalidad.Valor,
            ActividadesDesarrollar = CampoActividades.Valor,
            DiasPlazo = CampoDias.Valor,
        },
        Objeto = _tablaObjeto.Exportar(),
        Modo = Selector.Modo,
        Unico = _tablaUnico.Exportar().FirstOrDefault(),
        Entregables = _tablaEntregables.Exportar(),
        Pagos = _tablaPagos.Exportar(),
        Requisitos = ListaRequisitos.Valores().Cast<string?>().ToList(),
        Formacion = EditorFormacion.Valores().Cast<string?>().ToList(),
        Experiencia = EditorExperiencia.Valores().Cast<string?>().ToList(),
        Capacitaciones = EditorCapacitaciones.Valores().Cast<string?>().ToList(),
    };

    public void ImportarEstado(TdrPayload? datos)
    {
        datos ??= new TdrPayload();
        var generales = datos.Generales ?? new CamposGeneralesTdrPayload();

        CampoOficina.EstablecerValorSilencioso(generales.Oficina);
        CampoPedido.EstablecerValorSilencioso(generales.NumeroPedido);
        CampoActividad.EstablecerValorSilencioso(generales.ActividadPoi);
        CampoFuente.EstablecerValorSilencioso(generales.FuenteFinanciamiento);
        CampoMeta.EstablecerValorSilencioso(generales.Meta);
        CampoClasificador.EstablecerValorSilencioso(generales.Clasificador);
        CampoDenominacion.EstablecerValorSilencioso(generales.DenominacionServicio);
        CampoObjetivo.EstablecerValorSilencioso(generales.ObjetivoContratacion);
        CampoFinalidad.EstablecerValorSilencioso(generales.DescripcionFinalidadPublica);
        CampoActividades.EstablecerValorSilencioso(generales.ActividadesDesarrollar);
        CampoDias.EstablecerValorSilencioso(generales.DiasPlazo);

        _tablaObjeto.Importar(datos.Objeto);

        if (datos.Unico is not null)
        {
            _tablaUnico.Importar(new[] { datos.Unico });
        }

        _tablaEntregables.Importar(datos.Entregables);
        if (datos.Pagos is { Count: > 0 })
        {
            // Se respetan porcentajes guardados si corresponden a todos los
            // entregables y ya suman 100 %. Los registros antiguos o
            // incompletos se reparan con la distribución automática.
            _tablaPagos.Importar(datos.Pagos);
            _tablaPagos.EstablecerCantidad(_tablaEntregables.Cantidad);
            if (_tablaPagos.Total != 100)
            {
                _tablaPagos.Distribuir();
            }
        }
        else
        {
            _tablaPagos.Limpiar();
            _tablaPagos.EstablecerCantidad(_tablaEntregables.Cantidad);
        }

        ListaRequisitos.Cargar(datos.Requisitos?.Where(v => v is not null).Select(v => v!));
        EditorFormacion.Cargar(datos.Formacion?.Where(v => v is not null).Select(v => v!));
        EditorExperiencia.Cargar(datos.Experiencia?.Where(v => v is not null).Select(v => v!));
        EditorCapacitaciones.Cargar(datos.Capacitaciones?.Where(v => v is not null).Select(v => v!));

        Selector.EstablecerModo(datos.Modo);
    }

    /// <summary>
    /// Texto que se muestra cuando falla una operación con documentos.
    /// </summary>
    /// <remarks>
    /// <see cref="DocumentoException"/> lleva un mensaje redactado para el
    /// usuario y explica qué hacer, así que se muestra tal cual. Cualquier otra
    /// excepción trae texto de la biblioteca de Word o del sistema, que puede
    /// incluir rutas internas y no ayuda a quien está rellenando un formulario:
    /// en ese caso se da una indicación general y el detalle queda en el
    /// registro de diagnóstico.
    /// </remarks>
    private static string TextoFallo(Exception excepcion, string accion)
        => excepcion is DocumentoException
            ? excepcion.Message
            : $"No se pudo {accion}." + Environment.NewLine + Environment.NewLine
              + "Compruebe que el documento no esté abierto en Word y que tenga "
              + "permisos para escribir en la carpeta elegida. El detalle del error "
              + "quedó anotado en el registro de diagnóstico.";
}
