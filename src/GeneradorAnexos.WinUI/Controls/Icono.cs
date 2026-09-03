using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GeneradorAnexos.WinUI.Controls;

/// <summary>
/// Icono de la interfaz, dibujado con la fuente de iconos del propio Windows.
/// </summary>
/// <remarks>
/// Antes cada icono era un trazo vectorial propio, heredado del catalogo SVG
/// del aplicativo Python. Eso producia iconos de grosor y encuadre desiguales.
/// Ahora cada nombre se traduce a un valor del enumerado <see cref="Symbol"/>
/// de WinUI, cuyos codigos pertenecen a la fuente Segoe Fluent Icons
/// (Windows 11) con respaldo en Segoe MDL2 Assets (Windows 10). Con eso todos
/// los iconos comparten metrica, grosor y alineacion, y siguen el tema claro u
/// oscuro porque el color sale siempre de un pincel del tema.
///
/// Los nombres se conservan (user, trash, ...) para no tocar cada punto de uso.
/// </remarks>
public sealed partial class Icono : Control
{
    /// <summary>Traduccion de los nombres del catalogo original a iconos del sistema.</summary>
    private static readonly Dictionary<string, Symbol> Equivalencias = new()
    {
        ["user"] = Symbol.Contact,
        ["users"] = Symbol.People,
        ["id"] = Symbol.Contact2,
        ["building"] = Symbol.Home,
        ["bank"] = Symbol.Library,
        ["card"] = Symbol.Permissions,
        ["dollar"] = Symbol.Calculator,
        ["receipt"] = Symbol.Document,
        ["file"] = Symbol.Page,
        ["file-check"] = Symbol.Document,
        ["clipboard"] = Symbol.List,
        ["layers"] = Symbol.AllApps,
        ["folder"] = Symbol.Folder,
        ["save"] = Symbol.Save,
        ["edit"] = Symbol.Edit,
        ["trash"] = Symbol.Delete,
        ["plus"] = Symbol.Add,
        ["check"] = Symbol.Accept,
        ["check-circle"] = Symbol.Accept,
        ["x"] = Symbol.Cancel,
        ["eraser"] = Symbol.Clear,
        ["eye"] = Symbol.View,
        ["search"] = Symbol.Find,
        // E946 es el glifo "Info" de Segoe Fluent / MDL2: más adecuado que el
        // interrogante de ayuda para una nota informativa.
        ["info"] = (Symbol)0xE946,
        ["mail"] = Symbol.Mail,
        ["phone"] = Symbol.Phone,
        ["pin"] = Symbol.MapPin,
        ["map"] = Symbol.Map,
        ["calendar"] = Symbol.Calendar,
        ["clock"] = Symbol.Clock,
        ["text"] = Symbol.Font,
        // E70E / E70D son los chevrones arriba y abajo de la fuente de iconos.
        ["chevron-up"] = (Symbol)0xE70E,
        ["chevron-down"] = (Symbol)0xE70D,
        ["refresh"] = Symbol.Refresh,
        ["settings"] = Symbol.Setting,
    };

    private FontIcon? _glifo;

    public Icono()
    {
        DefaultStyleKey = typeof(Icono);
        IsTabStop = false;
        RegisterPropertyChangedCallback(ForegroundProperty, (_, _) => AplicarPincel());
    }

    /// <summary>Nombre del icono (por ejemplo "user").</summary>
    public string Nombre
    {
        get => (string)GetValue(NombreProperty);
        set => SetValue(NombreProperty, value);
    }

    public static readonly DependencyProperty NombreProperty =
        DependencyProperty.Register(
            nameof(Nombre), typeof(string), typeof(Icono),
            new PropertyMetadata(string.Empty, (d, _) => ((Icono)d).AplicarGlifo()));

    /// <summary>Tamano del icono en pixeles logicos.</summary>
    public double Tamano
    {
        get => (double)GetValue(TamanoProperty);
        set => SetValue(TamanoProperty, value);
    }

    public static readonly DependencyProperty TamanoProperty =
        DependencyProperty.Register(
            nameof(Tamano), typeof(double), typeof(Icono),
            new PropertyMetadata(16d));

    /// <summary>
    /// Se conserva por compatibilidad con el XAML existente. La fuente de
    /// iconos no usa trazo, asi que ya no tiene efecto.
    /// </summary>
    public double Grosor
    {
        get => (double)GetValue(GrosorProperty);
        set => SetValue(GrosorProperty, value);
    }

    public static readonly DependencyProperty GrosorProperty =
        DependencyProperty.Register(
            nameof(Grosor), typeof(double), typeof(Icono),
            new PropertyMetadata(2.0d));

    /// <summary>Color del icono. Si es null hereda el Foreground del contenedor.</summary>
    public Brush? Trazo
    {
        get => (Brush?)GetValue(TrazoProperty);
        set => SetValue(TrazoProperty, value);
    }

    public static readonly DependencyProperty TrazoProperty =
        DependencyProperty.Register(
            nameof(Trazo), typeof(Brush), typeof(Icono),
            new PropertyMetadata(null, (d, _) => ((Icono)d).AplicarPincel()));

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _glifo = GetTemplateChild("PartGlifo") as FontIcon;
        AplicarGlifo();
        AplicarPincel();
    }

    private void AplicarGlifo()
    {
        if (_glifo is null)
        {
            return;
        }

        // Los valores del enumerado Symbol son los puntos de codigo de la
        // fuente de iconos, asi que se convierten directamente a caracter.
        var simbolo = Equivalencias.TryGetValue(Nombre ?? string.Empty, out var s)
            ? s
            : Symbol.Page;

        _glifo.Glyph = ((char)simbolo).ToString();
    }

    private void AplicarPincel()
    {
        if (_glifo is not null)
        {
            _glifo.Foreground = Trazo ?? Foreground;
        }
    }
}
