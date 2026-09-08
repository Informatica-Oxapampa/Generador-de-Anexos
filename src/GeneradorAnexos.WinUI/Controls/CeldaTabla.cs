using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeneradorAnexos.WinUI.Controls;

/// <summary>
/// Fábricas de celdas de las tablas estilo Word.
/// </summary>
/// <remarks>
/// <b>Todas las celdas se pintan aplicando un estilo, nunca un pincel.</b>
///
/// Un pincel obtenido desde código es una fotografía del tema que estaba activo
/// en ese instante: no cambia al alternar entre claro y oscuro, y si la tabla se
/// construye antes de que se aplique el tema guardado, se queda con los colores
/// equivocados para siempre. Era la causa de que las celdas aparecieran blancas
/// con la aplicación en modo oscuro.
///
/// Un estilo cuyos setters usan ThemeResource sí se reevalúa cuando cambia el
/// tema, así que las celdas siguen a la interfaz sin ningún código adicional.
///
/// La rejilla se dibuja igual que el original: el marco tiene el color de las
/// líneas como fondo y las celdas dejan 1 px de separación entre sí.
/// </remarks>
public static class CeldaTabla
{
    /// <summary>Alto mínimo de una celda editable.</summary>
    public const double AltoCelda = 76;

    /// <summary>Celda de cabecera.</summary>
    public static Border Cabecera(string texto) => new()
    {
        Style = Estilo("Ga.CeldaCabeceraFondo"),
        Child = new TextBlock
        {
            Text = texto,
            Style = Estilo("Ga.CeldaCabecera"),
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    /// <summary>Celda editable multilínea.</summary>
    public static TextBox Editor(string texto = "", string marcador = "", double alto = AltoCelda) => new()
    {
        Text = texto,
        PlaceholderText = marcador,
        Style = Estilo("Ga.CeldaEditor"),

        // Alto mínimo, nunca fijo: la celda crece si el usuario tiene
        // configurado un tamaño de texto mayor en Windows.
        MinHeight = alto,
    };

    /// <summary>Entrada compacta centrada dentro de una celda.</summary>
    public static TextBox Campo(string texto) => new()
    {
        Text = texto,
        Style = Estilo("Ga.CeldaCampo"),
        MinHeight = 34,
    };

    /// <summary>Envoltorio de una celda no editable.</summary>
    public static Border Envolver(UIElement contenido) => new()
    {
        Style = Estilo("Ga.CeldaFondo"),
        Child = contenido,
    };

    /// <summary>Etiqueta de una celda no editable.</summary>
    public static TextBlock Etiqueta(string texto = "") => new()
    {
        Text = texto,
        Style = Estilo("Ga.CeldaEtiqueta"),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    /// <summary>
    /// Resalta la celda cuando su contenido no es válido.
    /// </summary>
    /// <remarks>
    /// Se intercambia el estilo completo en lugar de asignar pinceles sueltos,
    /// por el mismo motivo que en el resto de la clase: así el resalte también
    /// sigue al tema activo.
    /// </remarks>
    public static void Marcar(Control celda, bool invalido)
    {
        var esCampo = celda is TextBox { AcceptsReturn: false };

        celda.Style = (esCampo, invalido) switch
        {
            (true, true) => Estilo("Ga.CeldaCampoError"),
            (true, false) => Estilo("Ga.CeldaCampo"),
            (false, true) => Estilo("Ga.CeldaEditorError"),
            _ => Estilo("Ga.CeldaEditor"),
        };
    }

    /// <summary>Marco de la tabla: su fondo hace de rejilla.</summary>
    public static Border Marco(Grid rejilla) => new()
    {
        Style = Estilo("Ga.MarcoTabla"),
        Child = rejilla,
    };

    private static Style Estilo(string clave)
        => (Style)Microsoft.UI.Xaml.Application.Current.Resources[clave];
}
