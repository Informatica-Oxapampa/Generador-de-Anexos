using System.Diagnostics;
using GeneradorAnexos.Application.Abstractions.Security;

namespace GeneradorAnexos.Infrastructure.Windows.Diagnostics;

/// <summary>Writes only known identifiers to the process diagnostic trace.</summary>
public sealed class AllowlistedTraceSecurityEventSink : ISecurityEventSink
{
    public void Write(SecurityEventId eventId)
    {
        if (!IsAllowed(eventId))
        {
            return;
        }

        Trace.WriteLine(string.Concat("[GeneradorAnexos] ", eventId.ToString()));
    }

    private static bool IsAllowed(SecurityEventId eventId) => eventId is
        SecurityEventId.DataProtectionProtectFailed or
        SecurityEventId.DataProtectionUnprotectFailed or
        SecurityEventId.DataProtectionEnvelopeRejected or
        SecurityEventId.DraftSaveFailed or
        SecurityEventId.DraftLoadFailed or
        SecurityEventId.DraftDeleteFailed or
        SecurityEventId.DraftLegacyPlaintextRejected or
        SecurityEventId.DraftTemporaryCleanupFailed;
}
