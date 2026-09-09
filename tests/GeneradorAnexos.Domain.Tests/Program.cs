using GeneradorAnexos.Application.Sync;
using GeneradorAnexos.Domain.Documents;
using GeneradorAnexos.Domain.Models;
using GeneradorAnexos.Domain.Payments;
using GeneradorAnexos.Domain.Serialization;
using GeneradorAnexos.Domain.Validation;

var checks = 0;
void Equal(string name, object? actual, object? wanted)
{
    if (!Equals(actual, wanted))
    {
        throw new Exception($"{name}: esperado [{wanted}], obtenido [{actual}]");
    }

    checks++;
    Console.WriteLine("OK " + name);
}

void Throws<T>(string name, Action action) where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        checks++;
        Console.WriteLine("OK " + name);
        return;
    }

    throw new Exception(name + ": no se rechazó la entrada.");
}

// ── Validadores ──────────────────────────────────────────────────────────
Equal("DNI 8 dígitos", FieldValidators.IsValidDni("12345678"), true);
Equal("DNI corto", FieldValidators.IsValidDni("1234567"), false);
Equal("DNI con letra", FieldValidators.IsValidDni("1234567A"), false);
Equal("DNI rechaza dígitos Unicode", FieldValidators.IsValidDni("１２３４５６７８"), false);

var ruc = FieldValidators.Ruc10FromDni("12345678");
Equal("RUC 10 desde DNI tiene 11 dígitos", ruc.Length, 11);
Equal("RUC 10 prefijo", ruc.StartsWith("10", StringComparison.Ordinal), true);
Equal("RUC 10 dígito verificador", FieldValidators.IsValidRuc(ruc), true);
Equal("RUC prefijo inválido", FieldValidators.IsValidRuc("11123456789"), false);

Equal("CCI 20 dígitos", FieldValidators.IsValidCci(new string('1', 20)), true);
Equal("CCI corto", FieldValidators.IsValidCci("123"), false);
Equal("CCI rechaza dígitos Unicode", FieldValidators.IsValidCci("１２３４５６７８９０１２３４５６７８９０"), false);
Equal("correo válido", FieldValidators.IsValidEmail("oti@munioxapampa.gob.pe"), true);
Equal("correo inválido", FieldValidators.IsValidEmail("oti@"), false);
Equal("correo demasiado largo", FieldValidators.IsValidEmail(new string('a', 250) + "@x.pe"), false);
Equal("celular 9 dígitos", FieldValidators.IsValidPhone("999888777"), true);
Equal("celular con separador no permitido", FieldValidators.IsValidPhone("999-888-777"), false);
Equal("clasificador presupuestal", FieldValidators.IsValidClassifier("2.3.2.7.11.99"), true);
Equal("clasificador con raíz incorrecta", FieldValidators.IsValidClassifier("3.3.2.7.11.99"), false);
Equal("clasificador con segmentos incompletos", FieldValidators.IsValidClassifier("2.3.2.7.11"), false);
Equal("entero positivo", FieldValidators.IsPositiveInteger("30"), true);
Equal("cero no es positivo", FieldValidators.IsPositiveInteger("0"), false);
Equal("entero Unicode rechazado", FieldValidators.IsPositiveInteger("٣٠"), false);

Throws<PayloadJsonException>("payload con versión futura", () =>
    PayloadJson.Serialize(new BorradorPayloadV1 { Version = BorradorPayloadV1.VersionActual + 1 }));
Throws<PayloadJsonException>("payload excesivamente grande", () =>
    PayloadJson.Serialize(new BorradorPayloadV1
    {
        Anexos = new AnexosPayload
        {
            DireccionProveedor = new string('x', PayloadJson.MaxJsonBytes),
        },
    }));

// ── Disponibilidad real de documentos ───────────────────────────────────
var listo = CrearRegistroListo();
Equal("TDR completo habilita generación", DisponibilidadDocumentos.PuedeGenerarTdr(listo), true);
Equal("Anexo completo habilita generación", DisponibilidadDocumentos.PuedeGenerarAnexos(listo), true);

listo.Tdr!.Generales!.Clasificador = "2.3";
Equal("TDR con clasificador incompleto no genera", DisponibilidadDocumentos.PuedeGenerarTdr(listo), false);
listo.Tdr.Generales.Clasificador = "2.3.2.7.11.99";

listo.Anexos!.CciProveedor = "123";
Equal("Anexo con CCI incompleto no genera", DisponibilidadDocumentos.PuedeGenerarAnexos(listo), false);
listo.Anexos.CciProveedor = new string('1', 20);

listo.Tdr!.Generales!.Oficina = string.Empty;
Equal("Anexo sin área usuaria no genera", DisponibilidadDocumentos.PuedeGenerarAnexos(listo), false);
listo.Tdr.Generales.Oficina = "OFICINA DE TECNOLOGÍA DE LA INFORMACIÓN";

listo.Tdr.Modo = ConstructorPlanPagos.ModoMultiple;
listo.Tdr.Entregables =
[
    new EntregablePayload { Descripcion = "Informe 1", Plazo = "15 días" },
    new EntregablePayload { Descripcion = "Informe 2", Plazo = "30 días" },
];
listo.Tdr.Pagos =
[
    new PagoPayload { Condicion = "Primer pago", Porcentaje = 50 },
    new PagoPayload { Condicion = "Segundo pago", Porcentaje = 40 },
];
Equal("TDR con pagos que no suman 100 no genera", DisponibilidadDocumentos.PuedeGenerarTdr(listo), false);
Equal("Anexo con plan TDR incoherente no genera", DisponibilidadDocumentos.PuedeGenerarAnexos(listo), false);
listo.Tdr.Pagos[1]!.Porcentaje = 50;
Equal("TDR múltiple coherente habilita generación", DisponibilidadDocumentos.PuedeGenerarTdr(listo), true);
Equal("Anexo con plan coherente habilita generación", DisponibilidadDocumentos.PuedeGenerarAnexos(listo), true);

Equal(
    "un solo campo de Anexo no basta para generar",
    DisponibilidadDocumentos.PuedeGenerarAnexos(new BorradorPayloadV1
    {
        Anexos = new AnexosPayload { NombreProveedor = "Solo un dato" },
    }),
    false);

// Regresión: Anexos no debe exigir un TDR completo ni guardar un registro.
var soloAnexos = CrearRegistroListo();
soloAnexos.Tdr = new TdrPayload
{
    Generales = new CamposGeneralesTdrPayload { Oficina = "LOGÍSTICA" },
    Modo = ConstructorPlanPagos.ModoUnico,
};
soloAnexos.Anexos!.CelularProveedor = "999 888 777";
soloAnexos.Anexos.Monto = "S/ 1,234.50";
Equal("Anexo completo sin campos exclusivos del TDR", DisponibilidadDocumentos.PuedeGenerarAnexos(soloAnexos), true);
Equal("Anexo válido no informa errores", DisponibilidadDocumentos.ErroresAnexos(soloAnexos).Count, 0);
soloAnexos.Tdr.Modo = ConstructorPlanPagos.ModoMultiple;
Equal("Anexo sin pagos registrados usa pago único", ConstructorPlanPagos.ConstruirParaAnexos(soloAnexos.Tdr, "100").Modo, ConstructorPlanPagos.ModoUnico);
soloAnexos.Tdr.Pagos = [new PagoPayload { Condicion = "Primera conformidad", Porcentaje = 40 }, new PagoPayload { Condicion = "Segunda conformidad", Porcentaje = 60 }];
Equal("Anexo con pagos no exige entregables ni TDR completo", DisponibilidadDocumentos.PuedeGenerarAnexos(soloAnexos), true);
var pagosAnexo = ConstructorPlanPagos.ConstruirParaAnexos(soloAnexos.Tdr, "100");
Equal("Anexo sincroniza condición", pagosAnexo.Cuotas[1].Condicion, "Segunda conformidad");
Equal("Anexo sincroniza porcentaje", pagosAnexo.Cuotas[1].Porcentaje, 60);
soloAnexos.Tdr.Pagos[1]!.Porcentaje = 50;
Equal("Anexo no sustituye pagos incoherentes por pago único", DisponibilidadDocumentos.PuedeGenerarAnexos(soloAnexos), false);
soloAnexos.Tdr.Pagos[1]!.Porcentaje = 60;
soloAnexos.Tdr.Modo = ConstructorPlanPagos.ModoUnico;
Equal("Cambio a pago único ignora cuotas múltiples antiguas", ConstructorPlanPagos.ConstruirParaAnexos(soloAnexos.Tdr, "100").Modo, ConstructorPlanPagos.ModoUnico);
soloAnexos.Tdr.Generales!.Oficina = "";
Equal("Área ausente se identifica explícitamente", DisponibilidadDocumentos.ErroresAnexos(soloAnexos).Single(),
    "Área usuaria: complete este dato.");
soloAnexos.Tdr.Generales.Oficina = "LOGÍSTICA";
var recargado = PayloadJson.Deserialize(PayloadJson.Serialize(soloAnexos));
Equal("Área compartida conserva contrato JSON", recargado.Tdr!.Generales!.Oficina, "LOGÍSTICA");
Equal("Anexo recuperado permite generar", DisponibilidadDocumentos.PuedeGenerarAnexos(recargado), true);
soloAnexos.Anexos.CciProveedor = "123";
Equal("CCI inválido identifica su campo", DisponibilidadDocumentos.ErroresAnexos(soloAnexos).Single().StartsWith("CCI:", StringComparison.Ordinal), true);
var soloTdr = CrearRegistroListo();
soloTdr.Anexos = null;
Equal("TDR completo no depende de Anexos", DisponibilidadDocumentos.PuedeGenerarTdr(soloTdr), true);
Equal("TDR válido no informa errores", DisponibilidadDocumentos.ErroresTdr(soloTdr.Tdr).Count, 0);
soloTdr.Tdr!.Generales!.Clasificador = "2.3";
Equal("Clasificador inválido identifica su campo", DisponibilidadDocumentos.ErroresTdr(soloTdr.Tdr).Single().StartsWith("Clasificador", StringComparison.Ordinal), true);
soloTdr = CrearRegistroListo();
soloTdr.Tdr!.Unico!.Plazo = "0";
Equal("Plazo inválido identifica entregable", DisponibilidadDocumentos.ErroresTdr(soloTdr.Tdr).Single().StartsWith("Plazo del entregable 1:", StringComparison.Ordinal), true);
listo.Tdr!.Pagos![1]!.Porcentaje = 40;
Equal("Plan inválido informa suma requerida", DisponibilidadDocumentos.ErroresTdr(listo.Tdr).Any(e => e.Contains("100 %")), true);

// ── Etiquetas y porcentajes ──────────────────────────────────────────────
Equal("plazo numérico", TdrLabels.ExtraerCantidadDias("30"), "30");
Equal("plazo con texto", TdrLabels.ExtraerCantidadDias("Hasta treinta (30) días calendario"), "30");
Equal("plazo sufijo plural", TdrLabels.DiasConSufijo("30"), "30 días");
Equal("plazo sufijo singular", TdrLabels.DiasConSufijo("1"), "1 día");
Equal("plazo inválido", TdrLabels.DiasConSufijo("sin plazo"), "");

void ComprobarDistribucion(int cantidad, params int[] esperados)
{
    var valores = TdrLabels.DistribuirPorcentajes(cantidad);
    Equal($"distribución de {cantidad} pagos", string.Join(",", valores), string.Join(",", esperados));
    Equal($"total de {cantidad} pagos", valores.Sum(), 100);
}

ComprobarDistribucion(1, 100);
ComprobarDistribucion(2, 50, 50);
ComprobarDistribucion(3, 33, 33, 34);
ComprobarDistribucion(4, 25, 25, 25, 25);
ComprobarDistribucion(6, 16, 16, 16, 16, 16, 20);

for (var cantidad = 1; cantidad <= 100; cantidad++)
{
    var valores = TdrLabels.DistribuirPorcentajes(cantidad);
    Equal($"cantidad de filas para {cantidad}", valores.Length, cantidad);
    Equal($"suma exacta para {cantidad}", valores.Sum(), 100);
}

Equal("cero pagos", TdrLabels.DistribuirPorcentajes(0).Length, 0);

// ── Plan de pagos ────────────────────────────────────────────────────────
var unico = ConstructorPlanPagos.Construir(new TdrPayload { Modo = "unico" }, "1,500.00");
Equal("modo único porcentaje", unico.Cuotas[0].Porcentaje, 100);
Equal("modo único monto", unico.Cuotas[0].Monto, 1500.00m);

var tdrMultiple = new TdrPayload
{
    Modo = ConstructorPlanPagos.ModoMultiple,
    Entregables =
    [
        new EntregablePayload { Descripcion = "Informe 1", Plazo = "15 días" },
        new EntregablePayload { Descripcion = "Informe 2", Plazo = "30 días" },
        new EntregablePayload { Descripcion = "Informe 3", Plazo = "45 días" },
    ],
    Pagos =
    [
        new PagoPayload { Condicion = "Primer pago", Porcentaje = 33 },
        new PagoPayload { Condicion = "Segundo pago", Porcentaje = 33 },
        new PagoPayload { Condicion = "Tercer pago", Porcentaje = 34 },
    ],
};

var multiple = ConstructorPlanPagos.Construir(tdrMultiple, "100.00");
Equal("tres cuotas", multiple.Cuotas.Count, 3);
Equal("suma montos", multiple.Cuotas.Sum(c => c.Monto), 100.00m);
Equal("centavos primera", multiple.Cuotas[0].Monto, 33.00m);
Equal("centavos segunda", multiple.Cuotas[1].Monto, 33.00m);
Equal("centavos tercera", multiple.Cuotas[2].Monto, 34.00m);

Throws<PlanPagosException>("suma distinta de 100", () =>
{
    tdrMultiple.Pagos![2]!.Porcentaje = 30;
    ConstructorPlanPagos.Construir(tdrMultiple, "100.00");
});
tdrMultiple.Pagos![2]!.Porcentaje = 34;

Throws<PlanPagosException>("un solo entregable en múltiple", () =>
    ConstructorPlanPagos.Construir(new TdrPayload
    {
        Modo = ConstructorPlanPagos.ModoMultiple,
        Entregables = [new EntregablePayload { Descripcion = "A", Plazo = "1" }],
        Pagos = [new PagoPayload { Condicion = "A", Porcentaje = 100 }],
    }, "10.00"));

Throws<PlanPagosException>("monto cero", () =>
    ConstructorPlanPagos.Construir(new TdrPayload { Modo = "unico" }, "0"));

var vista = ConstructorPlanPagos.ConstruirVistaPrevia(new TdrPayload
{
    Modo = ConstructorPlanPagos.ModoMultiple,
    Entregables = [new EntregablePayload { Descripcion = "A", Plazo = "" }],
}, "abc");
Equal("vista previa no lanza", vista.Modo, ConstructorPlanPagos.ModoMultiple);

// ── Sincronización (Application) ─────────────────────────────────────────
var origen = "PRIMER MOTIVO";
var objeto = "";
var cuadro = "";
var anexos = "";
var sync = new SincronizadorUnidireccional(() => origen);
sync.Agregar("objeto", () => objeto, value => objeto = value);
sync.Agregar("cuadro", () => cuadro, value => cuadro = value);
sync.Agregar("anexos_desc", () => anexos, value => anexos = value);
sync.Propagar();
Equal("sincronización objeto", objeto, origen);
Equal("sincronización cuadro", cuadro, origen);
Equal("sincronización anexos", anexos, origen);
objeto = "EDICIÓN MANUAL";
sync.NotificarEdicion("objeto");
origen = "SEGUNDO MOTIVO";
sync.Propagar();
Equal("conservar edición manual", objeto, "EDICIÓN MANUAL");
Equal("actualizar cuadro no personalizado", cuadro, origen);
Equal("actualizar anexos no personalizados", anexos, origen);

var state = new EstadoCompartido();
var numeroAnexos = "";
state.NumeroPedidoCambiado += (_, change) => numeroAnexos = change.Texto;
state.EstablecerNumeroPedido("000123-A");
Equal("número compartido conserva ceros y sufijo", numeroAnexos, "000123-A");
state.EstablecerNumeroPedido("000123-A");
Equal("mismo número no reemite", numeroAnexos, "000123-A");

var origenOficinaTdr = new object();
var origenOficinaAnexos = new object();
var cambiosOficina = new List<CambioCompartido>();
state.OficinaCambiada += (_, cambio) => cambiosOficina.Add(cambio);
state.EstablecerOficina("LOGÍSTICA", origenOficinaAnexos);
Equal("Área desde Anexos se comparte", state.Oficina, "LOGÍSTICA");
Equal("Área conserva origen Anexos", cambiosOficina[^1].Origen, origenOficinaAnexos);
state.EstablecerOficina("LOGÍSTICA", origenOficinaTdr);
Equal("Área repetida no genera bucle", cambiosOficina.Count, 1);
state.EstablecerOficina("INFORMÁTICA", origenOficinaTdr);
Equal("Área desde TDR se comparte", cambiosOficina[^1].Texto, "INFORMÁTICA");
Equal("Área conserva origen TDR", cambiosOficina[^1].Origen, origenOficinaTdr);
state.EstablecerOficina(null, origenOficinaTdr);
Equal("Limpiar TDR propaga área vacía", state.Oficina, "");

Console.WriteLine($"TOTAL: {checks} comprobaciones correctas.");

static BorradorPayloadV1 CrearRegistroListo()
{
    const string dni = "12345678";
    return new BorradorPayloadV1
    {
        Fecha = "2026-08-10",
        Tdr = new TdrPayload
        {
            Generales = new CamposGeneralesTdrPayload
            {
                Oficina = "OFICINA DE TECNOLOGÍA DE LA INFORMACIÓN",
                NumeroPedido = "001211",
                ActividadPoi = "Gestión administrativa",
                FuenteFinanciamiento = "Recursos Ordinarios",
                Meta = "0001",
                Clasificador = "2.3.2.7.11.99",
                DenominacionServicio = "Servicio de soporte técnico",
                ObjetivoContratacion = "Contratar soporte técnico",
                DescripcionFinalidadPublica = "Mantener la continuidad operativa",
                ActividadesDesarrollar = "Atender incidencias informáticas",
                DiasPlazo = "30",
            },
            Objeto = new ObjetoServicioPayload
            {
                Cantidad = "1",
                Unidad = "SERVICIO",
                Descripcion = "Servicio de soporte técnico",
            },
            Modo = ConstructorPlanPagos.ModoUnico,
            Unico = new EntregablePayload
            {
                Descripcion = "Informe de actividades",
                Plazo = "30 días",
            },
            Formacion = ["Profesional o técnico en informática"],
            Experiencia = ["Experiencia en soporte técnico"],
            Capacitaciones = ["Capacitación en tecnologías de información"],
        },
        Anexos = new AnexosPayload
        {
            NombreProveedor = "Juan Pérez Quispe",
            Dni = dni,
            RucProveedor = FieldValidators.Ruc10FromDni(dni),
            DireccionProveedor = "Jr. Bolognesi 123",
            CelularProveedor = "999888777",
            EmailProveedor = "proveedor@example.com",
            CuentaProveedor = "Banco de la Nación",
            CciProveedor = new string('1', 20),
            DescripcionServicio = "Servicio de soporte técnico",
            Monto = "1500.00",
            DiasPlazo = "30",
            NumeroPedido = "001211",
        },
    };
}
