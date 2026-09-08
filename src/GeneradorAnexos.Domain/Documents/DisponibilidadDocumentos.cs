using System.Globalization;
using GeneradorAnexos.Domain.Formatting;
using GeneradorAnexos.Domain.Models;
using GeneradorAnexos.Domain.Payments;
using GeneradorAnexos.Domain.Validation;

namespace GeneradorAnexos.Domain.Documents;

/// <summary>Reglas de generación y sus motivos; no dependen de guardar un registro.</summary>
public static class DisponibilidadDocumentos
{
    public static bool PuedeGenerarTdr(BorradorPayloadV1? datos) => PuedeGenerarTdr(datos?.Tdr);
    public static bool PuedeGenerarTdr(TdrPayload? tdr) => ErroresTdr(tdr).Count == 0;
    public static bool PuedeGenerarAnexos(BorradorPayloadV1? datos) => ErroresAnexos(datos).Count == 0;

    public static IReadOnlyList<string> ErroresTdr(TdrPayload? tdr)
    {
        var errores = new List<string>();
        var generales = tdr?.Generales;
        var objeto = tdr?.Objeto;
        Texto(errores, generales?.Oficina, "Área usuaria");
        Texto(errores, generales?.NumeroPedido, "Número de pedido");
        Texto(errores, generales?.ActividadPoi, "Actividad POI / Acción estratégica PEI");
        Texto(errores, generales?.FuenteFinanciamiento, "Fuente de financiamiento");
        Texto(errores, generales?.Meta, "Meta");
        Texto(errores, generales?.DenominacionServicio, "Denominación del servicio");
        Texto(errores, generales?.ObjetivoContratacion, "Objeto de la contratación");
        Texto(errores, generales?.DescripcionFinalidadPublica, "Finalidad pública");
        Texto(errores, generales?.ActividadesDesarrollar, "Actividades a desarrollar");
        Exigir(errores, FieldValidators.IsValidClassifier(generales?.Clasificador),
            "Clasificador de gasto: use seis grupos separados por puntos, por ejemplo 2.3.2.7.11.99.");
        Exigir(errores, FieldValidators.IsPositiveInteger(generales?.DiasPlazo),
            "Plazo de prestación: ingrese un número de días mayor que cero.");
        Exigir(errores, int.TryParse(objeto?.Cantidad, NumberStyles.None, CultureInfo.InvariantCulture,
            out var cantidad) && cantidad > 0, "Cantidad del objeto: ingrese un entero mayor que cero.");
        Texto(errores, objeto?.Unidad, "Unidad de medida del objeto");
        Texto(errores, objeto?.Descripcion, "Descripción del objeto");
        Exigir(errores, tdr?.Formacion?.Any(FieldValidators.IsNonEmptyText) == true,
            "Formación académica: agregue al menos un requisito.");
        Exigir(errores, tdr?.Experiencia?.Any(FieldValidators.IsNonEmptyText) == true,
            "Experiencia: agregue al menos un requisito.");
        Exigir(errores, tdr?.Capacitaciones?.Any(FieldValidators.IsNonEmptyText) == true,
            "Capacitación y/o programas: agregue al menos un requisito.");
        var modo = string.IsNullOrEmpty(tdr?.Modo) ? ConstructorPlanPagos.ModoUnico : tdr.Modo;
        if (modo == ConstructorPlanPagos.ModoUnico)
            Entregable(errores, tdr?.Unico, 1);
        else if (modo == ConstructorPlanPagos.ModoMultiple && tdr?.Entregables is { } entregables)
            for (var i = 0; i < entregables.Count; i++) Entregable(errores, entregables[i], i + 1);
        Plan(errores, tdr, null);
        return errores;
    }

    public static IReadOnlyList<string> ErroresAnexos(BorradorPayloadV1? datos)
    {
        var errores = new List<string>();
        var anexos = datos?.Anexos;
        // Este dato se muestra también en Anexos y se conserva en el campo histórico del TDR.
        Texto(errores, datos?.Tdr?.Generales?.Oficina, "Área usuaria");
        Texto(errores, anexos?.NombreProveedor, "Razón social / Nombre completo");
        Texto(errores, anexos?.DireccionProveedor, "Dirección");
        Texto(errores, anexos?.CuentaProveedor, "Entidad bancaria");
        Texto(errores, anexos?.DescripcionServicio, "Descripción del bien / servicio");
        Exigir(errores, FieldValidators.IsValidDni(anexos?.Dni), "DNI: ingrese exactamente 8 dígitos.");
        Exigir(errores, FieldValidators.IsValidRuc(anexos?.RucProveedor),
            "RUC: revise sus 11 dígitos y el dígito verificador.");
        Exigir(errores, FieldValidators.IsValidPhone(anexos?.CelularProveedor),
            "Teléfono: ingrese 9 dígitos; puede separarlos con espacios.");
        Exigir(errores, FieldValidators.IsValidEmail(anexos?.EmailProveedor), "Correo electrónico: revise su formato.");
        Exigir(errores, FieldValidators.IsValidCci(anexos?.CciProveedor), "CCI: ingrese exactamente 20 dígitos.");
        Exigir(errores, FieldValidators.IsPositiveInteger(anexos?.DiasPlazo),
            "Plazo de entrega: ingrese un número de días mayor que cero.");
        Exigir(errores, DocumentFormatting.TryParseAmount(anexos?.Monto, out var monto) && monto > 0,
            "Monto: ingrese un importe mayor que cero.");
        Plan(errores, datos?.Tdr, anexos?.Monto);
        return errores;
    }

    private static void Texto(List<string> errores, string? valor, string campo)
        => Exigir(errores, FieldValidators.IsNonEmptyText(valor), campo + ": complete este dato.");

    private static void Exigir(List<string> errores, bool valido, string mensaje)
    {
        if (!valido) errores.Add(mensaje);
    }

    private static void Entregable(List<string> errores, EntregablePayload? fila, int numero)
    {
        Texto(errores, fila?.Descripcion, $"Descripción del entregable {numero}");
        Exigir(errores, FieldValidators.IsPositiveInteger(TdrLabels.ExtraerCantidadDias(fila?.Plazo)),
            $"Plazo del entregable {numero}: ingrese un número de días mayor que cero.");
    }

    private static void Plan(List<string> errores, TdrPayload? tdr, string? monto)
    {
        try { _ = ConstructorPlanPagos.Construir(tdr, monto); }
        catch (PlanPagosException ex) { errores.Add("Forma de pago (TDR): " + ex.Message); }
    }
}
