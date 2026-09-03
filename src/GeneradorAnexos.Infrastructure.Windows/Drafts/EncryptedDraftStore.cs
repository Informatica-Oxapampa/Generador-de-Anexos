using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GeneradorAnexos.Application.Abstractions.Drafts;
using GeneradorAnexos.Application.Abstractions.Security;
using GeneradorAnexos.Infrastructure.Windows.Diagnostics;

namespace GeneradorAnexos.Infrastructure.Windows.Drafts;

/// <summary>
/// Stores the complete draft as a DPAPI envelope. Plaintext is constructed in
/// memory only and never written to the filesystem by this class.
/// </summary>
public sealed class EncryptedDraftStore : IDraftStore
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly string _autosavePath;
    private readonly string _autosaveDirectory;
    private readonly IDataProtectionService _dataProtection;
    private readonly ISecurityEventSink _events;

    public EncryptedDraftStore(
        IDraftPathProvider pathProvider,
        IDataProtectionService dataProtection,
        ISecurityEventSink? events = null)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(dataProtection);

        var suppliedPath = pathProvider.AutosavePath;
        ArgumentException.ThrowIfNullOrWhiteSpace(suppliedPath);
        if (!Path.IsPathFullyQualified(suppliedPath))
        {
            throw new ArgumentException(
                "The autosave path must be fully qualified.",
                nameof(pathProvider));
        }

        _autosavePath = Path.GetFullPath(suppliedPath);
        _autosaveDirectory = Path.GetDirectoryName(_autosavePath)
            ?? throw new ArgumentException(
                "The autosave path must have a parent directory.",
                nameof(pathProvider));
        _dataProtection = dataProtection;
        _events = events ?? NullSecurityEventSink.Instance;
    }

    public bool Exists() => File.Exists(_autosavePath);

    public async Task SaveAsync(string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        ValidateJsonObject(json);
        cancellationToken.ThrowIfCancellationRequested();

        string protectedEnvelope;
        try
        {
            protectedEnvelope = _dataProtection.Protect(json);
        }
        catch (DataProtectionException)
        {
            _events.Write(SecurityEventId.DraftSaveFailed);
            throw new DraftStoreException(DraftStoreFailure.ProtectionFailed);
        }

        byte[]? bytes = null;
        string? temporaryPath = null;
        try
        {
            Directory.CreateDirectory(_autosaveDirectory);
            temporaryPath = CreateTemporaryPath();
            bytes = StrictUtf8.GetBytes(protectedEnvelope);

            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, _autosavePath, overwrite: true);
            temporaryPath = null;
        }
        catch (IOException)
        {
            _events.Write(SecurityEventId.DraftSaveFailed);
            throw new DraftStoreException(DraftStoreFailure.SaveFailed);
        }
        catch (UnauthorizedAccessException)
        {
            _events.Write(SecurityEventId.DraftSaveFailed);
            throw new DraftStoreException(DraftStoreFailure.SaveFailed);
        }
        catch (SecurityException)
        {
            _events.Write(SecurityEventId.DraftSaveFailed);
            throw new DraftStoreException(DraftStoreFailure.SaveFailed);
        }
        finally
        {
            if (bytes is not null)
            {
                CryptographicOperations.ZeroMemory(bytes);
            }

            if (temporaryPath is not null)
            {
                TryDeleteTemporary(temporaryPath);
            }
        }
    }

    public async Task<DraftReadResult?> LoadAsync(
        LegacyDraftReadPolicy legacyPolicy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string content;
        try
        {
            content = await File.ReadAllTextAsync(
                _autosavePath,
                StrictUtf8,
                cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (DecoderFallbackException)
        {
            _events.Write(SecurityEventId.DraftLoadFailed);
            throw new DraftStoreException(DraftStoreFailure.LoadFailed);
        }
        catch (IOException)
        {
            _events.Write(SecurityEventId.DraftLoadFailed);
            throw new DraftStoreException(DraftStoreFailure.LoadFailed);
        }
        catch (UnauthorizedAccessException)
        {
            _events.Write(SecurityEventId.DraftLoadFailed);
            throw new DraftStoreException(DraftStoreFailure.LoadFailed);
        }
        catch (SecurityException)
        {
            _events.Write(SecurityEventId.DraftLoadFailed);
            throw new DraftStoreException(DraftStoreFailure.LoadFailed);
        }

        string json;
        var wasLegacyPlaintext = false;
        if (_dataProtection.IsProtected(content))
        {
            try
            {
                json = _dataProtection.Unprotect(content);
            }
            catch (DataProtectionException)
            {
                _events.Write(SecurityEventId.DraftLoadFailed);
                throw new DraftStoreException(DraftStoreFailure.ProtectionFailed);
            }
        }
        else if (legacyPolicy == LegacyDraftReadPolicy.AllowPlaintextForMigration)
        {
            json = content;
            wasLegacyPlaintext = true;
        }
        else
        {
            _events.Write(SecurityEventId.DraftLegacyPlaintextRejected);
            throw new DraftStoreException(DraftStoreFailure.LegacyPlaintextRejected);
        }

        ValidateJsonObject(json);
        return new DraftReadResult(json, wasLegacyPlaintext);
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            File.Delete(_autosavePath);
            return Task.CompletedTask;
        }
        catch (IOException)
        {
            _events.Write(SecurityEventId.DraftDeleteFailed);
            throw new DraftStoreException(DraftStoreFailure.DeleteFailed);
        }
        catch (UnauthorizedAccessException)
        {
            _events.Write(SecurityEventId.DraftDeleteFailed);
            throw new DraftStoreException(DraftStoreFailure.DeleteFailed);
        }
        catch (SecurityException)
        {
            _events.Write(SecurityEventId.DraftDeleteFailed);
            throw new DraftStoreException(DraftStoreFailure.DeleteFailed);
        }
    }

    private static void ValidateJsonObject(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new DraftStoreException(DraftStoreFailure.InvalidJson);
            }
        }
        catch (JsonException)
        {
            throw new DraftStoreException(DraftStoreFailure.InvalidJson);
        }
    }

    private string CreateTemporaryPath()
    {
        var fileName = string.Concat(
            ".autoguardado-",
            Guid.NewGuid().ToString("N"),
            ".tmp");
        return Path.Combine(_autosaveDirectory, fileName);
    }

    private void TryDeleteTemporary(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            _events.Write(SecurityEventId.DraftTemporaryCleanupFailed);
        }
        catch (UnauthorizedAccessException)
        {
            _events.Write(SecurityEventId.DraftTemporaryCleanupFailed);
        }
        catch (SecurityException)
        {
            _events.Write(SecurityEventId.DraftTemporaryCleanupFailed);
        }
    }
}
