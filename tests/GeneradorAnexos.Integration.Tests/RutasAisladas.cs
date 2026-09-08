// Solo sustituye las rutas en este ejecutable de pruebas. El repositorio,
// los respaldos, los documentos y DPAPI compilan sus archivos de producción.
namespace GeneradorAnexos.Infrastructure.Windows.Persistence;
public static class RutasDatos
{
    public static string Root { get; set; } = Path.Combine(Path.GetTempPath(), "ga-integration-" + Guid.NewGuid().ToString("N"));
    public static string RutaBaseActiva() => Path.Combine(Root, "datos", "registros.db");
    public static string RutaBaseHeredada() => Path.Combine(Root, "registros.db");
    public static string DirectorioRespaldos() => Path.Combine(Root, "respaldos");
    public static string DirectorioEspejo() => Path.Combine(Root, "espejo");
}
