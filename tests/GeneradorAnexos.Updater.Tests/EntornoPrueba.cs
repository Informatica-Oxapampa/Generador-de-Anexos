// Sustituye solamente el almacenamiento/UI.
// El servicio de consulta, validación, redirecciones y hash es el código de producción.
namespace GeneradorAnexos.WinUI.Services.Actualizaciones;
internal static class Constantes { public const string AppVersion = "1.0.0"; public static bool EsVersionPrueba => false; }
internal sealed class PreferenciasUi
{
    public static string RutaCarpeta { get; } = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public static string Maxima = "";
    public string VersionMasAltaVista { get => Maxima; set => Maxima = value; }
}
internal static class Registro
{
    public static void Info(string _) { }
    public static void Advertencia(string _) { }
    public static void Error(string _, Exception e) { }
}
