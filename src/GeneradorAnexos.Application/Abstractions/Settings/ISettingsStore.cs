namespace GeneradorAnexos.Application.Abstractions.Settings;

public interface ISettingsStore
{
    bool LargeText { get; set; }

    string LastDocumentDirectory { get; set; }

    string LastOrderDirectory { get; set; }

    IReadOnlyList<string> RecentOffices { get; set; }

    Task SaveAsync(CancellationToken cancellationToken = default);
}
