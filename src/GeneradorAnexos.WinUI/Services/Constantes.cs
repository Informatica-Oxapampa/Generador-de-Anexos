using System.Collections.Generic;
using System.Reflection;

namespace GeneradorAnexos.WinUI.Services;

/// <summary>Equivalente de <c>utils/constantes.py</c>. Valores identicos.</summary>
public static class Constantes
{
    public const string AppNombre = "Generador de Anexos";
    public const string AppSubtitulo = "Municipalidad Provincial de Oxapampa · Contratos Menores";
    /// <summary>
    /// Versión del programa, en formato Semantic Versioning MAYOR.MENOR.PARCHE.
    /// </summary>
    /// <remarks>
    /// Se lee del ensamblado, cuyo número se define en una sola línea del
    /// archivo de proyecto (<c>&lt;Version&gt;</c>). Así la versión que muestra
    /// la aplicación, la que comprueba el sistema de actualizaciones y la que
    /// declara el instalador no pueden quedar descoordinadas.
    /// </remarks>
    public static string AppVersion { get; } = LeerVersion();

    private static string LeerVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;

        return version is null
            ? "0.0.0"
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }
    public const string AppOrganizacion = "Oficina de Tecnologías de la Información (OTI)";
    public const string AppEntidad = "Municipalidad Provincial de Oxapampa";
    public const string AppDescriptor = "Contratación de bienes y servicios menores · Anexos N° 06–09";

    public const string RutaPlantillaAnexos = "plantillas/plantilla_anexos.docx";
    public const string RutaPlantillaTdr = "plantillas/plantilla_tdr.docx";

    public const string MarcadorBanco = "— Seleccione una entidad —";
    public const string UnidadMedidaDefecto = "SERVICIO";

    public const int LongitudDni = 8;
    public const int LongitudRuc = 11;
    public const int LongitudCci = 20;
    public const int LongitudTelefono = 9;

    /// <summary>Texto por defecto de la forma de pago unica.</summary>
    public const string TextoFormaPagoUnico =
        "A la conformidad otorgada al Único Entregable.";

    /// <summary>
    /// Respaldo de entidades bancarias. El catálogo vigente vive en
    /// <c>catalogos.json</c> y lo sirve <see cref="ServicioCatalogos"/>; esta
    /// lista solo se usa si ese archivo falta o está dañado.
    /// </summary>
    public static IReadOnlyList<string> EntidadesBancarias { get; } = new[]
    {
        "Banco de Crédito del Perú (BCP)",
        "BBVA Perú",
        "Interbank",
        "Scotiabank Perú",
        "Banco de la Nación",
        "BanBif",
        "Banco Pichincha Perú",
        "Mibanco",
        "Banco Falabella",
        "Banco Ripley",
        "Banco Santander Perú",
        "Banco GNB Perú",
        "ICBC Perú",
        "Caja Arequipa",
        "Caja Huancayo",
        "Caja Piura",
        "Caja Cusco",
        "Caja Trujillo",
        "Caja Tacna",
        "Caja Ica",
        "Caja Maynas",
        "Financiera Crediscotia",
        "Financiera Compartamos",
        "Financiera Oh!",
    };

    public static IReadOnlyList<string> RequisitosBase { get; } = new[]
    {
        "Inscripción vigente en el RNP.",
        "Persona natural y/o jurídica que se encuentre activo y habido en el registro de la SUNAT.",
        "Suscribir las declaraciones juradas de los anexos N° 6, 7, 8 y 9.",
        "No estar impedido para contratar con el Estado.",
    };

    /// <summary>Unidades organicas de la MPO, para el autocompletado del area usuaria.</summary>
    /// <summary>
    /// Respaldo de áreas usuarias. El catálogo vigente vive en
    /// <c>catalogos.json</c> y lo sirve <see cref="ServicioCatalogos"/>; esta
    /// lista solo se usa si ese archivo falta o está dañado.
    /// </summary>
    public static IReadOnlyList<string> AreasMunicipales { get; } = new[]
    {
        "ÓRGANO DE CONTROL INSTITUCIONAL",
        "PROCURADURÍA PÚBLICA MUNICIPAL",
        "CONCEJO MUNICIPAL",
        "CONSEJO DE COORDINACIÓN LOCAL PROVINCIAL - CCLP",
        "JUNTA DE DELEGADOS VECINALES COMUNALES",
        "ALCALDÍA",
        "GERENCIA MUNICIPAL",
        "OFICINA GENERAL DE ASESORÍA JURÍDICA",
        "OFICINA GENERAL DE PLANEAMIENTO, PRESUPUESTO E INVERSIONES",
        "OFICINA DE PLANEAMIENTO, PRESUPUESTO Y COOPERACIÓN TÉCNICA",
        "OFICINA DE MODERNIZACIÓN",
        "OFICINA DE PROGRAMACIÓN MULTIANUAL DE INVERSIONES",
        "OFICINA DE ESTUDIOS DE INVERSIÓN",
        "OFICINA GENERAL DE ATENCIÓN AL CIUDADANO Y GESTIÓN DOCUMENTARIA",
        "OFICINA DE TRÁMITE DOCUMENTARIO",
        "OFICINA DE ARCHIVO CENTRAL",
        "OFICINA DE RELACIONES PÚBLICAS E IMAGEN INSTITUCIONAL",
        "OFICINA GENERAL DE ADMINISTRACIÓN",
        "OFICINA DE ABASTECIMIENTO",
        "OFICINA DE CONTABILIDAD",
        "OFICINA DE TESORERÍA",
        "OFICINA DE GESTIÓN DE RECURSOS HUMANOS",
        "OFICINA DE TECNOLOGÍA DE LA INFORMACIÓN",
        "GERENCIA DE SERVICIOS PÚBLICOS",
        "SUBGERENCIA DE TRANSPORTE Y CIRCULACIÓN VIAL",
        "SUBGERENCIA DE SEGURIDAD CIUDADANA",
        "GERENCIA DE INFRAESTRUCTURA, DESARROLLO URBANO Y RURAL",
        "SUBGERENCIA DE PLANIFICACIÓN URBANA, RURAL Y CATASTRO",
        "SUBGERENCIA DE SUPERVISIÓN, LIQUIDACIÓN Y TRANSFERENCIA",
        "SUBGERENCIA DE INFRAESTRUCTURA",
        "SUBGERENCIA DE GESTIÓN DE RIESGO DE DESASTRES",
        "SUBGERENCIA DE SANEAMIENTO BÁSICO URBANO Y RURAL",
        "GERENCIA DE DESARROLLO E INCLUSIÓN SOCIAL",
        "SUBGERENCIA DE EDUCACIÓN, CULTURA, DEPORTE Y RECREACIÓN",
        "SUBGERENCIA DE PROGRAMAS SOCIALES",
        "SUBGERENCIA DE REGISTRO DEL ESTADO CIVIL",
        "SUBGERENCIA DE DEFENSORÍA MUNICIPAL DEL NIÑO Y ADOLESCENTE",
        "SUBGERENCIA DE ATENCIÓN A LAS PERSONAS CON DISCAPACIDAD",
        "SUBGERENCIA DE PARTICIPACIÓN VECINAL Y APOYOS SOCIALES",
        "GERENCIA DE ADMINISTRACIÓN TRIBUTARIA",
        "SUBGERENCIA DE RECAUDACIÓN",
        "SUBGERENCIA DE FISCALIZACIÓN TRIBUTARIA",
        "SUBGERENCIA DE COMERCIALIZACIÓN",
        "SUBGERENCIA DE EJECUCIÓN COACTIVA",
        "SUBGERENCIA DE POLICÍA MUNICIPAL",
        "GERENCIA DE DESARROLLO ECONÓMICO",
        "SUBGERENCIA DE DESARROLLO AGROPECUARIO E INDUSTRIAL",
        "SUBGERENCIA DE TURISMO",
        "GERENCIA DE RESERVA BIOSFERA",
        "SUBGERENCIA DE RESERVA BIOSFERA, OXAPAMPA, ASHÁNINKA Y YÁNESHA",
        "SUBGERENCIA DE GESTIÓN AMBIENTAL",
        "GERENCIA DE DESARROLLO DE PUEBLOS ORIGINARIOS",
        "SUBGERENCIA DE INVESTIGACIÓN, PROTECCIÓN Y PROMOCIÓN DEL DESARROLLO INDÍGENA",
        "GERENCIA DE GESTIÓN DE RESIDUOS SÓLIDOS",
        "SUBGERENCIA DE GESTIÓN DE RESIDUOS SÓLIDOS",
    };
}
