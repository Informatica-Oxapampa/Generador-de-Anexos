using System.Data;
using GeneradorAnexos.Application.Abstractions.Persistence;
using GeneradorAnexos.Application.Abstractions.Security;
using GeneradorAnexos.Domain.Models;
using GeneradorAnexos.Domain.Serialization;
using Microsoft.Data.Sqlite;

namespace GeneradorAnexos.Infrastructure.Windows.Persistence;

/// <summary>
/// Equivalente de <c>core/base_datos.py</c>.
/// </summary>
/// <remarks>
/// Conserva sin cambios el contrato de datos del aplicativo Python: la tabla
/// <c>registros(id, nombre, datos, creado, actualizado)</c>, el nombre del
/// registro cifrado con DPAPI y el contenido cifrado en reposo. Los registros
/// heredados en texto plano se migran a cifrado en una única transacción.
/// </remarks>
public sealed class SqliteRecordRepository : IRecordRepository
{
    private readonly IDataProtectionService _proteccion;
    private readonly IBackupService _respaldos;
    private readonly string _rutaBase;

    public SqliteRecordRepository(IDataProtectionService proteccion, IBackupService respaldos)
    {
        _proteccion = proteccion;
        _respaldos = respaldos;
        _rutaBase = RutasDatos.RutaBaseActiva();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_rutaBase)!);

        await using var conexion = Abrir();
        await conexion.OpenAsync(cancellationToken);

        await using (var comando = conexion.CreateCommand())
        {
            comando.CommandText = """
                CREATE TABLE IF NOT EXISTS registros (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    nombre      TEXT NOT NULL,
                    datos       TEXT NOT NULL,
                    creado      TEXT NOT NULL,
                    actualizado TEXT NOT NULL
                )
                """;
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        if (await MigrarNombresHeredadosAsync(conexion, cancellationToken))
        {
            await _respaldos.CreateAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<SavedRecordSummary>> ListAsync(
        string? search = null, CancellationToken cancellationToken = default)
    {
        var resultados = new List<SavedRecordSummary>();

        await using var conexion = Abrir();
        await conexion.OpenAsync(cancellationToken);

        await using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id, nombre, creado, actualizado FROM registros ORDER BY actualizado DESC";

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            var nombre = Descifrar(lector.GetString(1));

            // El filtro se aplica en memoria porque el nombre está cifrado en
            // reposo y no puede compararse dentro de la consulta SQL.
            if (!string.IsNullOrWhiteSpace(search) &&
                !nombre.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            resultados.Add(new SavedRecordSummary(
                lector.GetInt64(0),
                nombre,
                DateTime.Parse(lector.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                DateTime.Parse(lector.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind)));
        }

        return resultados;
    }

    public async Task<SavedRecord?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var conexion = Abrir();
        await conexion.OpenAsync(cancellationToken);

        await using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id, nombre, datos, creado, actualizado FROM registros WHERE id = $id";
        comando.Parameters.AddWithValue("$id", id);

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await lector.ReadAsync(cancellationToken))
        {
            return null;
        }

        var payload = PayloadJson.Deserialize(Descifrar(lector.GetString(2)));

        return new SavedRecord(
            lector.GetInt64(0),
            Descifrar(lector.GetString(1)),
            payload,
            DateTime.Parse(lector.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
            DateTime.Parse(lector.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind));
    }

    public async Task<long> InsertAsync(
        string name, BorradorPayloadV1 payload, CancellationToken cancellationToken = default)
    {
        var ahora = DateTime.UtcNow.ToString("O");

        await using var conexion = Abrir();
        await conexion.OpenAsync(cancellationToken);

        await using var comando = conexion.CreateCommand();
        comando.CommandText = """
            INSERT INTO registros (nombre, datos, creado, actualizado)
            VALUES ($nombre, $datos, $creado, $actualizado);
            SELECT last_insert_rowid();
            """;
        comando.Parameters.AddWithValue("$nombre", Cifrar(Normalizar(name)));
        comando.Parameters.AddWithValue("$datos", Cifrar(PayloadJson.Serialize(payload)));
        comando.Parameters.AddWithValue("$creado", ahora);
        comando.Parameters.AddWithValue("$actualizado", ahora);

        var id = (long)(await comando.ExecuteScalarAsync(cancellationToken))!;
        await _respaldos.CreateAsync(cancellationToken);
        return id;
    }

    public async Task UpdateAsync(
        long id, string name, BorradorPayloadV1 payload, CancellationToken cancellationToken = default)
    {
        await using var conexion = Abrir();
        await conexion.OpenAsync(cancellationToken);

        await using var comando = conexion.CreateCommand();
        comando.CommandText = """
            UPDATE registros
            SET nombre = $nombre, datos = $datos, actualizado = $actualizado
            WHERE id = $id
            """;
        comando.Parameters.AddWithValue("$nombre", Cifrar(Normalizar(name)));
        comando.Parameters.AddWithValue("$datos", Cifrar(PayloadJson.Serialize(payload)));
        comando.Parameters.AddWithValue("$actualizado", DateTime.UtcNow.ToString("O"));
        comando.Parameters.AddWithValue("$id", id);

        await comando.ExecuteNonQueryAsync(cancellationToken);
        await _respaldos.CreateAsync(cancellationToken);
    }

    public async Task RenameAsync(long id, string name, CancellationToken cancellationToken = default)
    {
        await using var conexion = Abrir();
        await conexion.OpenAsync(cancellationToken);

        await using var comando = conexion.CreateCommand();
        comando.CommandText =
            "UPDATE registros SET nombre = $nombre, actualizado = $actualizado WHERE id = $id";
        comando.Parameters.AddWithValue("$nombre", Cifrar(Normalizar(name)));
        comando.Parameters.AddWithValue("$actualizado", DateTime.UtcNow.ToString("O"));
        comando.Parameters.AddWithValue("$id", id);

        await comando.ExecuteNonQueryAsync(cancellationToken);
        await _respaldos.CreateAsync(cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var conexion = Abrir();
        await conexion.OpenAsync(cancellationToken);

        await using var comando = conexion.CreateCommand();
        comando.CommandText = "DELETE FROM registros WHERE id = $id";
        comando.Parameters.AddWithValue("$id", id);

        await comando.ExecuteNonQueryAsync(cancellationToken);
        await _respaldos.CreateAsync(cancellationToken);
    }

    /// <summary>
    /// Ejecuta <c>PRAGMA integrity_check</c> sobre la base de registros.
    /// </summary>
    /// <remarks>
    /// SQLite es muy resistente, pero un corte de energía durante una escritura
    /// o un disco con sectores defectuosos pueden dañar el archivo. Esta
    /// comprobación lo detecta antes de que el usuario descubra el problema al
    /// no poder abrir un registro, cuando ya es tarde para recuperarlo desde un
    /// respaldo reciente.
    ///
    /// Devuelve cadena vacía si la base está sana; SQLite responde «ok» en ese
    /// caso.
    /// </remarks>
    public async Task<string> CheckIntegrityAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_rutaBase))
        {
            return string.Empty;
        }

        await using var conexion = Abrir();
        await conexion.OpenAsync(cancellationToken);

        await using var comando = conexion.CreateCommand();
        comando.CommandText = "PRAGMA integrity_check;";

        var resultado = (await comando.ExecuteScalarAsync(cancellationToken))?.ToString();

        return string.Equals(resultado, "ok", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : resultado ?? "La comprobación no devolvió ningún resultado.";
    }

    public async Task<long?> FindByNameAsync(
        string name, long? excludedId = null, CancellationToken cancellationToken = default)
    {
        var buscado = Normalizar(name);

        await using var conexion = Abrir();
        await conexion.OpenAsync(cancellationToken);

        await using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT id, nombre FROM registros";

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            var id = lector.GetInt64(0);
            if (excludedId is not null && id == excludedId)
            {
                continue;
            }

            if (string.Equals(Descifrar(lector.GetString(1)), buscado, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return null;
    }

    // ═══════════════════════ Internos ═══════════════════════

    private SqliteConnection Abrir() => new(new SqliteConnectionStringBuilder
    {
        DataSource = _rutaBase,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Private,
    }.ToString());

    private static string Normalizar(string? nombre)
    {
        var limpio = (nombre ?? string.Empty).Trim();
        return limpio.Length == 0 ? "Registro sin nombre" : limpio;
    }

    private string Cifrar(string texto) => _proteccion.Protect(texto);

    /// <summary>Descifra y tolera valores heredados en texto plano.</summary>
    private string Descifrar(string valor)
    {
        try
        {
            return _proteccion.IsProtected(valor) ? _proteccion.Unprotect(valor) : valor;
        }
        catch (DataProtectionException)
        {
            return valor;
        }
    }

    /// <summary>Cifra los nombres heredados en una única transacción.</summary>
    private async Task<bool> MigrarNombresHeredadosAsync(
        SqliteConnection conexion, CancellationToken cancellationToken)
    {
        var pendientes = new List<(long Id, string Nombre)>();

        await using (var comando = conexion.CreateCommand())
        {
            comando.CommandText = "SELECT id, nombre FROM registros";
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                var nombre = lector.GetString(1);
                if (!_proteccion.IsProtected(nombre))
                {
                    pendientes.Add((lector.GetInt64(0), nombre));
                }
            }
        }

        if (pendientes.Count == 0)
        {
            return false;
        }

        await using var transaccion = await conexion.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var (id, nombre) in pendientes)
            {
                await using var comando = conexion.CreateCommand();
                comando.Transaction = (SqliteTransaction)transaccion;
                comando.CommandText = "UPDATE registros SET nombre = $nombre WHERE id = $id";
                comando.Parameters.AddWithValue("$nombre", Cifrar(Normalizar(nombre)));
                comando.Parameters.AddWithValue("$id", id);
                await comando.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaccion.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaccion.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
