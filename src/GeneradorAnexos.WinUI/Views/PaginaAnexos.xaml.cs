using GaSync = GeneradorAnexos.Application.Sync;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
/// Equivalente de la pestaña ANEXOS de <c>ui/ventana_principal.py</c>.
/// </summary>
public sealed partial class PaginaAnexos : UserControl
{
    private readonly PreferenciasUi _preferencias = new();
    private readonly GestorVistasPrevias _vistasPrevias = new("anexo");

    private VentanaPrincipal? _ventana;
    private GaSync.EstadoCompartido? _estado;
    private bool _consultandoDni;

    public PaginaAnexos()
    {
        InitializeComponent();
        EtiquetaVersion.Text = $"v{Constantes.AppVersion}";
        ConfigurarCampos();
    }

    /// <summary>Inyecta la ventana y el estado compartido tras construir el árbol.</summary>
    public void Inicializar(VentanaPrincipal ventana, GaSync.EstadoCompartido estado)
    {
        _ventana = ventana;
        _estado = estado;
    }

    // ═══════════════════════ Configuración de campos ═══════════════════════

    /// <summary>Aplica validadores, formateadores y opciones, como el original.</summary>
    private void ConfigurarCampos()
    {
        CampoNombre.Validacion = FieldValidators.IsNonEmptyText;

        FilaDniRuc.Dni.FiltroTeclado = t => SoloDigitos(t, Constantes.LongitudDni);
        FilaDniRuc.Dni.Validacion = FieldValidators.IsValidDni;
        FilaDniRuc.Ruc.FiltroTeclado = t => SoloDigitos(t, Constantes.LongitudRuc);
        FilaDniRuc.Ruc.Validacion = FieldValidators.IsValidRuc;
        FilaDniRuc.Validar += (_, _) => _ = ValidarDniAsync();

        CampoDireccion.Validacion = FieldValidators.IsNonEmptyText;

        CampoTelefono.FiltroTeclado = t => t.Length <= 11 && t.All(c => char.IsDigit(c) || c == ' ');
        CampoTelefono.Validacion = FieldValidators.IsValidPhone;
        CampoTelefono.Formateador = DocumentFormatting.FormatPhone;

        CampoEmail.Validacion = FieldValidators.IsValidEmail;

        CampoBanco.EstablecerOpciones(ServicioCatalogos.EntidadesBancarias);
        CampoBanco.Marcador = Constantes.MarcadorBanco;

        CampoCci.FiltroTeclado = t => SoloDigitos(t, Constantes.LongitudCci);
        CampoCci.Validacion = FieldValidators.IsValidCci;

        // Monto: dígitos y hasta dos decimales (validador_monto del original).
        CampoMonto.FiltroTeclado = EsMontoParcial;
        CampoMonto.Validacion = t => DocumentFormatting.TryParseAmount(t, out var m) && m > 0;
        CampoMonto.Cambiado += (_, _) => ActualizarMonto();

        CampoDias.FiltroTeclado = t => SoloDigitos(t, 3);
        CampoDias.Validacion = FieldValidators.IsPositiveInteger;
        CampoDias.UsarTecladoNumerico();
        CampoDias.Caja.TextAlignment = TextAlignment.Center;

        ResumenFormaPago.Text = ConstructorPlanPagos.TextoFormaPagoUnico;
    }

    private static bool SoloDigitos(string texto, int maximo)
        => texto.Length <= maximo && texto.All(char.IsDigit);

    /// <summary>Acepta un monto en construcción: "", "15", "1500.", "1500.50".</summary>
    private static bool EsMontoParcial(string texto)
    {
        if (texto.Length == 0)
        {
            return true;
        }

        var partes = texto.Split('.');
        if (partes.Length > 2)
        {
            return false;
        }

        if (partes[0].Length > 9 || !partes[0].All(char.IsDigit))
        {
            return false;
        }

        return partes.Length == 1 || (partes[1].Length <= 2 && partes[1].All(char.IsDigit));
    }

    /// <summary>Conecta los campos que se sincronizan con la pestaña TDR.</summary>
    public void ConectarEstado()
    {
        if (_estado is null)
        {
            return;
        }

        // Plazo: bidireccional entre TDR y Anexos.
        CampoDias.TextoCambiado += (_, _) => _estado.EstablecerPlazo(CampoDias.Valor, CampoDias);
        _estado.PlazoCambiado += (_, cambio) =>
        {
            if (!ReferenceEquals(cambio.Origen, CampoDias))
            {
                CampoDias.EstablecerValorSilencioso(cambio.Texto);
                CampoDias.DestellarSincronizacion();
            }
        };

        // Número de pedido: fuente única compartida.
        CampoNumeroPedido.TextoCambiado += (_, _) =>
            _estado.EstablecerNumeroPedido(CampoNumeroPedido.Valor, CampoNumeroPedido);
        _estado.NumeroPedidoCambiado += (_, cambio) =>
        {
            if (!ReferenceEquals(cambio.Origen, CampoNumeroPedido))
            {
                CampoNumeroPedido.EstablecerValorSilencioso(cambio.Texto);
                CampoNumeroPedido.DestellarSincronizacion();
            }
        };
    }

    /// <summary>Receptor "anexos_desc" del sincronizador unidireccional.</summary>
    public CampoArea CampoDescripcionServicio => CampoDescripcion;

    // ═══════════════════════ Consulta de DNI ═══════════════════════

    /// <summary>
    /// Botón «Validar»: deriva el RUC de forma local y avisa de que la consulta
    /// del nombre está pendiente.
    /// </summary>
    /// <remarks>
    /// La consulta automática del nombre está retirada a propósito. La versión
    /// anterior la resolvía leyendo los formularios públicos de un sitio
    /// privado, lo que implicaba enviar el DNI de un ciudadano fuera de la
    /// entidad sin convenio ni base legal, y dependía de que ese sitio no
    /// cambiara su maquetación.
    ///
    /// El RUC sí se sigue calculando aquí mismo, con el algoritmo de SUNAT y sin
    /// ninguna conexión. Es la parte útil del botón y funciona siempre.
    ///
    /// Queda pendiente integrar el servicio oficial de RENIEC cuando la entidad
    /// tramite el convenio correspondiente.
    /// </remarks>
    private async Task ValidarDniAsync()
    {
        if (_consultandoDni)
        {
            return;
        }

        if (!FilaDniRuc.Dni.ForzarValidacion())
        {
            FilaDniRuc.Dni.Enfocar();
            await ServicioDialogos.MostrarAdvertenciaAsync(
                "Validar DNI",
                "Ingrese un DNI válido de 8 dígitos antes de validar.");
            return;
        }

        _consultandoDni = true;
        FilaDniRuc.EstablecerConsultando(true);

        try
        {
            var ruc = FieldValidators.Ruc10FromDni(FilaDniRuc.Dni.Valor);
            if (string.IsNullOrEmpty(ruc))
            {
                await ServicioDialogos.MostrarAdvertenciaAsync(
                    "Validar DNI",
                    "No se pudo derivar el RUC a partir de ese DNI. Revise los dígitos.");
                return;
            }

            FilaDniRuc.Ruc.EstablecerValorSilencioso(ruc);
            FilaDniRuc.Ruc.DestellarSincronizacion();
            Registro.Info("DNI_RUC_DERIVED_LOCALLY");

            await ServicioDialogos.MostrarInformacionAsync(
                "RUC calculado",
                $"Se calculó el RUC {ruc} a partir del DNI."
                + Environment.NewLine + Environment.NewLine
                + "La consulta automática del nombre está en desarrollo. Se "
                + "habilitará en una próxima actualización, una vez integrado el "
                + "servicio oficial de RENIEC."
                + Environment.NewLine + Environment.NewLine
                + "Por ahora, escriba el nombre o la razón social a mano.");
        }
        finally
        {
            _consultandoDni = false;
            FilaDniRuc.EstablecerConsultando(false);
            CampoNombre.Enfocar();
        }
    }

    // ═══════════════════════ Monto y forma de pago ═══════════════════════

    private void ActualizarMonto()
    {
        MontoFormateado.Text = DocumentFormatting.TryParseAmount(CampoMonto.Valor, out var monto) && monto > 0
            ? DocumentFormatting.FormatCurrency(monto)
            : "S/ 0.00";

        ActualizarResumenFormaPago();
    }

    /// <summary>
    /// Recalcula el resumen desde el TDR. Nunca se guarda una copia del plan en
    /// Anexos: el TDR es la única fuente de verdad.
    /// </summary>
    public void ActualizarResumenFormaPago()
    {
        if (_ventana is null)
        {
            return;
        }

        try
        {
            var plan = PlanActual();
            ResumenFormaPago.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["Ga.TextoPanel"];

            if (plan.Modo == ConstructorPlanPagos.ModoUnico)
            {
                ResumenFormaPago.Text =
                    $"Único pago · 100 %. {ConstructorPlanPagos.TextoFormaPagoUnico}";
                return;
            }

            var cuotas = plan.Cuotas.Select(c =>
            {
                var importe = c.Monto is null
                    ? string.Empty
                    : $" · {DocumentFormatting.FormatCurrency(c.Monto.Value)}";
                return $"Pago {c.Indice}: {c.Porcentaje} %{importe}";
            });

            ResumenFormaPago.Text =
                $"{plan.Cuotas.Count} entregables y pagos sincronizados desde el TDR. " +
                string.Join("  |  ", cuotas);
        }
        catch (PlanPagosException excepcion)
        {
            ResumenFormaPago.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["Ga.MensajeError"];
            ResumenFormaPago.Text =
                "Plan pendiente de corrección en TDR › Entregables / Forma de Pago. " +
                excepcion.Message;
        }
    }

    private PlanPagos PlanActual()
    {
        var monto = CampoMonto.Valor;
        return ConstructorPlanPagos.Construir(
            _ventana!.PaginaTdrVista.ExportarEstado(),
            string.IsNullOrWhiteSpace(monto) ? null : monto);
    }

    /// <summary>Valida el plan antes de generar; avisa y aborta si es incoherente.</summary>
    private async Task<PlanPagos?> ValidarPlanAsync()
    {
        try
        {
            var plan = PlanActual();
            ActualizarResumenFormaPago();
            return plan;
        }
        catch (PlanPagosException excepcion)
        {
            ActualizarResumenFormaPago();
            await ServicioDialogos.MostrarAdvertenciaAsync(
                "Forma de pago incompleta",
                $"No se puede generar un Anexo contradictorio con el TDR.{Environment.NewLine}{Environment.NewLine}" +
                $"{excepcion.Message}{Environment.NewLine}{Environment.NewLine}" +
                "Corrija Entregables y Forma de Pago en la pestaña TDR.");
            return null;
        }
    }

    // ═══════════════════════ Validación del formulario ═══════════════════════

    private ICampo[] CamposFormulario() => new ICampo[]
    {
        CampoNombre, FilaDniRuc.Dni, FilaDniRuc.Ruc, CampoDireccion,
        CampoTelefono, CampoEmail, CampoBanco, CampoCci,
        CampoDescripcion, CampoMonto, CampoDias, CampoNumeroPedido,
    };

    private IEnumerable<ICampo> CamposObligatorios()
        => CamposFormulario().Where(campo => campo.Obligatorio);

    /// <summary>Valida todos los campos (sin cortocircuito) para resaltarlos en rojo.</summary>
    private bool ValidarTodo()
        => CamposObligatorios().Select(c => c.ForzarValidacion()).ToList().All(r => r);

    private int ContarFaltantes() => CamposObligatorios().Count(c => !c.EsValido);

    /// <summary>Aviso comun de campos obligatorios sin completar.</summary>
    private async Task AvisarCamposFaltantesAsync()
    {
        var n = ContarFaltantes();
        var plural = n != 1;
        await ServicioDialogos.MostrarAdvertenciaAsync(
            "Datos incompletos",
            $"Falta{(plural ? "n" : string.Empty)} {n} campo{(plural ? "s" : string.Empty)} " +
            $"obligatorio{(plural ? "s" : string.Empty)} " +
            $"(resaltado{(plural ? "s" : string.Empty)} en rojo).");
    }

    private void EnfocarPrimerInvalido()
    {
        var campo = CamposObligatorios().FirstOrDefault(c => !c.EsValido);
        campo?.Enfocar();
    }

    // ═══════════════════════ Contexto documental ═══════════════════════

    /// <summary>Equivalente de <c>_recolectar_contexto</c>.</summary>
    private Dictionary<string, string> RecolectarContexto()
    {
        // Tolerante a proposito: quien decide si falta el monto es la
        // validacion del formulario, no la construccion del contexto.
        var montoValido = DocumentFormatting.TryParseAmount(CampoMonto.Valor, out var monto) && monto > 0;
        var partes = DocumentFormatting.GetDateParts(_ventana!.FechaDocumento);

        var contexto = new Dictionary<string, string>
        {
            ["OFICINA"] = _ventana.PaginaTdrVista.Oficina,
            ["NOMBRE_PROVEEDOR"] = CampoNombre.Valor,
            ["DNI"] = FilaDniRuc.Dni.Valor,
            ["RUC_PROVEEDOR"] = FilaDniRuc.Ruc.Valor,
            ["DIRECCION_PROVEEDOR"] = CampoDireccion.Valor,
            ["CEL_PROVEEDOR"] = CampoTelefono.Valor,
            ["EMAIL_PROVEEDOR"] = CampoEmail.Valor,
            ["CUENTA_PROVEEDOR"] = CampoBanco.Valor,
            ["CCI_PROVEEDOR"] = CampoCci.Valor,
            ["DESCRIPCION_SERVICIO"] = CampoDescripcion.Valor,
            ["MONTO"] = montoValido
                ? DocumentFormatting.FormatCurrencyWithoutSymbol(monto)
                : string.Empty,
            ["MONTO_EN_LETRAS"] = montoValido
                ? SpanishNumberConverter.AmountToWords(monto)
                : string.Empty,
            // 30 -> "treinta (30)" para la redacción legal del plazo.
            ["DIAS_PLAZO"] = SpanishNumberConverter.NumberWithWords(CampoDias.Valor),
            ["DIA"] = partes.Dia,
            ["MES"] = partes.Mes,
            ["ANO"] = partes.Anio,
            [GaSync.EstadoCompartido.ClaveNumeroPedido] = CampoNumeroPedido.Valor,
        };

        return contexto;
    }

    /// <summary>
    /// Nombre propuesto al guardar los Anexos. Lleva el número de pedido y la
    /// fecha para que dos documentos del mismo proveedor no se pisen entre sí.
    /// </summary>
    private string NombreSugerido()
        => NombreDocumento.Componer(
            "Anexos_06-09", CampoNumeroPedido.Valor, CampoNombre.Valor, "Proveedor");

    // ═══════════════════════ Acciones ═══════════════════════

    private void AlGenerar(object sender, RoutedEventArgs e) => _ = GenerarAsync();

    private void AlLimpiar(object sender, RoutedEventArgs e) => _ = LimpiarAsync();

    private void AlVistaPrevia(object sender, RoutedEventArgs e) => _ = VistaPreviaAsync();

    /// <summary>Equivalente de <c>_generar</c>.</summary>
    public async Task GenerarAsync()
    {
        if (!ValidarTodo())
        {
            EnfocarPrimerInvalido();
            await AvisarCamposFaltantesAsync();
            return;
        }

        var plan = await ValidarPlanAsync();
        if (plan is null)
        {
            return;
        }

        var ruta = await SelectorArchivos.GuardarComoAsync(
            _ventana!, "Guardar anexos", NombreSugerido(),
            "Documento de Word", ".docx", _preferencias.CarpetaGuardar());

        if (string.IsNullOrEmpty(ruta))
        {
            return;
        }

        try
        {
            await ServiciosApp.Documentos.GenerateAnnexesAsync(
                RecolectarContexto(), ruta, plan, default);
        }
        catch (Exception excepcion)
        {
            Registro.Error("ANEXO_GENERATION_FAILED", excepcion);
            await ServicioDialogos.MostrarErrorAsync(
                "Error al generar",
                TextoFallo(excepcion, "generar el documento"));
            return;
        }

        _preferencias.RecordarCarpeta(ruta);
        Registro.Info("ANEXO_GENERATION_OK");
        await MostrarExitoAsync(ruta);
    }

    private static async Task MostrarExitoAsync(string ruta)
    {
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

    /// <summary>Equivalente de <c>_vista_previa_anexos</c>.</summary>
    public async Task VistaPreviaAsync()
    {
        // La vista previa admite un formulario parcial. Solo Generar aplica la
        // validación obligatoria y el plan estricto.
        var plan = ConstructorPlanPagos.ConstruirVistaPrevia(
            _ventana!.PaginaTdrVista.ExportarEstado(),
            CampoMonto.Valor);

        var ruta = _vistasPrevias.CrearRuta();
        try
        {
            await ServiciosApp.Documentos.GenerateAnnexesAsync(
                RecolectarContexto(), ruta, plan, default);
        }
        catch (Exception excepcion)
        {
            _vistasPrevias.Descartar(ruta);
            Registro.Error("ANEXO_PREVIEW_FAILED", excepcion);
            await ServicioDialogos.MostrarErrorAsync(
                "Error en la vista previa",
                TextoFallo(excepcion, "generar la vista previa"));
            return;
        }

        await AccionDocumento.AbrirAsync(ruta);
    }

    /// <summary>Equivalente de <c>_limpiar</c>.</summary>
    public async Task LimpiarAsync()
    {
        if (!await ServicioDialogos.PreguntarSiNoAsync(
                "Limpiar formulario", "¿Desea borrar todos los datos ingresados?"))
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
        foreach (var campo in CamposFormulario())
        {
            campo.Limpiar();
        }

        ActualizarMonto();
    }

    public void LimpiarVistasPrevias() => _vistasPrevias.LimpiarTodo();

    // ═══════════════════════ Serialización ═══════════════════════

    public AnexosPayload ExportarEstado() => new()
    {
        NombreProveedor = CampoNombre.Valor,
        Dni = FilaDniRuc.Dni.Valor,
        RucProveedor = FilaDniRuc.Ruc.Valor,
        DireccionProveedor = CampoDireccion.Valor,
        CelularProveedor = CampoTelefono.Valor,
        EmailProveedor = CampoEmail.Valor,
        CuentaProveedor = CampoBanco.Valor,
        CciProveedor = CampoCci.Valor,
        DescripcionServicio = CampoDescripcion.Valor,
        Monto = CampoMonto.Valor,
        DiasPlazo = CampoDias.Valor,
        NumeroPedido = CampoNumeroPedido.Valor,
    };

    public void ImportarEstado(AnexosPayload? datos)
    {
        datos ??= new AnexosPayload();

        CampoNombre.EstablecerValorSilencioso(datos.NombreProveedor);
        FilaDniRuc.Dni.EstablecerValorSilencioso(datos.Dni);
        FilaDniRuc.Ruc.EstablecerValorSilencioso(datos.RucProveedor);
        CampoDireccion.EstablecerValorSilencioso(datos.DireccionProveedor);
        CampoTelefono.EstablecerValorSilencioso(datos.CelularProveedor);
        CampoEmail.EstablecerValorSilencioso(datos.EmailProveedor);
        CampoBanco.EstablecerValorSilencioso(datos.CuentaProveedor);
        CampoCci.EstablecerValorSilencioso(datos.CciProveedor);
        CampoDescripcion.EstablecerValorSilencioso(datos.DescripcionServicio);
        CampoMonto.EstablecerValorSilencioso(datos.Monto);
        CampoDias.EstablecerValorSilencioso(datos.DiasPlazo);
        CampoNumeroPedido.EstablecerValorSilencioso(datos.NumeroPedido);

        ActualizarMonto();
    }

    /// <summary>Coloca en el Anexo el monto extraído del Pedido SIGA.</summary>
    public bool EstablecerMonto(string? valor)
    {
        try
        {
            CampoMonto.EstablecerValor(valor);
            ActualizarMonto();
            return true;
        }
        catch (Exception excepcion)
        {
            Registro.Error("SET_AMOUNT_FAILED", excepcion);
            return false;
        }
    }

    /// <summary>
    /// Texto que se muestra cuando falla una operación con documentos.
    /// </summary>
    /// <remarks>
    /// <see cref="DocumentoException"/> lleva un mensaje redactado para el
    /// usuario. Cualquier otra excepción trae texto de la biblioteca de Word o
    /// del sistema, que puede incluir rutas internas del equipo: en ese caso se
    /// da una indicación general y el detalle queda en el registro.
    /// </remarks>
    private static string TextoFallo(Exception excepcion, string accion)
        => excepcion is DocumentoException
            ? excepcion.Message
            : $"No se pudo {accion}." + Environment.NewLine + Environment.NewLine
              + "Compruebe que el documento no esté abierto en Word y que tenga "
              + "permisos para escribir en la carpeta elegida. El detalle del error "
              + "quedó anotado en el registro de diagnóstico.";
}
