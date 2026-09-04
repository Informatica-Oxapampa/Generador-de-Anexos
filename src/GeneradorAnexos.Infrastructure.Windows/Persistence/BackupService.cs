using System.Globalization;
using GeneradorAnexos.Application.Abstractions.Persistence;
using Microsoft.Data.Sqlite;

namespace GeneradorAnexos.Infrastructure.Windows.Persistence;

/// <summary>
/// Crea respaldos transaccionalmente consistentes mediante la API de backup de SQLite.
/// </summary>
public sealed class BackupService : IBackupService
{
    private const int MaximoRespaldos = 10;
    // Servicio único durante toda la ejecución. El semáforo estático evita
    // respaldos simultáneos y no necesita descartarse antes de terminar el proceso.
    private static readonly SemaphoreSlim Semaforo = new(1, 1);
    private volatile bool _ultimaOperacionOk = true;

    public async Task<bool> CreateAsync(CancellationToken cancellationToken = default)
    {
        await Semaforo.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var resultado = await Task.Run(
                () => CrearRespaldo(cancellationToken), cancellationToken).ConfigureAwait(false);
            _ultimaOperacionOk = resultado;
            return resultado;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException or SqliteException)
        {
            _ultimaOperacionOk = false;
            return false;
        }
        finally
        {
            Semaforo.Release();
        }
    }

    public Task<BackupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
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
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            _ultimaOperacionOk = false;
            return Task.FromResult(new BackupStatus(
                0,
                null,
                RutasDatos.DirectorioRespaldos(),
                RutasDatos.DirectorioEspejo(),
                false));
        }
    }

    private static bool CrearRespaldo(CancellationToken cancellationToken)
    {
        var origen = RutasDatos.RutaBaseActiva();
        if (!File.Exists(origen))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var carpeta = RutasDatos.DirectorioRespaldos();
        Directory.CreateDirectory(carpeta);

        var marca = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" +
                    Guid.NewGuid().ToString("N")[..8];
        var destino = Path.Combine(carpeta, $"registros-{marca}.db");
        var temporal = destino + ".tmp-" + Guid.NewGuid().ToString("N");

        try
        {
            using var conexionOrigen = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = origen,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private,
                DefaultTimeout = 5,
            }.ToString());
            using var conexionDestino = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = temporal,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                DefaultTimeout = 5,
            }.ToString());

            conexionOrigen.Open();
            conexionDestino.Open();
            cancellationToken.ThrowIfCancellationRequested();
            conexionOrigen.BackupDatabase(conexionDestino);

            using var integridad = conexionDestino.CreateCommand();
            integridad.CommandText = "PRAGMA integrity_check;";
            var resultado = integridad.ExecuteScalar()?.ToString();
            if (!string.Equals(resultado, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            conexionDestino.Close();
            conexionOrigen.Close();
            File.Move(temporal, destino, overwrite: false);

            var espejo = RutasDatos.DirectorioEspejo();
            Directory.CreateDirectory(espejo);
            var espejoFinal = Path.Combine(espejo, "registros.db");
            var espejoTemporal = espejoFinal + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.Copy(destino, espejoTemporal, overwrite: false);
                File.Move(espejoTemporal, espejoFinal, overwrite: true);
            }
            finally
            {
                IntentarEliminar(espejoTemporal);
            }

            Rotar(carpeta, MaximoRespaldos);
            return true;
        }
        finally
        {
            IntentarEliminar(temporal);
        }
    }

    private static void Rotar(string carpeta, int maximo)
    {
        foreach (var archivo in Directory.GetFiles(carpeta, "registros-*.db")
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Skip(maximo))
        {
            IntentarEliminar(archivo);
        }
    }

    private static void IntentarEliminar(string ruta)
    {
        try
        {
            if (File.Exists(ruta))
            {
                File.Delete(ruta);
            }
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            // La limpieza de temporales y la rotación son best-effort.
        }
    }
}
