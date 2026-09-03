using GeneradorAnexos.Application.Sync;
using GeneradorAnexos.Domain.Documents;
using GeneradorAnexos.Domain.Models;
using GeneradorAnexos.Domain.Payments;
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

var ruc = FieldValidators.Ruc10FromDni("12345678");
Equal("RUC 10 desde DNI tiene 11 dígitos", ruc.Length, 11);
Equal("RUC 10 prefijo", ruc.StartsWith("10", StringComparison.Ordinal), true);
Equal("RUC 10 dígito verificador", FieldValidators.IsValidRuc(ruc), true);
Equal("RUC prefijo inválido", FieldValidators.IsValidRuc("11123456789"), false);

Equal("CCI 20 dígitos", FieldValidators.IsValidCci(new string('1', 20)), true);
Equal("CCI corto", FieldValidators.IsValidCci("123"), false);
Equal("correo válido", FieldValidators.IsValidEmail("oti@munioxapampa.gob.pe"), true);
Equal("correo inválido", FieldValidators.IsValidEmail("oti@"), false);
Equal("celular 9 dígitos", FieldValidators.IsValidPhone("999888777"), true);
Equal("clasificador presupuestal", FieldValidators.IsValidClassifier("2.3.2.7.11.99"), true);
Equal("entero positivo", FieldValidators.IsPositiveInteger("30"), true);
Equal("cero no es positivo", FieldValidators.IsPositiveInteger("0"), false);

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

Console.WriteLine($"TOTAL: {checks} comprobaciones correctas.");
