namespace GeneradorAnexos.Application.Abstractions.Drafts;

/// <summary>Privacy-safe categories for draft persistence failures.</summary>
public enum DraftStoreFailure
{
    Unknown = 0,
    InvalidJson = 1,
    LegacyPlaintextRejected = 2,
    ProtectionFailed = 3,
    SaveFailed = 4,
    LoadFailed = 5,
    DeleteFailed = 6,
}
