using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneradorAnexos.Application.Sync;

/// <summary>
/// Equivalente de <c>ui/sincronizacion.py: SincronizadorUnidireccional</c>.
/// </summary>
/// <remarks>
/// Un único campo origen (Denominación del servicio) alimenta a varios
/// receptores. Reglas conservadas del original:
/// <list type="bullet">
///   <item>solo el origen propaga; los receptores nunca reenvían;</item>
///   <item>al editar un receptor a mano, ese receptor queda personalizado y
///         deja de recibir actualizaciones;</item>
///   <item>si el usuario vacía un receptor personalizado, vuelve a quedar
///         disponible para la sincronización.</item>
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
    /// <param name="escribir">Escritura con destello de sincronización.</param>
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
            return; // Cambio programático: sincronización o carga de borrador.
        }

        var receptor = _receptores.FirstOrDefault(r => r.Clave == clave);
        if (receptor is not null)
        {
            receptor.Personalizado = !string.IsNullOrEmpty(receptor.Leer());
        }
    }

    /// <summary>Suspende la sincronización mientras se carga un borrador.</summary>
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
