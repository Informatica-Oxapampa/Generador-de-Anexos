namespace GeneradorAnexos.Application.Abstractions.Security;

/// <summary>
/// Protects application text at rest. Implementations must never return
/// plaintext when protection or unprotection fails.
/// </summary>
public interface IDataProtectionService
{
    /// <summary>Returns whether <paramref name="value"/> declares a protected format.</summary>
    bool IsProtected(string? value);

    /// <summary>Protects UTF-8 text and returns its versioned storage envelope.</summary>
    /// <exception cref="DataProtectionException">Protection failed.</exception>
    string Protect(string plaintext);

    /// <summary>
    /// Unprotects a versioned storage envelope. Plaintext input is rejected;
    /// legacy handling belongs to the migration boundary, not to this service.
    /// </summary>
    /// <exception cref="DataProtectionException">
    /// The envelope is invalid or cannot be unprotected for the current user.
    /// </exception>
    string Unprotect(string protectedValue);
}
