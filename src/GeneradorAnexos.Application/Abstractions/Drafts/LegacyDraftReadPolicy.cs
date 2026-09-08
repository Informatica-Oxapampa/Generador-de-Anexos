namespace GeneradorAnexos.Application.Abstractions.Drafts;

/// <summary>Explicit policy for the one-time import of Python plaintext drafts.</summary>
public enum LegacyDraftReadPolicy
{
    RejectPlaintext = 0,
    AllowPlaintextForMigration = 1,
}
