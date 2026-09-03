namespace GeneradorAnexos.Application.Abstractions.Persistence;

public sealed record BackupStatus(
    int UniqueBackupCount,
    DateTime? LastBackupAt,
    string LocalDirectory,
    string MirrorDirectory,
    bool LastOperationSucceeded);

public interface IBackupService
{
    Task<bool> CreateAsync(CancellationToken cancellationToken = default);

    Task<BackupStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
