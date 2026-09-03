namespace GeneradorAnexos.Application.Abstractions.Security;

/// <summary>
/// Closed diagnostic vocabulary. Values intentionally carry no names, paths,
/// identifiers, exception messages, request data, or other personal data.
/// </summary>
public enum SecurityEventId
{
    DataProtectionProtectFailed = 1001,
    DataProtectionUnprotectFailed = 1002,
    DataProtectionEnvelopeRejected = 1003,
    DraftSaveFailed = 1101,
    DraftLoadFailed = 1102,
    DraftDeleteFailed = 1103,
    DraftLegacyPlaintextRejected = 1104,
    DraftTemporaryCleanupFailed = 1105,
}
