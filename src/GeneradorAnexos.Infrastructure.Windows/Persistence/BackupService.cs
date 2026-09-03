using GeneradorAnexos.Application.Abstractions.Persistence;

namespace GeneradorAnexos.Infrastructure.Windows.Persistence;

/// <summary>
/// Equivalente de <c>core/almacen.py: respaldar / info_respaldos</c>.
/// </summary>
/// <remarks>
/// Copia consistente de la base tras cada cambio, con rotación de los archivos
/// más antiguos y un espejo. Es best-effort: un fallo del respaldo nunca
/// interrumpe la operación del usuario, igual que en el original.
/// </remarks>
public sealed class BackupService : IBackupService
{
    private const int MaximoRespaldos = 10;

    private bool _ultimaOperacionOk = true;

    public Task<bool> CreateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var origen = RutasDatos.RutaBaseActiva();
            if (!File.Exists(origen))
            {
                return Task.FromResult(false);
            }

            var carpeta = RutasDatos.DirectorioRespaldos();
            Directory.CreateDirectory(carpeta);

            var destino = Path.Combine(
                carpeta, $"registros-{DateTime.Now:yyyyMMdd-HHmmss}.db");

            CopiaConsistente(origen, destino);
            Rotar(carpeta, MaximoRespaldos);

            // Espejo: siempre la copia más reciente, con nombre estable.
            var espejo = RutasDatos.DirectorioEspejo();
            Directory.CreateDirectory(espejo);
            CopiaConsistente(origen, Path.Combine(espejo, "registros.db"));

            _ultimaOperacionOk = true;
            return Task.FromResult(true);
        }
        catch (IOException)
        {
            _ultimaOperacionOk = false;
            return Task.FromResult(false);
        }
        catch (UnauthorizedAccessException)
        {
            _ultimaOperacionOk = false;
            return Task.FromResult(false);
        }
    }

    public Task<BackupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var carpeta = RutasDatos.DirectorioRespaldos();
        var archivos = Directory.Exists(carpeta)
            ? Directory.GetFiles(carpeta, "registros-*.db")
            : Array.Empty<string>();

        DateTime? ultimo = archivos.Length == 0
            ? null
            : archivos.Max(File.GetLastWriteTime);

        return Task.FromResult(new BackupStatus(
            archivos.Length,
            ultimo,
            carpeta,
            RutasDatos.DirectorioEspejo(),
            _ultimaOperacionOk));
    }

    /// <summary>Copia incluyendo los archivos WAL y SHM si existen.</summary>
    private static void CopiaConsistente(string origen, string destino)
    {
        File.Copy(origen, destino, overwrite: true);

        foreach (var sufijo in new[] { "-wal", "-shm" })
        {
            var lateral = origen + sufijo;
            if (File.Exists(lateral))
            {
                File.Copy(lateral, destino + sufijo, overwrite: true);
            }
        }
    }

    /// <summary>Conserva solo los respaldos más recientes.</summary>
    private static void Rotar(string carpeta, int maximo)
    {
        var archivos = Directory.GetFiles(carpeta, "registros-*.db")
            .OrderByDescending(File.GetLastWriteTime)
            .Skip(maximo)
            .ToList();

        foreach (var archivo in archivos)
        {
            try
            {
                File.Delete(archivo);
            }
            catch (IOException)
            {
                // La rotación es best-effort.
            }
        }
    }
}
