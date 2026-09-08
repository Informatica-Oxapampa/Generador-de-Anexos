namespace GeneradorAnexos.Application.Abstractions.Drafts;

/// <summary>A draft error whose message contains no draft content or path.</summary>
public sealed class DraftStoreException : Exception
{
    public DraftStoreException()
        : this(DraftStoreFailure.Unknown)
    {
    }

    public DraftStoreException(string? message)
        : base(message)
    {
        Failure = DraftStoreFailure.Unknown;
    }

    public DraftStoreException(string? message, Exception? innerException)
        : base(message, innerException)
    {
        Failure = DraftStoreFailure.Unknown;
    }

    public DraftStoreException(DraftStoreFailure failure)
        : base(MessageFor(failure))
    {
        Failure = failure;
    }

    public DraftStoreFailure Failure { get; }

    private static string MessageFor(DraftStoreFailure failure) => failure switch
    {
        DraftStoreFailure.InvalidJson => "El borrador no contiene un objeto JSON válido.",
        DraftStoreFailure.LegacyPlaintextRejected =>
            "El borrador sin cifrar requiere una migración explícita.",
        DraftStoreFailure.ProtectionFailed => "No se pudo proteger el borrador.",
        DraftStoreFailure.SaveFailed => "No se pudo guardar el borrador.",
        DraftStoreFailure.LoadFailed => "No se pudo abrir el borrador.",
        DraftStoreFailure.DeleteFailed => "No se pudo eliminar el borrador.",
        _ => "Ocurrió un error con el borrador.",
    };
}
