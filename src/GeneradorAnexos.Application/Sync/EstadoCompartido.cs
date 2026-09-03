using System;
using System.Collections.Generic;

namespace GeneradorAnexos.Application.Sync;

/// <summary>
/// Equivalente de <c>core/estado.py: EstadoCompartido</c>.
/// </summary>
/// <remarks>
/// Cada cambio se publica junto con el <c>origen</c> que lo produjo, de modo
/// que el campo que el usuario está editando no se reescriba a sí mismo. Es lo
/// que evita saltos de cursor y bucles de eventos, igual que en el original.
/// Vive en Application porque no depende de WinUI: las vistas y las pruebas
/// lo consumen por igual.
/// </remarks>
public sealed class EstadoCompartido
{
    public const string ClaveNumeroPedido = "NUM_PEDIDO";

    private string _descripcion = string.Empty;
    private string _plazo = string.Empty;
    private string _numeroPedido = string.Empty;

    public event EventHandler<CambioCompartido>? DescripcionCambiada;

    public event EventHandler<CambioCompartido>? PlazoCambiado;

    public event EventHandler<CambioCompartido>? NumeroPedidoCambiado;

    public string Descripcion => _descripcion;

    public string Plazo => _plazo;

    public string NumeroPedido => _numeroPedido;

    public void EstablecerDescripcion(string? texto, object? origen = null)
    {
        var valor = texto ?? string.Empty;
        if (valor == _descripcion)
        {
            return;
        }

        _descripcion = valor;
        DescripcionCambiada?.Invoke(this, new CambioCompartido(valor, origen));
    }

    public void EstablecerPlazo(string? texto, object? origen = null)
    {
        var valor = texto ?? string.Empty;
        if (valor == _plazo)
        {
            return;
        }

        _plazo = valor;
        PlazoCambiado?.Invoke(this, new CambioCompartido(valor, origen));
    }

    public void EstablecerNumeroPedido(string? texto, object? origen = null)
    {
        var valor = texto ?? string.Empty;
        if (valor == _numeroPedido)
        {
            return;
        }

        _numeroPedido = valor;
        NumeroPedidoCambiado?.Invoke(this, new CambioCompartido(valor, origen));
    }

    /// <summary>Pieza de contexto documental compartida por TDR y Anexos.</summary>
    public Dictionary<string, string> ContextoNumeroPedido() =>
        new() { [ClaveNumeroPedido] = _numeroPedido };
}

/// <summary>Cambio publicado por el estado compartido.</summary>
/// <param name="Texto">Valor nuevo.</param>
/// <param name="Origen">Control que originó el cambio, o <c>null</c>.</param>
public readonly record struct CambioCompartido(string Texto, object? Origen);
