using System.Data;
using System.Globalization;
using System.Text;
using GeneradorAnexos.Application.Abstractions.Persistence;
using GeneradorAnexos.Application.Abstractions.Security;
using GeneradorAnexos.Domain.Documents;
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
    private const int LongitudMaximaNombre = 120;
    private const int TamanoMaximoPayloadBytes = 2 * 1024 * 1024;
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
        var rutaHeredadaCopiada = await MigrarRutaHeredadaAsync(cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(_rutaBase)!);

        await using var conexion = Abrir();
        await conexion.OpenAsync(cancellationToken);

        await ConfigurarConexionAsync(conexion, cancellationToken);

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

        var datosHeredadosMigrados = await MigrarRegistrosHeredadosAsync(conexion, cancellationToken);
        if (datosHeredadosMigrados)
        {
            await PurgarResiduosMigracionAsync(conexion, cancellationToken);
        }

        await using (var version = conexion.CreateCommand())
        {
            version.CommandText = "PRAGMA user_version = 2;";
            await version.ExecuteNonQueryAsync(cancellationToken);
        }

        var respaldoMigracionCreado = true;
        if (rutaHeredadaCopiada || datosHeredadosMigrados)
        {
            respaldoMigracionCreado = await _respaldos.CreateAsync(cancellationToken);
        }

        if (rutaHeredadaCopiada && respaldoMigracionCreado)
        {
            // Solo se retira el original después de cifrar, compactar y crear
            // un respaldo íntegro de la nueva ubicación.
            IntentarEliminarBaseHeredada();
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
            "SELECT id, nombre, datos, creado, actualizado FROM registros ORDER BY actualizado DESC";

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            var id = lector.GetInt64(0);
            string nombre;
            BorradorPayloadV1? contenido = null;
            var danado = false;

            try
            {
                nombre = NormalizarHeredado(Descifrar(lector.GetString(1)), id);
                contenido = PayloadJson.Deserialize(Descifrar(lector.GetString(2)));
            }
            catch (Exception excepcion) when (excepcion is DataProtectionException or
                PayloadJsonException or FormatException or InvalidOperationException)
            {
                nombre = $"Registro dañado #{id}";
                danado = true;
            }

            // El filtro se aplica en memoria porque el nombre está cifrado en
            // reposo y no puede compararse dentro de la consulta SQL.
            if (!string.IsNullOrWhiteSpace(search) &&
                !nombre.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            resultados.Add(new SavedRecordSummary(
                id,
                nombre,
                ParseDateOrDefault(lector.GetString(3)),
                ParseDateOrDefault(lector.GetString(4)),
                !danado && ContenidoRegistro.TieneTdr(contenido),
                !danado && ContenidoRegistro.TieneAnexo(contenido),
                danado));
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
            NormalizarHeredado(Descifrar(lector.GetString(1)), lector.GetInt64(0)),
            payload,
            ParseDateOrDefault(lector.GetString(3)),
            ParseDateOrDefault(lector.GetString(4)));
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
        comando.Parameters.AddWithValue("$datos", Cifrar(SerializarPayload(payload)));
        comando.Parameters.AddWithValue("$creado", ahora);
        comando.Parameters.AddWithValue("$actualizado", ahora);

        var id = (long)(await comando.ExecuteScalarAsync(cancellationToken))!;
        _ = await _respaldos.CreateAsync(cancellationToken);
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
        comando.Parameters.AddWithValue("$datos", Cifrar(SerializarPayload(payload)));
        comando.Parameters.AddWithValue("$actualizado", DateTime.UtcNow.ToString("O"));
        comando.Parameters.AddWithValue("$id", id);

        var afectadas = await comando.ExecuteNonQueryAsync(cancellationToken);
        ExigirUnaFila(afectadas, id);
        _ = await _respaldos.CreateAsync(cancellationToken);
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

        var afectadas = await comando.ExecuteNonQueryAsync(cancellationToken);
        ExigirUnaFila(afectadas, id);
        _ = await _respaldos.CreateAsync(cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var conexion = Abrir();
        await conexion.OpenAsync(cancellationToken);

        await using var comando = conexion.CreateCommand();
        comando.CommandText = "DELETE FROM registros WHERE id = $id";
        comando.Parameters.AddWithValue("$id", id);

        var afectadas = await comando.ExecuteNonQueryAsync(cancellationToken);
        ExigirUnaFila(afectadas, id);
        _ = await _respaldos.CreateAsync(cancellationToken);
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

            try
            {
                var candidato = NormalizarHeredado(Descifrar(lector.GetString(1)), id);
                if (string.Equals(candidato, buscado, StringComparison.OrdinalIgnoreCase))
                {
                    return id;
                }
            }
            catch (DataProtectionException)
            {
                // Una fila dañada no debe impedir buscar ni guardar las demás.
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
        DefaultTimeout = 5,
    }.ToString());

    private static string Normalizar(string? nombre)
    {
        var limpio = (nombre ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);
        limpio = limpio.Length == 0 ? "Registro sin nombre" : limpio;
        if (limpio.Length > LongitudMaximaNombre)
        {
            throw new ArgumentException($"El nombre no puede superar {LongitudMaximaNombre} caracteres.", nameof(nombre));
        }

        if (limpio.Any(caracter =>
                CharUnicodeInfo.GetUnicodeCategory(caracter) is
                    UnicodeCategory.Control or
                    UnicodeCategory.Format or
                    UnicodeCategory.Surrogate))
        {
            throw new ArgumentException("El nombre contiene caracteres de control no permitidos.", nameof(nombre));
        }

        return limpio;
    }

    private static string NormalizarHeredado(string? nombre, long id)
    {
        var limpio = new string((nombre ?? string.Empty)
            .Where(caracter =>
                CharUnicodeInfo.GetUnicodeCategory(caracter) is not (
                    UnicodeCategory.Control or
                    UnicodeCategory.Format or
                    UnicodeCategory.Surrogate))
            .ToArray())
            .Trim()
            .Normalize(NormalizationForm.FormC);

        if (limpio.Length > LongitudMaximaNombre)
        {
            limpio = limpio[..LongitudMaximaNombre];
        }

        return limpio.Length == 0 ? $"Registro migrado #{id}" : limpio;
    }

    private string Cifrar(string texto) => _proteccion.Protect(texto);

    /// <summary>Descifra únicamente el formato protegido esperado.</summary>
    /// <remarks>
    /// Los valores heredados en texto plano se convierten durante
    /// <see cref="InitializeAsync"/>. A partir de ahí, aceptar texto sin el
    /// sobre DPAPI permitiría que una modificación externa de la base se
    /// interpretase como un registro legítimo.
    /// </remarks>
    private string Descifrar(string valor)
        => _proteccion.IsProtected(valor)
            ? _proteccion.Unprotect(valor)
            : throw new DataProtectionException(DataProtectionFailure.InvalidEnvelope);

    /// <summary>Cifra nombre y contenido heredados en una única transacción.</summary>
    private async Task<bool> MigrarRegistrosHeredadosAsync(
        SqliteConnection conexion, CancellationToken cancellationToken)
    {
        var pendientes = new List<(long Id, string? NombrePlano, string? DatosPlanos)>();

        await using (var comando = conexion.CreateCommand())
        {
            comando.CommandText = "SELECT id, nombre, datos FROM registros";
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                var nombreGuardado = lector.GetString(1);
                var datosGuardados = lector.GetString(2);
                var nombrePlano = _proteccion.IsProtected(nombreGuardado) ? null : nombreGuardado;
                var datosPlanos = _proteccion.IsProtected(datosGuardados) ? null : datosGuardados;
                if (nombrePlano is not null || datosPlanos is not null)
                {
                    pendientes.Add((lector.GetInt64(0), nombrePlano, datosPlanos));
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
            foreach (var (id, nombrePlano, datosPlanos) in pendientes)
            {
                await using var comando = conexion.CreateCommand();
                comando.Transaction = (SqliteTransaction)transaccion;
                comando.CommandText = """
                    UPDATE registros
                    SET nombre = COALESCE($nombre, nombre),
                        datos = COALESCE($datos, datos)
                    WHERE id = $id
                    """;
                comando.Parameters.AddWithValue(
                    "$nombre",
                    nombrePlano is null
                        ? (object)DBNull.Value
                        : Cifrar(NormalizarHeredado(nombrePlano, id)));
                comando.Parameters.AddWithValue(
                    "$datos",
                    datosPlanos is null ? (object)DBNull.Value : Cifrar(datosPlanos));
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

    private static async Task ConfigurarConexionAsync(
        SqliteConnection conexion,
        CancellationToken cancellationToken)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "PRAGMA busy_timeout = 5000; PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateTime ParseDateOrDefault(string value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result)
            ? result
            : DateTime.UnixEpoch;

    private static string SerializarPayload(BorradorPayloadV1 payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var json = PayloadJson.Serialize(payload);
        if (Encoding.UTF8.GetByteCount(json) > TamanoMaximoPayloadBytes)
        {
            throw new InvalidDataException("El registro supera el tamaño máximo permitido de 2 MB.");
        }

        return json;
    }

    private static void ExigirUnaFila(int afectadas, long id)
    {
        if (afectadas != 1)
        {
            throw new DBConcurrencyException($"El registro {id} ya no existe o fue modificado.");
        }
    }

    private async Task<bool> MigrarRutaHeredadaAsync(CancellationToken cancellationToken)
    {
        var heredada = RutasDatos.RutaBaseHeredada();
        if (File.Exists(_rutaBase) || !File.Exists(heredada))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_rutaBase)!);
        var temporal = _rutaBase + ".migrando-" + Guid.NewGuid().ToString("N");
        try
        {
            await using var origen = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = heredada,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private,
                DefaultTimeout = 5,
            }.ToString());
            await using var destino = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = temporal,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                DefaultTimeout = 5,
            }.ToString());

            await origen.OpenAsync(cancellationToken);
            await destino.OpenAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            origen.BackupDatabase(destino);

            await using var comprobar = destino.CreateCommand();
            comprobar.CommandText = "PRAGMA integrity_check;";
            var resultado = (await comprobar.ExecuteScalarAsync(cancellationToken))?.ToString();
            if (!string.Equals(resultado, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("La base heredada no superó la comprobación de integridad.");
            }

            await origen.CloseAsync();
            await destino.CloseAsync();
            File.Move(temporal, _rutaBase, overwrite: false);
            return true;
        }
        finally
        {
            if (File.Exists(temporal))
            {
                File.Delete(temporal);
            }
        }
    }

    /// <summary>
    /// VACUUM reconstruye el archivo tras cifrar filas heredadas y evita que
    /// el texto plano permanezca recuperable en páginas libres. El checkpoint
    /// trunca además el WAL que pudo contener los valores anteriores.
    /// </summary>
    private static async Task PurgarResiduosMigracionAsync(
        SqliteConnection conexion,
        CancellationToken cancellationToken)
    {
        foreach (var sql in new[]
                 {
                     "PRAGMA wal_checkpoint(TRUNCATE);",
                     "VACUUM;",
                     "PRAGMA wal_checkpoint(TRUNCATE);",
                 })
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = sql;
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void IntentarEliminarBaseHeredada()
    {
        try
        {
            var heredada = RutasDatos.RutaBaseHeredada();
            if (File.Exists(heredada))
            {
                File.Delete(heredada);
            }

            foreach (var sufijo in new[] { "-wal", "-shm" })
            {
                var auxiliar = heredada + sufijo;
                if (File.Exists(auxiliar))
                {
                    File.Delete(auxiliar);
                }
            }
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            // No se arriesga la base nueva si Windows mantiene bloqueado el
            // archivo heredado; se intentará retirar manualmente con la app cerrada.
        }
    }
}
