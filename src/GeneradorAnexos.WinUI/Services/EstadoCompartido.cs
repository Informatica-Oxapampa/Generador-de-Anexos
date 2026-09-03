using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneradorAnexos.WinUI.Services;

/// <summary>
/// Equivalente de <c>core/estado.py: EstadoCompartido</c>.
/// </summary>
/// <remarks>
/// Cada cambio se publica junto con el <c>origen</c> que lo produjo, de modo
/// que el campo que el usuario esta editando no se reescriba a si mismo. Es lo
/// que evita saltos de cursor y bucles de eventos, igual que en el original.
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
/// <param name="Origen">Control que origino el cambio, o <c>null</c>.</param>
public readonly record struct CambioCompartido(string Texto, object? Origen);

/// <summary>
/// Equivalente de <c>ui/sincronizacion.py: SincronizadorUnidireccional</c>.
/// </summary>
/// <remarks>
/// Un unico campo origen (Denominacion del servicio) alimenta a varios
/// receptores. Reglas conservadas del original:
/// <list type="bullet">
///   <item>solo el origen propaga; los receptores nunca reenvian;</item>
///   <item>al editar un receptor a mano, ese receptor queda personalizado y
///         deja de recibir actualizaciones;</item>
///   <item>si el usuario vacia un receptor personalizado, vuelve a quedar
///         disponible para la sincronizacion.</item>
/// </list>
/// </remarks>
public sealed class SincronizadorUnidireccional
{
    private readonly List<Receptor> _receptores = new();
    private readonly Func<string> _leerOrigen;

    private bool _aplicando;
    private bool _silencio;

    /// <param name="leerOrigen">Lectura del valor actual del campo origen.</param>
    public SincronizadorUnidireccional(Func<string> leerOrigen)
        => _leerOrigen = leerOrigen;

    /// <summary>Registra un receptor identificado por su clave de persistencia.</summary>
    /// <param name="clave">Clave usada en <c>sync_personalizado</c> del JSON.</param>
    /// <param name="leer">Lectura del valor actual del receptor.</param>
    /// <param name="escribir">Escritura con destello de sincronizacion.</param>
    public void Agregar(string clave, Func<string> leer, Action<string> escribir)
        => _receptores.Add(new Receptor(clave, leer, escribir));

    /// <summary>Llamar cuando cambia el texto del campo origen.</summary>
    public void Propagar()
    {
        if (_aplicando || _silencio)
        {
            return;
        }

        var texto = _leerOrigen();
        _aplicando = true;
        try
        {
            foreach (var receptor in _receptores)
            {
                if (receptor.Personalizado || receptor.Leer() == texto)
                {
                    continue;
                }

                receptor.Escribir(texto);
            }
        }
        finally
        {
            _aplicando = false;
        }
    }

    /// <summary>Llamar cuando cambia el texto de un receptor.</summary>
    public void NotificarEdicion(string clave)
    {
        if (_aplicando || _silencio)
        {
            return; // Cambio programatico: sincronizacion o carga de borrador.
        }

        var receptor = _receptores.FirstOrDefault(r => r.Clave == clave);
        if (receptor is not null)
        {
            receptor.Personalizado = !string.IsNullOrEmpty(receptor.Leer());
        }
    }

    /// <summary>Suspende la sincronizacion mientras se carga un borrador.</summary>
    public void Silenciar(bool valor) => _silencio = valor;

    public Dictionary<string, bool> EstadoPersonalizado()
        => _receptores.ToDictionary(r => r.Clave, r => r.Personalizado);

    public void AplicarPersonalizado(IReadOnlyDictionary<string, bool>? datos)
    {
        foreach (var receptor in _receptores)
        {
            receptor.Personalizado =
                datos is not null && datos.TryGetValue(receptor.Clave, out var v) && v;
        }
    }

    private sealed class Receptor
    {
        public Receptor(string clave, Func<string> leer, Action<string> escribir)
        {
            Clave = clave;
            Leer = leer;
            Escribir = escribir;
        }

        public string Clave { get; }

        public Func<string> Leer { get; }

        public Action<string> Escribir { get; }

        public bool Personalizado { get; set; }
    }
}
