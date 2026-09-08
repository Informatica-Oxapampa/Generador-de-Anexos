using System;
using Microsoft.UI.Xaml;

namespace GeneradorAnexos.WinUI.Services;

/// <summary>
/// Aplica y conserva el tema de la aplicación: seguir a Windows, claro fijo u
/// oscuro fijo.
/// </summary>
/// <remarks>
/// El tema se aplica sobre el elemento raíz de la ventana, de modo que todos
/// los controles descendientes lo heredan de una sola vez: páginas, tarjetas,
/// campos, listas, tablas y controles propios.
///
/// Con <see cref="PreferenciasUi.TemaSistema"/> se usa
/// <see cref="ElementTheme.Default"/>, que es el modo en que WinUI sigue al
/// sistema por sí solo: si el usuario cambia Windows de claro a oscuro con la
/// aplicación abierta, la interfaz cambia sin reiniciar.
///
/// Los <c>ContentDialog</c> son la excepción: se alojan fuera del árbol de la
/// ventana, así que no heredan el tema. Por eso
/// <see cref="ServicioDialogos"/> consulta <see cref="TemaEfectivo"/> y se lo
/// asigna a cada diálogo que crea.
/// </remarks>
public static class ServicioTema
{
    private static readonly PreferenciasUi Preferencias = new();

    /// <summary>
    /// Raíz sobre la que se aplica el tema. Se registra desde el constructor de
    /// la ventana, cuando <c>App.Ventana</c> todavía no está asignada.
    /// </summary>
    private static FrameworkElement? _raiz;

    /// <summary>Modo elegido por el usuario, tal como se guarda.</summary>
    public static string Modo { get; private set; } = PreferenciasUi.TemaSistema;

    /// <summary>Se dispara cuando el usuario cambia el modo.</summary>
    public static event EventHandler? Cambiado;

    /// <summary>
    /// Tema con el que se está dibujando ahora mismo. Con el modo «sistema»
    /// devuelve el que Windows tenga activo en este instante.
    /// </summary>
    public static ElementTheme TemaEfectivo
    {
        get
        {
            var raiz = _raiz ?? App.Ventana?.Content as FrameworkElement;
            if (raiz is not null && raiz.ActualTheme != ElementTheme.Default)
            {
                return raiz.ActualTheme;
            }

            return Modo switch
            {
                PreferenciasUi.TemaOscuro => ElementTheme.Dark,
                _ => ElementTheme.Light,
            };
        }
    }

    /// <summary>
    /// Registra la raíz de la ventana y aplica el tema guardado. Se llama una
    /// sola vez, desde el constructor de la ventana principal.
    /// </summary>
    public static void Inicializar(FrameworkElement raiz)
    {
        _raiz = raiz;
        Aplicar(Preferencias.Tema, guardar: false);
    }

    /// <summary>Aplica un modo y, salvo que se indique lo contrario, lo guarda.</summary>
    public static void Aplicar(string modo, bool guardar = true)
    {
        Modo = modo is PreferenciasUi.TemaClaro
            or PreferenciasUi.TemaOscuro
            or PreferenciasUi.TemaSistema
            ? modo
            : PreferenciasUi.TemaSistema;

        var destino = _raiz ?? App.Ventana?.Content as FrameworkElement;
        if (destino is not null)
        {
            destino.RequestedTheme = ATema(Modo);
        }

        if (guardar)
        {
            Preferencias.Tema = Modo;
            Registro.Info("THEME_SET_" + Modo.ToUpperInvariant());
        }

        Cambiado?.Invoke(null, EventArgs.Empty);
    }

    private static ElementTheme ATema(string modo) => modo switch
    {
        PreferenciasUi.TemaClaro => ElementTheme.Light,
        PreferenciasUi.TemaOscuro => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };
}
