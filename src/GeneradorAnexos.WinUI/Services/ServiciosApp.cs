using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneradorAnexos.Application.Abstractions.Documents;
using GeneradorAnexos.Application.Abstractions.Drafts;
using GeneradorAnexos.Application.Abstractions.Integrations;
using GeneradorAnexos.Application.Abstractions.Persistence;
using GeneradorAnexos.Domain.Models;
using GeneradorAnexos.Domain.Payments;
using GeneradorAnexos.Domain.Serialization;
using GeneradorAnexos.Infrastructure.Windows.Diagnostics;
using GeneradorAnexos.Infrastructure.Windows.Documents;
using GeneradorAnexos.Infrastructure.Windows.Drafts;
using GeneradorAnexos.Infrastructure.Windows.Integrations;
using GeneradorAnexos.Infrastructure.Windows.Persistence;
using GeneradorAnexos.Infrastructure.Windows.Security;

namespace GeneradorAnexos.WinUI.Services;

/// <summary>
/// Raíz de composición de la aplicación.
/// </summary>
/// <remarks>
/// El original resolvía las dependencias por import directo de módulos. Aquí se
/// construyen una sola vez y la interfaz consume fachadas estrechas, de modo que
/// la capa de presentación no dependa de la infraestructura de Windows.
/// </remarks>
public static class ServiciosApp
{
    private static readonly Lazy<DpapiDataProtectionService> Proteccion = new(
        () => new DpapiDataProtectionService(NullSecurityEventSink.Instance));

    private static readonly Lazy<IBackupService> Almacen = new(
        () => new BackupService());

    private static readonly Lazy<IRecordRepository> Repositorio = new(
        () => new SqliteRecordRepository(Proteccion.Value, Almacen.Value));

    private static readonly Lazy<IDraftStore> AlmacenBorradores = new(
        () => new EncryptedDraftStore(
            WindowsDraftPathProvider.CreateForCurrentUser(),
            Proteccion.Value,
            NullSecurityEventSink.Instance));


    /// <summary>Registros guardados (SQLite cifrado en reposo).</summary>
    public static FachadaRegistros Registros { get; } = new(Repositorio);

    /// <summary>Respaldos rotativos de la base.</summary>
    public static IBackupService Respaldos => Almacen.Value;

    /// <summary>Generación de documentos Word.</summary>
    public static FachadaDocumentos Documentos { get; } = new();

    /// <summary>Lectura del Pedido de Servicio (PDF del SIGA).</summary>
    public static IOrderPdfReader LectorPedido { get; } = new OrderPdfReader();

    /// <summary>
    /// Consulta del nombre por DNI. No envía datos hasta que el usuario o el
    /// despliegue institucional configure un token del proveedor.
    /// </summary>
    /// <summary>
    /// Consulta de DNI. Permanece desactivada hasta integrar el servicio
    /// oficial de RENIEC: el botón «Validar» deriva el RUC localmente y avisa
    /// de que la consulta del nombre está en desarrollo.
    /// </summary>
    public static IDniLookupService ConsultaDni { get; } = new DisabledDniLookupService();


    /// <summary>Autoguardado cifrado de la sesión.</summary>
    public static FachadaBorradores Borradores { get; } = new(AlmacenBorradores);
}

/// <summary>Fachada de registros con la forma que consume la interfaz.</summary>
public sealed class FachadaRegistros
{
    private readonly Lazy<IRecordRepository> _repositorio;

    internal FachadaRegistros(Lazy<IRecordRepository> repositorio) => _repositorio = repositorio;

    public Task InitializeAsync(CancellationToken ct) => _repositorio.Value.InitializeAsync(ct);

    public Task<IReadOnlyList<SavedRecordSummary>> ListAsync(CancellationToken ct)
        => _repositorio.Value.ListAsync(null, ct);

    public async Task<BorradorPayloadV1?> GetAsync(long id, CancellationToken ct)
        => (await _repositorio.Value.GetAsync(id, ct))?.Payload;

    public Task<long> CreateAsync(string nombre, BorradorPayloadV1 payload, CancellationToken ct)
        => _repositorio.Value.InsertAsync(nombre, payload, ct);

    public Task UpdateAsync(long id, string nombre, BorradorPayloadV1 payload, CancellationToken ct)
        => _repositorio.Value.UpdateAsync(id, nombre, payload, ct);

    public Task RenameAsync(long id, string nombre, CancellationToken ct)
        => _repositorio.Value.RenameAsync(id, nombre, ct);

    public Task DeleteAsync(long id, CancellationToken ct)
        => _repositorio.Value.DeleteAsync(id, ct);

    /// <summary>Comprueba que la base de registros no esté dañada.</summary>
    public Task<string> ComprobarIntegridadAsync(CancellationToken ct)
        => _repositorio.Value.CheckIntegrityAsync(ct);

    public Task<long?> FindIdByNameAsync(string nombre, long? excluir, CancellationToken ct)
        => _repositorio.Value.FindByNameAsync(nombre, excluir, ct);
}

/// <summary>
/// Fachada de documentos: adapta el contexto plano que arma la interfaz al
/// generador OpenXML.
/// </summary>
public sealed class FachadaDocumentos
{
    private readonly AnnexDocumentGenerator _anexos = new();
    private readonly TdrDocumentGenerator _tdr = new();

    public Task GenerateAnnexesAsync(
        IReadOnlyDictionary<string, string> contexto,
        string destino,
        PlanPagos plan,
        CancellationToken ct)
        => _anexos.GenerateAsync(contexto, destino, plan, ct);

    public Task GenerateTdrAsync(
        IReadOnlyDictionary<string, string> contexto,
        string destino,
        TdrPayload tdr,
        PlanPagos plan,
        CancellationToken ct)
        => _tdr.GenerateAsync(contexto, destino, tdr, plan, ct);
}

/// <summary>Fachada del autoguardado cifrado.</summary>
public sealed class FachadaBorradores
{
    private readonly Lazy<IDraftStore> _almacen;

    internal FachadaBorradores(Lazy<IDraftStore> almacen) => _almacen = almacen;

    public Task<bool> AutosaveExistsAsync(CancellationToken ct)
        => Task.FromResult(_almacen.Value.Exists());

    public Task SaveAutosaveAsync(BorradorPayloadV1 payload, CancellationToken ct)
        => _almacen.Value.SaveAsync(PayloadJson.Serialize(payload), ct);

    public async Task<ResultadoBorrador?> ReadAutosaveAsync(CancellationToken ct)
    {
        var resultado = await _almacen.Value.LoadAsync(LegacyDraftReadPolicy.AllowPlaintextForMigration, ct);
        if (resultado is null)
        {
            return null;
        }

        try
        {
            return new ResultadoBorrador(PayloadJson.Deserialize(resultado.Json));
        }
        catch (JsonException excepcion)
        {
            Registro.Error("AUTOSAVE_PARSE_FAILED", excepcion);
            return null;
        }
    }

    public Task DeleteAutosaveAsync(CancellationToken ct) => _almacen.Value.DeleteAsync(ct);
}

/// <summary>Borrador recuperado del autoguardado.</summary>
public sealed record ResultadoBorrador(BorradorPayloadV1? Payload);
