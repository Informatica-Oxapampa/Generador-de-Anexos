using GeneradorAnexos.Domain.Models;

namespace GeneradorAnexos.Application.Abstractions.Documents;

public interface IAnnexDocumentGenerator
{
    Task GenerateAsync(
        BorradorPayloadV1 session,
        string destinationPath,
        CancellationToken cancellationToken = default);
}

public interface ITdrDocumentGenerator
{
    Task GenerateAsync(
        BorradorPayloadV1 session,
        string destinationPath,
        CancellationToken cancellationToken = default);
}
