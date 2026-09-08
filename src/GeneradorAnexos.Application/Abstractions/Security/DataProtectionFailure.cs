namespace GeneradorAnexos.Application.Abstractions.Security;

/// <summary>Privacy-safe categories for data-protection failures.</summary>
public enum DataProtectionFailure
{
    Unknown = 0,
    InvalidEnvelope = 1,
    ProtectionFailed = 2,
    UnprotectionFailed = 3,
    UnsupportedPlatform = 4,
}
