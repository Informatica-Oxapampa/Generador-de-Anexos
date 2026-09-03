using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace GeneradorAnexos.WinUI.Converters;

/// <summary>Oculta un elemento cuando su texto esta vacio.</summary>
/// <remarks>
/// El original conseguia lo mismo con <c>setVisible(bool(texto))</c> en
/// CampoFormulario y en las descripciones de las tarjetas.
/// </remarks>
public sealed partial class TextoAVisibilidad : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

public sealed partial class BooleanoAVisibilidad : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Visible;
}

public sealed partial class BooleanoAVisibilidadInversa : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Collapsed;
}
