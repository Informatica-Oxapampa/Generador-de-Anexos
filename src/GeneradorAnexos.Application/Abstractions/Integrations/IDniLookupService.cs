namespace GeneradorAnexos.Application.Abstractions.Integrations;

public sealed record DniLookupResult(
    string Dni,
    string FullName,
    string Ruc);

/// <summary>Categoria segura de un fallo al consultar un DNI.</summary>
public enum DniLookupFailure
{
    Unknown,
    NotConfigured,
    Authentication,
    NotFound,
    Network,
    ProviderUnavailable,
    InvalidResponse,
}

/// <summary>
/// Error de consulta sin incluir el DNI, el token ni la respuesta completa del
/// proveedor, para que pueda mostrarse y registrarse sin filtrar datos.
/// </summary>
public sealed class DniLookupException : Exception
{
    public DniLookupException(DniLookupFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    public DniLookupException(DniLookupFailure failure, string message, Exception innerException)
        : base(message, innerException)
    {
        Failure = failure;
    }

    public DniLookupFailure Failure { get; }
}

public interface IDniLookupService
{
    bool IsEnabled { get; }

    Task<DniLookupResult> LookupAsync(
        string dni,
        CancellationToken cancellationToken = default);
}
