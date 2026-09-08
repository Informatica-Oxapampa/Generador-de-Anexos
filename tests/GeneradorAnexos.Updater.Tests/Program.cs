using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using GeneradorAnexos.WinUI.Services.Actualizaciones;

var checks = 0;
void Check(string name, bool ok) { if (!ok) throw new Exception(name); checks++; Console.WriteLine("OK " + name); }
var bytes = new byte[] { 1, 2, 3, 4 };
ManifiestoActualizacion Manifest(string version) => new()
{
    Formato = ConfiguracionActualizaciones.FormatoManifiesto, Publicado = DateTimeOffset.UtcNow.ToString("O"), Release = "v" + version,
    App = new() { Version = version, Fecha = DateTime.UtcNow.ToString("yyyy-MM-dd"), Tamano = bytes.Length,
        Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
        Url = ConfiguracionActualizaciones.UrlRepositorio + $"/releases/download/v{version}/GeneradorAnexos-{version}-Setup.exe" }
};
async Task<ResultadoComprobacion> Query(ManifiestoActualizacion m)
{
    PreferenciasUi.Maxima = "";
    using var client = new HttpClient(new Responder(_ => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(m)) }));
    return await ServicioActualizaciones.ComprobarAsync(client, default);
}
var nuevo = await Query(Manifest("1.0.1"));
Check("consulta sin certificado detecta nueva versión", nuevo.AppPendiente(out _)?.Version == "1.0.1");
Check("misma versión no reinstala", (await Query(Manifest("1.0.0"))).AppPendiente(out _) is null);
Check("versión anterior no instala", (await Query(Manifest("0.9.0"))).AppPendiente(out _) is null);
var soloPlantillas = Manifest("1.0.0");
soloPlantillas.Release = "plantillas-1.0.1";
soloPlantillas.Plantillas = new()
{
    Version = "1.0.1", Fecha = soloPlantillas.App!.Fecha, Tamano = bytes.Length,
    Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
    Url = ConfiguracionActualizaciones.UrlRepositorio + "/releases/download/plantillas-1.0.1/plantillas-1.0.1.zip"
};
var actualizacionPlantillas = await Query(soloPlantillas);
Check("Release de plantillas aceptada sin cambiar programa", actualizacionPlantillas.Estado == EstadoComprobacion.Correcta && actualizacionPlantillas.AppPendiente(out _) is null);
Check("plantillas 1.0.1 detectadas desde 1.0.0", actualizacionPlantillas.PlantillasPendientes(new(1,0,0), out _)?.Version == "1.0.1");
Check("plantillas ya instaladas no se repiten", actualizacionPlantillas.PlantillasPendientes(new(1,0,1), out _) is null);
var legado = Manifest("1.0.1"); legado.Formato = 1;
Check("publicación antigua incompatible rechazada", (await Query(legado)).Estado == EstadoComprobacion.Fallida);
var otro = Manifest("1.0.1"); otro.App!.Url = "https://github.com/otro/repositorio/releases/download/v1.0.1/setup.exe";
Check("otro repositorio rechazado", (await Query(otro)).Estado == EstadoComprobacion.Fallida);
foreach (var suffix in new[] { "../../../../../otro/repo/setup.exe", "%2e%2e/evil.exe", "v1.0.1/setup.exe?otra=ruta" })
{
    otro.App.Url = ConfiguracionActualizaciones.UrlRepositorio + "/releases/download/" + suffix;
    Check("ruta ambigua rechazada", (await Query(otro)).Estado == EstadoComprobacion.Fallida);
}
var minima = Manifest("2.0.0"); minima.App!.VersionMinima = "1.5.0";
Check("versión intermedia requerida", (await Query(minima)).Estado == EstadoComprobacion.Fallida);
using (var client = new HttpClient(new Responder(_ => new(HttpStatusCode.NotFound))))
    Check("404 explica publicación incompleta", (await ServicioActualizaciones.ComprobarAsync(client, default)).Mensaje.Contains("update.json"));
using (var client = new HttpClient(new Responder(_ => new(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://example.com/setup.exe") } })))
    Check("redirección externa rechazada", (await ServicioActualizaciones.ComprobarAsync(client, default)).Estado == EstadoComprobacion.Fallida);
using (var client = new HttpClient(new Responder(_ => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(Manifest("1.0.1"))) })))
{
    PreferenciasUi.Maxima = "1.0.2";
    Check("manifiesto anterior rechazado", (await ServicioActualizaciones.ComprobarAsync(client, default)).Estado == EstadoComprobacion.Fallida);
}
Check("estable superior se conserva", !ResultadoComprobacion.DebeInstalar(new(2,0,0), new(1,0,0), false));
Check("prueba superior pasa a estable", ResultadoComprobacion.DebeInstalar(new(2,0,0), new(1,0,0), true));
Check("prueba igual pasa a estable", ResultadoComprobacion.DebeInstalar(new(1,0,0), new(1,0,0), true));
Directory.CreateDirectory(PreferenciasUi.RutaCarpeta);
try
{
    var prefsPath = Path.Combine(PreferenciasUi.RutaCarpeta, "preferencias.json");
    await File.WriteAllTextAsync(prefsPath, "{\"version_mas_alta_vista\":\"1.0.4\",\"version_omitida\":\"1.0.1\",\"tema\":\"oscuro\",\"otro\":42}");
    var prefs = new GeneradorAnexos.WinUI.Services.PreferenciasUi(prefsPath);
    Check("reinicio ignora máximo y omisión de etapa anterior", prefs.VersionMasAltaVista == "" && prefs.VersionOmitida == "");
    Check("reinicio conserva tema", prefs.Tema == "oscuro");
    prefs.VersionMasAltaVista = "1.0.1";
    prefs.VersionOmitida = "1.0.2";
    var reabiertas = new GeneradorAnexos.WinUI.Services.PreferenciasUi(prefsPath);
    Check("historial nuevo persiste al reabrir", reabiertas.VersionMasAltaVista == "1.0.1" && reabiertas.VersionOmitida == "1.0.2");
    using (var guardado = JsonDocument.Parse(await File.ReadAllTextAsync(prefsPath)))
        Check("preferencias ajenas intactas", guardado.RootElement.GetProperty("otro").GetInt32() == 42 && !guardado.RootElement.TryGetProperty("version_mas_alta_vista", out _));
    Directory.CreateDirectory(ServicioActualizaciones.CarpetaDescargas);
    var viejo = Path.Combine(ServicioActualizaciones.CarpetaDescargas, "GeneradorAnexos-0.9.0-Setup.exe");
    var ajeno = Path.Combine(ServicioActualizaciones.CarpetaDescargas, "documento.txt");
    File.WriteAllText(viejo, "viejo"); File.WriteAllText(ajeno, "conservar");
    ServicioActualizaciones.LimpiarDescargasAnteriores("");
    Check("limpieza elimina solo descargas propias", !File.Exists(viejo) && File.ReadAllText(ajeno) == "conservar");
    var path = Path.Combine(PreferenciasUi.RutaCarpeta, "hash.bin");
    var verificar = typeof(ServicioActualizaciones).GetMethod("VerificarAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
    Task<bool> Hash() => (Task<bool>)verificar.Invoke(null, new object[] { path, Manifest("1.0.1").App!, 100L, CancellationToken.None })!;
    await File.WriteAllBytesAsync(path, bytes);
    Check("hash correcto aceptado", await Hash());
    await File.WriteAllBytesAsync(path, new byte[] { 4, 3, 2, 1 });
    Check("alteración mismo tamaño rechazada", !await Hash());
    await File.WriteAllBytesAsync(path, new byte[] { 1 });
    Check("archivo truncado rechazado", !await Hash());
}
finally { Directory.Delete(PreferenciasUi.RutaCarpeta, true); }
Console.WriteLine($"{checks} pruebas de actualizaciones correctas.");
sealed class Responder(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(reply(request));
}
