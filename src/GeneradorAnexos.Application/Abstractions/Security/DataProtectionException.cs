namespace GeneradorAnexos.Application.Abstractions.Security;

/// <summary>An error that is safe to present without including protected content.</summary>
public sealed class DataProtectionException : Exception
{
    public DataProtectionException()
        : this(DataProtectionFailure.Unknown)
    {
    }

    public DataProtectionException(string? message)
        : base(message)
    {
        Failure = DataProtectionFailure.Unknown;
    }

    public DataProtectionException(string? message, Exception? innerException)
        : base(message, innerException)
    {
        Failure = DataProtectionFailure.Unknown;
    }

    public DataProtectionException(DataProtectionFailure failure)
        : base(MessageFor(failure))
    {
        Failure = failure;
    }

    /// <summary>Gets a category that contains no protected data.</summary>
    public DataProtectionFailure Failure { get; }

    private static string MessageFor(DataProtectionFailure failure) => failure switch
    {
        DataProtectionFailure.InvalidEnvelope => "El valor protegido tiene un formato inválido.",
        DataProtectionFailure.ProtectionFailed => "No se pudieron proteger los datos.",
        DataProtectionFailure.UnprotectionFailed =>
            "Los datos no se pueden abrir con la cuenta actual de Windows.",
        DataProtectionFailure.UnsupportedPlatform =>
            "La protección de datos requiere Windows.",
        _ => "Ocurrió un error al proteger los datos.",
    };
}
