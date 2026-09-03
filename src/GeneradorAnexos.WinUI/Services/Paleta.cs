using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace GeneradorAnexos.WinUI.Services;

/// <summary>
/// Acceso desde código a los pinceles de la paleta, resolviendo siempre contra
/// el tema que la aplicación está usando de verdad.
/// </summary>
/// <remarks>
/// <b>Por qué existe esta clase.</b> En XAML, <c>{ThemeResource Ga.X}</c> se
/// resuelve contra el tema del elemento, así que responde correctamente cuando
/// el usuario fija el tema claro u oscuro desde Configuración.
///
/// Desde código, en cambio, <c>Application.Current.Resources["Ga.X"]</c>
/// resuelve contra el tema de la <i>aplicación</i>, que sigue al de Windows.
/// Si el usuario elegía tema oscuro con Windows en claro, todo lo pintado desde
/// código —bordes de validación, celdas de las tablas, destellos de
/// sincronización, colores de la barra de título— se quedaba con los colores
/// del tema contrario.
///
/// Esta clase busca la clave dentro del diccionario de tema correcto y solo
/// recurre a la búsqueda normal si no la encuentra.
/// </remarks>
public static class Paleta
{
    /// <summary>Pincel de la paleta para el tema activo.</summary>
    public static Brush Pincel(string clave)
    {
        if (Buscar(clave) is Brush pincel)
        {
            return pincel;
        }

        // Respaldo: búsqueda normal. Nunca debería hacer falta, pero es
        // preferible a devolver null y provocar un cierre inesperado.
        return (Brush)Microsoft.UI.Xaml.Application.Current.Resources[clave];
    }

    /// <summary>Color de la paleta para el tema activo.</summary>
    public static Windows.UI.Color Color(string clave) => ((SolidColorBrush)Pincel(clave)).Color;

    private static object? Buscar(string clave)
    {
        var nombreTema = ServicioTema.TemaEfectivo == ElementTheme.Dark ? "Dark" : "Light";

        try
        {
            foreach (var diccionario in Microsoft.UI.Xaml.Application.Current.Resources.MergedDictionaries)
            {
                if (diccionario.ThemeDictionaries.TryGetValue(nombreTema, out var tema)
                    && tema is ResourceDictionary paleta
                    && paleta.TryGetValue(clave, out var valor))
                {
                    return valor;
                }
            }
        }
        catch (Exception excepcion)
        {
            Registro.Advertencia("PALETTE_LOOKUP_" + excepcion.GetType().Name);
        }

        return null;
    }
}
