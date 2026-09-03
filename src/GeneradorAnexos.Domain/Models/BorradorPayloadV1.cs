#nullable enable

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeneradorAnexos.Domain.Models;

/// <summary>
/// Contrato JSON versión 1 de un borrador o registro del aplicativo.
/// </summary>
/// <remarks>
/// Los diccionarios marcados con <see cref="JsonExtensionDataAttribute"/>
/// permiten leer y volver a guardar campos agregados por versiones futuras sin
/// descartarlos.
/// </remarks>
public sealed class BorradorPayloadV1
{
    public const int VersionActual = 1;

    [JsonPropertyName("version")]
    public int Version { get; set; } = VersionActual;

    /// <summary>Fecha documental en formato <c>yyyy-MM-dd</c>.</summary>
    [JsonPropertyName("fecha")]
    public string? Fecha { get; set; } = string.Empty;

    [JsonPropertyName("anexos")]
    public AnexosPayload? Anexos { get; set; } = new();

    [JsonPropertyName("tdr")]
    public TdrPayload? Tdr { get; set; } = new();

    [JsonPropertyName("sync_personalizado")]
    public Dictionary<string, bool>? SincronizacionPersonalizada { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}

/// <summary>Valores en crudo del formulario de Anexos.</summary>
public sealed class AnexosPayload
{
    [JsonPropertyName("NOMBRE_PROVEEDOR")]
    public string? NombreProveedor { get; set; } = string.Empty;

    [JsonPropertyName("DNI")]
    public string? Dni { get; set; } = string.Empty;

    [JsonPropertyName("RUC_PROVEEDOR")]
    public string? RucProveedor { get; set; } = string.Empty;

    [JsonPropertyName("DIRECCION_PROVEEDOR")]
    public string? DireccionProveedor { get; set; } = string.Empty;

    [JsonPropertyName("CEL_PROVEEDOR")]
    public string? CelularProveedor { get; set; } = string.Empty;

    [JsonPropertyName("EMAIL_PROVEEDOR")]
    public string? EmailProveedor { get; set; } = string.Empty;

    [JsonPropertyName("CUENTA_PROVEEDOR")]
    public string? CuentaProveedor { get; set; } = string.Empty;

    [JsonPropertyName("CCI_PROVEEDOR")]
    public string? CciProveedor { get; set; } = string.Empty;

    [JsonPropertyName("DESCRIPCION_SERVICIO")]
    public string? DescripcionServicio { get; set; } = string.Empty;

    /// <summary>Monto como texto de entrada, sin normalizar.</summary>
    [JsonPropertyName("MONTO")]
    public string? Monto { get; set; } = string.Empty;

    [JsonPropertyName("DIAS_PLAZO")]
    public string? DiasPlazo { get; set; } = string.Empty;

    /// <summary>Número literal; conserva ceros iniciales, letras y guiones.</summary>
    [JsonPropertyName("NUM_PEDIDO")]
    public string? NumeroPedido { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}

/// <summary>Estado completo del formulario de Términos de Referencia.</summary>
public sealed class TdrPayload
{
    [JsonPropertyName("generales")]
    public CamposGeneralesTdrPayload? Generales { get; set; } = new();

    [JsonPropertyName("objeto")]
    public ObjetoServicioPayload? Objeto { get; set; } = new();

    /// <summary>Código compatible con Python: <c>unico</c> o <c>multiple</c>.</summary>
    [JsonPropertyName("modo")]
    public string? Modo { get; set; } = "unico";

    [JsonPropertyName("unico")]
    public EntregablePayload? Unico { get; set; } = new();

    [JsonPropertyName("entregables")]
    public List<EntregablePayload?>? Entregables { get; set; } = new();

    [JsonPropertyName("pagos")]
    public List<PagoPayload?>? Pagos { get; set; } = new();

    [JsonPropertyName("requisitos")]
    public List<string?>? Requisitos { get; set; } = new();

    [JsonPropertyName("formacion")]
    public List<string?>? Formacion { get; set; } = new();

    [JsonPropertyName("experiencia")]
    public List<string?>? Experiencia { get; set; } = new();

    [JsonPropertyName("capacitaciones")]
    public List<string?>? Capacitaciones { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}

/// <summary>Campos generales del TDR, usando las claves históricas de plantilla.</summary>
public sealed class CamposGeneralesTdrPayload
{
    [JsonPropertyName("OFICINA")]
    public string? Oficina { get; set; } = string.Empty;

    [JsonPropertyName("NUM_PEDIDO")]
    public string? NumeroPedido { get; set; } = string.Empty;

    [JsonPropertyName("ACTIVIDAD_POI")]
    public string? ActividadPoi { get; set; } = string.Empty;

    [JsonPropertyName("FUENTE_FINANCIAMIENTO")]
    public string? FuenteFinanciamiento { get; set; } = string.Empty;

    [JsonPropertyName("META")]
    public string? Meta { get; set; } = string.Empty;

    [JsonPropertyName("CLASIFICADOR")]
    public string? Clasificador { get; set; } = string.Empty;

    [JsonPropertyName("DENOMINACION_SERVICIO")]
    public string? DenominacionServicio { get; set; } = string.Empty;

    [JsonPropertyName("OBJETIVO_CONTRATACION")]
    public string? ObjetivoContratacion { get; set; } = string.Empty;

    [JsonPropertyName("DESCRIPCION_DE_LA_FINALIDAD_PUBLICA")]
    public string? DescripcionFinalidadPublica { get; set; } = string.Empty;

    [JsonPropertyName("ACTIVIDADES_A_DESARROLLAR")]
    public string? ActividadesDesarrollar { get; set; } = string.Empty;

    [JsonPropertyName("DIAS_PLAZO")]
    public string? DiasPlazo { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}

/// <summary>Fila del objeto del servicio en el TDR.</summary>
public sealed class ObjetoServicioPayload
{
    [JsonPropertyName("cantidad")]
    public string? Cantidad { get; set; } = "1";

    [JsonPropertyName("unidad")]
    public string? Unidad { get; set; } = string.Empty;

    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}

/// <summary>Entregable persistido en modo único o múltiple.</summary>
public sealed class EntregablePayload
{
    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("plazo")]
    public string? Plazo { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}

/// <summary>Condición y porcentaje asociado a un entregable.</summary>
public sealed class PagoPayload
{
    [JsonPropertyName("condicion")]
    public string? Condicion { get; set; } = string.Empty;

    /// <summary>
    /// Es nullable para distinguir un porcentaje ausente de un cero explícito.
    /// </summary>
    [JsonPropertyName("porcentaje")]
    public int? Porcentaje { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}
