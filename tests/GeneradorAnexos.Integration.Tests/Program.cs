using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using GeneradorAnexos.Application.Abstractions.Security;
using GeneradorAnexos.Application.Abstractions.Drafts;
using GeneradorAnexos.Domain.Models;
using GeneradorAnexos.Domain.Payments;
using GeneradorAnexos.Domain.Serialization;
using GeneradorAnexos.Infrastructure.Windows.Documents;
using GeneradorAnexos.Infrastructure.Windows.Drafts;
using GeneradorAnexos.Infrastructure.Windows.Persistence;
using GeneradorAnexos.Infrastructure.Windows.Security;
using GeneradorAnexos.WinUI.Services.Actualizaciones;
using Microsoft.Data.Sqlite;

var checks = 0;
void Check(string name, bool result)
{
    if (!result) throw new InvalidOperationException(name);
    checks++; Console.WriteLine("OK " + name);
}
async Task Throws<T>(string name, Func<Task> action) where T : Exception
{
    try { await action(); } catch (T) { Check(name, true); return; }
    throw new InvalidOperationException("No se rechazó: " + name);
}
async Task Sql(string path, string sql)
{
    using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
    await db.OpenAsync(); using var cmd = db.CreateCommand(); cmd.CommandText = sql; await cmd.ExecuteNonQueryAsync();
}
IDataProtectionService protection = OperatingSystem.IsWindows()
    ? new DpapiDataProtectionService() : new TestProtection();
Console.WriteLine(OperatingSystem.IsWindows() ? "Cifrado: DPAPI real" : "Cifrado: doble AES-GCM; DPAPI requiere Windows");
var root = RutasDatos.Root;
Directory.CreateDirectory(root);
try
{
    PruebasFormato.Ejecutar(Check);
    await PruebasCuadroPagos.EjecutarAsync(root);
    foreach (var invalid in new[] { "1.0.xyz", "1.0.2147483648", "1.0.2.3.4", "1.0.2-preview", "1.-1.0", "1.0.", "1.0.2 suffix" })
        Check("versión inválida " + invalid, !VersionSemantica.TryParse(invalid, out _));
    Check("ensamblado compatible", VersionSemantica.TryParse("v1.0.2.0", out var parsed) && parsed == new VersionSemantica(1, 0, 2));
    Check("comparación numérica", new VersionSemantica(1, 10, 0) > new VersionSemantica(1, 9, 0));
    foreach (var url in new[] { "http://github.com/a", "https://github.com.evil.example/a", "https://evil.github.com/a", "https://github.com:444/a", "https://user@github.com/a" })
        Check("URL rechazada " + url, !PaqueteActualizacion.EsDescargaPermitida(url));
    var package = new PaqueteActualizacion { Version = "1.0.2", Fecha = "2026-09-07", Sha256 = new string('a', 64), Tamano = 100,
        Url = ConfiguracionActualizaciones.UrlRepositorio + "/releases/download/v1.0.2/GeneradorAnexos-1.0.2-Setup.exe" };
    Check("paquete válido", package.EsValido(out _));
    package.Notas = null!; Check("JSON con null", !package.EsValido(out _));

    var payload = new BorradorPayloadV1 { Fecha = "2026-09-03", Anexos = new AnexosPayload { DireccionProveedor = "DOMICILIO SINTETICO" } };
    var json = PayloadJson.Serialize(payload);
    var encoded = protection.Protect(json);
    Check("sobre enc1 compatible", encoded.StartsWith("enc1:", StringComparison.Ordinal));
    Check("descifrado", protection.Unprotect(encoded) == json);
    await Throws<DataProtectionException>("sobre alterado", () => Task.Run(() => protection.Unprotect("enc1:invalido")));

    // Reproduce una instalación antigua, con IDs y fechas históricas.
    Directory.CreateDirectory(Path.GetDirectoryName(RutasDatos.RutaBaseActiva())!);
    await Sql(RutasDatos.RutaBaseActiva(), "CREATE TABLE registros(id INTEGER PRIMARY KEY AUTOINCREMENT,nombre TEXT NOT NULL,datos TEXT NOT NULL,creado TEXT NOT NULL,actualizado TEXT NOT NULL);");
    using (var db = new SqliteConnection("Data Source=" + RutasDatos.RutaBaseActiva()))
    {
        await db.OpenAsync(); using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO registros VALUES(42,$n,$d,'2026-09-03T12:00:00Z','2026-09-03T12:00:00Z')";
        cmd.Parameters.AddWithValue("$n", "Registro legado sintético"); cmd.Parameters.AddWithValue("$d", encoded); await cmd.ExecuteNonQueryAsync();
    }
    var backups = new BackupService();
    var repo = new SqliteRecordRepository(protection, backups);
    await repo.InitializeAsync();
    var old = await repo.GetAsync(42);
    Check("migración conserva ID, nombre y fecha", old is not null && old.Name == "Registro legado sintético" && old.CreatedAt.Year == 2026);
    var before = old!.UpdatedAt;
    await repo.GetAsync(42); Check("cargar no cambia fecha", (await repo.GetAsync(42))!.UpdatedAt == before);
    var id = await repo.InsertAsync("Nuevo sintético", payload);
    await repo.UpdateAsync(id, "Editado sintético", payload);
    repo = new SqliteRecordRepository(protection, backups); await repo.InitializeAsync();
    Check("guardar, editar y reabrir", (await repo.GetAsync(id))!.Name == "Editado sintético");
    await repo.RenameAsync(id, "Renombrado sintético");
    Check("buscar nombre", await repo.FindByNameAsync("Renombrado sintético") == id);
    for (var i = 0; i < 12; i++) Check("respaldo íntegro " + i, await backups.CreateAsync());
    Check("rotación a diez", (await backups.GetStatusAsync()).UniqueBackupCount == 10);
    Check("integridad SQLite", await repo.CheckIntegrityAsync() == "");
    await Sql(RutasDatos.RutaBaseActiva(), "UPDATE registros SET nombre='texto inyectado' WHERE id=42;");
    await repo.InitializeAsync();
    await Throws<DataProtectionException>("reinicio no remigra texto plano", async () => { await repo.GetAsync(42); });
    await repo.DeleteAsync(id); Check("eliminar", await repo.GetAsync(id) is null);
    await Sql(RutasDatos.RutaBaseActiva(), "PRAGMA user_version=99;");
    await Throws<InvalidDataException>("esquema futuro intacto", () => repo.InitializeAsync());

    var draft = new EncryptedDraftStore(new DraftPath(Path.Combine(root, "autosave.enc")), protection);
    await draft.SaveAsync(json);
    Check("borrador recuperable", (await draft.LoadAsync(LegacyDraftReadPolicy.RejectPlaintext))?.Json == json);
    using (var cancelled = new CancellationTokenSource())
    {
        cancelled.Cancel(); await Throws<OperationCanceledException>("autoguardado cancelado", () => draft.SaveAsync("{}", cancelled.Token));
    }
    Check("cancelación conserva borrador", (await draft.LoadAsync(LegacyDraftReadPolicy.RejectPlaintext))?.Json == json);

    // Plantillas reales incluidas, con valores sintéticos para todos los marcadores.
    Dictionary<string,string> Context(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        var text = doc.MainDocumentPart!.Document!.InnerText;
        return Regex.Matches(text, @"\{\{\s*([A-Za-z0-9_]+)\s*\}\}").Select(m => m.Groups[1].Value)
            .Distinct().ToDictionary(k => k, _ => "VALOR SINTETICO");
    }
    var plan = ConstructorPlanPagos.Construir(null, "1000");
    var annexPath = Path.Combine(root, "anexo.docx");
    var tdrPath = Path.Combine(root, "tdr.docx");
    await new AnnexDocumentGenerator().GenerateAsync(Context(RutasPlantillas.Anexos()), annexPath, plan);
    await new TdrDocumentGenerator().GenerateAsync(Context(RutasPlantillas.Tdr()), tdrPath, new TdrPayload(), plan);
    foreach (var path in new[] { annexPath, tdrPath })
    {
        using var doc = WordprocessingDocument.Open(path, false);
        Check("Word válido " + Path.GetFileName(path), !doc.MainDocumentPart!.Document!.InnerText.Contains("{{", StringComparison.Ordinal));
    }
    var original = File.ReadAllBytes(annexPath);
    await Throws<DocumentoException>("fallo conserva documento anterior", () => Task.Run(() => DocxTemplateEngine.Renderizar(
        RutasPlantillas.Anexos(), annexPath, new Dictionary<string,string>(), _ => throw new InvalidOperationException("fallo sintético"))));
    Check("destino intacto", original.SequenceEqual(File.ReadAllBytes(annexPath)));
    if (OperatingSystem.IsWindows())
    using (var locked = new FileStream(annexPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        await Throws<DocumentoException>("Word bloqueado", () => new AnnexDocumentGenerator().GenerateAsync(Context(RutasPlantillas.Anexos()), annexPath, plan));
    Check("sin temporales de generación", Directory.GetFiles(root, "*.generando-*").Length == 0);
    Console.WriteLine($"PASS: {checks} comprobaciones de integración");
}
finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }

sealed record DraftPath(string AutosavePath) : IDraftPathProvider;
// No sustituye DPAPI en producción; permite comprobar persistencia en Linux.
sealed class TestProtection : IDataProtectionService
{
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    public bool IsProtected(string? value) => value?.StartsWith("enc1:", StringComparison.Ordinal) == true;
    public string Protect(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12); var bytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[bytes.Length]; var tag = new byte[16]; using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, bytes, cipher, tag); return "enc1:" + Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }
    public string Unprotect(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value[5..]); var result = new byte[bytes.Length - 28];
            using var aes = new AesGcm(key,16); aes.Decrypt(bytes.AsSpan(0,12), bytes.AsSpan(28), bytes.AsSpan(12,16),result);
            return Encoding.UTF8.GetString(result);
        }
        catch { throw new DataProtectionException(DataProtectionFailure.InvalidEnvelope); }
    }
}
