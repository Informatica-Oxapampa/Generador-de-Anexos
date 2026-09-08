namespace GeneradorAnexos.Application.Abstractions.Drafts;

/// <summary>Persists the complete versioned draft payload.</summary>
public interface IDraftStore
{
    bool Exists();

    Task SaveAsync(string json, CancellationToken cancellationToken = default);

    Task<DraftReadResult?> LoadAsync(
        LegacyDraftReadPolicy legacyPolicy,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(CancellationToken cancellationToken = default);
}
