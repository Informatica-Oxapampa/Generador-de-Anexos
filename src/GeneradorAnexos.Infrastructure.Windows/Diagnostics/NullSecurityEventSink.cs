using GeneradorAnexos.Application.Abstractions.Security;

namespace GeneradorAnexos.Infrastructure.Windows.Diagnostics;

/// <summary>No-op diagnostics for hosts that have not configured a sink.</summary>
public sealed class NullSecurityEventSink : ISecurityEventSink
{
    public static NullSecurityEventSink Instance { get; } = new();

    private NullSecurityEventSink()
    {
    }

    public void Write(SecurityEventId eventId)
    {
        _ = eventId;
    }
}
