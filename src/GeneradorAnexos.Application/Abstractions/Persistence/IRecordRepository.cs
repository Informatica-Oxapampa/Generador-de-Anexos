using GeneradorAnexos.Domain.Models;

namespace GeneradorAnexos.Application.Abstractions.Persistence;

public sealed record SavedRecordSummary(
    long Id,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record SavedRecord(
    long Id,
    string Name,
    BorradorPayloadV1 Payload,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public interface IRecordRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedRecordSummary>> ListAsync(
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<SavedRecord?> GetAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<long> InsertAsync(
        string name,
        BorradorPayloadV1 payload,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        long id,
        string name,
        BorradorPayloadV1 payload,
        CancellationToken cancellationToken = default);

    Task RenameAsync(
        long id,
        string name,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Comprueba que la base de datos no esté dañada.
    /// </summary>
    /// <returns>
    /// Cadena vacía si todo está correcto; en caso contrario, la descripción
    /// del primer problema encontrado.
    /// </returns>
    Task<string> CheckIntegrityAsync(CancellationToken cancellationToken = default);

    Task<long?> FindByNameAsync(
        string name,
        long? excludedId = null,
        CancellationToken cancellationToken = default);
}
