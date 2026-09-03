namespace GeneradorAnexos.Application.Abstractions.Integrations;

public sealed record OrderData(
    string Number,
    string Reason,
    string RequestingOffice,
    string Meta,
    string Classifier,
    string Amount,
    string Unit,
    string Description);

public interface IOrderPdfReader
{
    Task<OrderData> ReadFirstPageAsync(
        string pdfPath,
        CancellationToken cancellationToken = default);
}
