using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeneradorAnexos.Domain.Formatting;
using GeneradorAnexos.Domain.Models;

namespace GeneradorAnexos.Domain.Payments;

/// <summary>Error de coherencia del plan de pagos definido en el TDR.</summary>
public sealed class PlanPagosException : Exception
{
    public PlanPagosException(string mensaje) : base(mensaje)
    {
    }
}

/// <summary>Cuota derivada de un entregable y de su condicion de pago.</summary>
public sealed record CuotaPago(
    int Indice,
    string Descripcion,
    string Plazo,
    string Condicion,
    int Porcentaje,
    decimal? Monto);

/// <summary>Vista inmutable del plan que consumen los documentos.</summary>
public sealed record PlanPagos(
    string Modo,
    IReadOnlyList<CuotaPago> Cuotas,
    decimal? MontoTotal);

/// <summary>
/// Equivalente de <c>core/plan_pagos.py</c>.
/// </summary>
/// <remarks>
/// La proyeccion se construye siempre desde el estado vigente del TDR y nunca
/// se persiste por separado, de modo que el Anexo no pueda conservar una copia
/// obsoleta o contradictoria de los entregables y porcentajes.
/// </remarks>
public static class ConstructorPlanPagos
{
    public const string TextoFormaPagoUnico =
        "Según los Términos de Referencia y/o Especificaciones Técnicas.";

    public const string ModoUnico = "unico";

    public const string ModoMultiple = "multiple";

    /// <summary>Construye y valida la forma de pago vigente a partir del TDR.</summary>
    /// <param name="tdr">Estado del TDR; <c>null</c> se interpreta como pago único.</param>
    /// <param name="montoTotal">Monto en texto tal como lo escribió el usuario.</param>
    /// <exception cref="PlanPagosException">Si el TDR no es coherente.</exception>
    public static PlanPagos Construir(TdrPayload? tdr, string? montoTotal)
    {
        var modo = string.IsNullOrEmpty(tdr?.Modo) ? ModoUnico : tdr!.Modo!;
        if (modo != ModoUnico && modo != ModoMultiple)
        {
            throw new PlanPagosException($"El modo de entregables «{modo}» no es válido.");
        }

        var total = ConvertirMonto(montoTotal);

        if (modo == ModoUnico)
        {
            var cuota = new CuotaPago(1, string.Empty, string.Empty,
                TextoFormaPagoUnico, 100, total);
            return new PlanPagos(ModoUnico, new[] { cuota }, total);
        }

        var entregables = tdr?.Entregables;
        var pagos = tdr?.Pagos;

        if (entregables is null)
        {
            throw new PlanPagosException("Los entregables múltiples del TDR no son válidos.");
        }

        if (entregables.Count < 2)
        {
            throw new PlanPagosException(
                "El modo múltiple requiere al menos dos entregables en el TDR.");
        }

        if (pagos is null)
        {
            throw new PlanPagosException("Los pagos múltiples del TDR no son válidos.");
        }

        if (entregables.Count != pagos.Count)
        {
            throw new PlanPagosException(
                "Cada entregable del TDR debe tener exactamente un pago asociado.");
        }

        var filas = new List<(string Descripcion, string Plazo, string Condicion, int Porcentaje)>();
        for (var i = 0; i < entregables.Count; i++)
        {
            var indice = i + 1;
            var entregable = entregables[i];
            var pago = pagos[i];

            if (entregable is null)
            {
                throw new PlanPagosException($"El entregable {indice} no tiene un formato válido.");
            }

            if (pago is null)
            {
                throw new PlanPagosException($"El pago {indice} no tiene un formato válido.");
            }

            filas.Add((
                TextoObligatorio(entregable.Descripcion, $"La descripción del entregable {indice}"),
                TextoObligatorio(entregable.Plazo, $"El plazo del entregable {indice}"),
                TextoObligatorio(pago.Condicion, $"La condición del pago {indice}"),
                PorcentajeEntero(pago.Porcentaje, indice)));
        }

        var porcentajes = filas.Select(f => f.Porcentaje).ToArray();
        var suma = porcentajes.Sum();
        if (suma != 100)
        {
            throw new PlanPagosException(
                $"La suma de los porcentajes debe ser 100 % (actual: {suma} %).");
        }

        var montos = MontosPorPorcentaje(total, porcentajes);
        var cuotas = filas
            .Select((fila, i) => new CuotaPago(
                i + 1, fila.Descripcion, fila.Plazo, fila.Condicion, fila.Porcentaje, montos[i]))
            .ToArray();

        return new PlanPagos(ModoMultiple, cuotas, total);
    }

    /// <summary>
    /// Construye una proyección tolerante para vista previa. Conserva todo lo
    /// escrito hasta el momento y completa lo ausente con valores vacíos; no
    /// sustituye a <see cref="Construir"/> para la exportación final.
    /// </summary>
    public static PlanPagos ConstruirVistaPrevia(TdrPayload? tdr, string? montoTotal)
    {
        try
        {
            return Construir(tdr, montoTotal);
        }
        catch (PlanPagosException)
        {
            // La vista previa es deliberadamente tolerante. La exportación
            // final sigue usando Construir y mantiene todas sus invariantes.
        }

        var total = ConvertirMontoVistaPrevia(montoTotal);
        if (!string.Equals(tdr?.Modo, ModoMultiple, StringComparison.Ordinal))
        {
            var cuotaUnica = new CuotaPago(
                1,
                string.Empty,
                string.Empty,
                TextoFormaPagoUnico,
                100,
                total);
            return new PlanPagos(ModoUnico, new[] { cuotaUnica }, total);
        }

        var entregables = tdr?.Entregables ?? new List<EntregablePayload?>();
        var pagos = tdr?.Pagos ?? new List<PagoPayload?>();
        var cantidad = Math.Max(1, Math.Max(entregables.Count, pagos.Count));
        var porcentajes = new int[cantidad];
        var filas = new (string Descripcion, string Plazo, string Condicion)[cantidad];

        for (var i = 0; i < cantidad; i++)
        {
            var entregable = i < entregables.Count ? entregables[i] : null;
            var pago = i < pagos.Count ? pagos[i] : null;
            filas[i] = (
                entregable?.Descripcion?.Trim() ?? string.Empty,
                entregable?.Plazo?.Trim() ?? string.Empty,
                pago?.Condicion?.Trim() ?? string.Empty);
            porcentajes[i] = pago?.Porcentaje is >= 1 and <= 100
                ? pago.Porcentaje.Value
                : 0;
        }

        decimal?[] montos = new decimal?[cantidad];
        if (total is not null && porcentajes.All(p => p > 0) && porcentajes.Sum() == 100)
        {
            try
            {
                montos = MontosPorPorcentaje(total, porcentajes);
            }
            catch (PlanPagosException)
            {
                // Un monto demasiado pequeño se muestra sin reparto durante
                // la vista previa; Generar seguirá rechazándolo.
            }
        }

        var cuotas = Enumerable.Range(0, cantidad)
            .Select(i => new CuotaPago(
                i + 1,
                filas[i].Descripcion,
                filas[i].Plazo,
                filas[i].Condicion,
                porcentajes[i],
                montos[i]))
            .ToArray();

        return new PlanPagos(ModoMultiple, cuotas, total);
    }

    /// <summary>
    /// Convierte el monto escrito por el usuario, aceptando separador de miles
    /// solo si la agrupacion es valida, igual que el original.
    /// </summary>
    private static decimal? ConvertirMonto(string? valor)
    {
        if (valor is null)
        {
            return null;
        }

        var texto = valor.Replace("S/", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        if (texto.Length == 0)
        {
            return null;
        }

        if (texto.Contains(',', StringComparison.Ordinal))
        {
            var parteEntera = texto.Split('.', 2)[0];
            var grupos = parteEntera.Split(',');
            var agrupacionValida =
                grupos[0].Length is >= 1 and <= 3 && grupos[0].All(char.IsDigit) &&
                grupos.Skip(1).All(g => g.Length == 3 && g.All(char.IsDigit));

            if (!agrupacionValida)
            {
                throw new PlanPagosException(
                    "El monto total debe ser un número válido mayor que cero.");
            }

            texto = texto.Replace(",", string.Empty, StringComparison.Ordinal);
        }

        if (texto == "." || texto.Count(c => c == '.') > 1 ||
            texto.Any(c => !char.IsDigit(c) && c != '.'))
        {
            throw new PlanPagosException(
                "El monto total debe ser un número válido mayor que cero.");
        }

        if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var monto))
        {
            throw new PlanPagosException(
                "El monto total debe ser un número válido mayor que cero.");
        }

        if (monto <= 0)
        {
            throw new PlanPagosException("El monto total debe ser un número mayor que cero.");
        }

        return Math.Round(monto, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal? ConvertirMontoVistaPrevia(string? valor)
        => DocumentFormatting.TryParseAmount(valor, out var monto) && monto > 0
            ? Math.Round(monto, 2, MidpointRounding.AwayFromZero)
            : null;

    private static string TextoObligatorio(string? valor, string etiqueta)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new PlanPagosException($"{etiqueta} no puede estar vacío.");
        }

        return valor.Trim();
    }

    private static int PorcentajeEntero(int? valor, int indice)
    {
        if (valor is null)
        {
            throw new PlanPagosException(
                $"El porcentaje del pago {indice} debe ser un número entero.");
        }

        if (valor is < 1 or > 100)
        {
            throw new PlanPagosException(
                $"El porcentaje del pago {indice} debe estar entre 1 y 100.");
        }

        return valor.Value;
    }

    /// <summary>
    /// Reparte el total en centavos por porcentaje, asignando los centavos
    /// sobrantes a las cuotas con mayor resto (mismo criterio que el original).
    /// </summary>
    private static decimal?[] MontosPorPorcentaje(decimal? total, int[] porcentajes)
    {
        if (total is null)
        {
            return new decimal?[porcentajes.Length];
        }

        var centavosTotales = (long)Math.Round(total.Value * 100m, MidpointRounding.AwayFromZero);
        var exactos = porcentajes.Select(p => centavosTotales * (decimal)p / 100m).ToArray();
        var centavos = exactos.Select(e => (long)Math.Floor(e)).ToArray();
        var restantes = centavosTotales - centavos.Sum();

        var orden = Enumerable.Range(0, exactos.Length)
            .OrderByDescending(i => exactos[i] - centavos[i])
            .ThenBy(i => i)
            .Take((int)restantes);

        foreach (var indice in orden)
        {
            centavos[indice]++;
        }

        if (centavos.Any(v => v <= 0))
        {
            throw new PlanPagosException(
                "El monto total no permite asignar al menos S/ 0.01 a cada pago.");
        }

        return centavos.Select(v => (decimal?)(v / 100m)).ToArray();
    }
}
