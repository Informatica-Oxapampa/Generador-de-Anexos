#nullable enable

using System;
using System.Text.Json;
using GeneradorAnexos.Domain.Models;

namespace GeneradorAnexos.Domain.Serialization;

/// <summary>Serializa el contrato JSON v1 sin descartar campos desconocidos.</summary>
public static class PayloadJson
{
    /// <summary>Deserializa y valida la versión raíz del payload.</summary>
    /// <exception cref="PayloadJsonException">
    /// El JSON está dañado, no representa un objeto válido o usa otra versión.
    /// </exception>
    public static BorradorPayloadV1 Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new PayloadJsonException("El contenido JSON del registro está vacío.");
        }

        BorradorPayloadV1 payload;
        try
        {
            payload = JsonSerializer.Deserialize<BorradorPayloadV1>(json, CreateOptions())
                ?? throw new PayloadJsonException("El contenido JSON del registro no es válido.");
        }
        catch (PayloadJsonException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new PayloadJsonException(
                "No se pudo interpretar el contenido JSON del registro.", exception);
        }

        if (payload.Version != BorradorPayloadV1.VersionActual)
        {
            throw new PayloadJsonException(
                $"La versión del registro ({payload.Version}) no es compatible; " +
                $"se esperaba la versión {BorradorPayloadV1.VersionActual}.");
        }

        return payload;
    }

    /// <summary>Serializa un payload v1, incluyendo sus datos de extensión.</summary>
    public static string Serialize(BorradorPayloadV1 payload, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Version != BorradorPayloadV1.VersionActual)
        {
            throw new PayloadJsonException(
                $"La versión del registro ({payload.Version}) no es compatible; " +
                $"se esperaba la versión {BorradorPayloadV1.VersionActual}.");
        }

        return JsonSerializer.Serialize(payload, CreateOptions(indented));
    }

    private static JsonSerializerOptions CreateOptions(bool indented = false) => new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = indented,
    };
}

/// <summary>Error controlado al leer o escribir el payload persistido.</summary>
public sealed class PayloadJsonException : Exception
{
    public PayloadJsonException(string message)
        : base(message)
    {
    }

    public PayloadJsonException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
