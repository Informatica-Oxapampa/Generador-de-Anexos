namespace GeneradorAnexos.Application.Abstractions.Security;

/// <summary>
/// Receives only allowlisted event identifiers. Implementations must not throw
/// and must not enrich events with ambient paths, exceptions, or user data.
/// </summary>
public interface ISecurityEventSink
{
    void Write(SecurityEventId eventId);
}
