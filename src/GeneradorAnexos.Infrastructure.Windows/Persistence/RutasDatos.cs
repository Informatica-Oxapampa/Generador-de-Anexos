namespace GeneradorAnexos.Infrastructure.Windows.Persistence;

/// <summary>
/// Equivalente de <c>core/almacen.py</c> y <c>utils/rutas_datos.py</c>.
/// </summary>
/// <remarks>
/// Contrato congelado de la migración: la base activa vive en
/// <c>%LOCALAPPDATA%\GeneradorAnexos\datos\registros.db</c> y la base heredada
/// en <c>%LOCALAPPDATA%\GeneradorAnexos\registros.db</c>.
/// </remarks>
public static class RutasDatos
{
    public static string RaizDatos() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GeneradorAnexos");

    public static string DirectorioDatos() => Path.Combine(RaizDatos(), "datos");

    public static string DirectorioRespaldos() => Path.Combine(RaizDatos(), "respaldos");

    public static string DirectorioEspejo() => Path.Combine(RaizDatos(), "espejo");

    public static string RutaBaseActiva() => Path.Combine(DirectorioDatos(), "registros.db");

    public static string RutaBaseHeredada() => Path.Combine(RaizDatos(), "registros.db");
}
