using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeneradorAnexos.WinUI.Controls;

/// <summary>Equivalente de <c>ui/tab_tdr.py: SelectorModo</c>.</summary>
public sealed partial class SelectorModo : UserControl
{
    /// <summary>Codigos compatibles con el JSON del original.</summary>
    public const string ModoUnico = "unico";

    public const string ModoMultiple = "multiple";

    private string _modo = ModoUnico;

    public SelectorModo() => InitializeComponent();

    /// <summary>Se emite con "unico" o "multiple", como la senal del original.</summary>
    public event EventHandler<string>? Cambiado;

    public string Modo => _modo;

    /// <summary>Equivalente de <c>set_modo</c>; emite siempre, como el original.</summary>
    public void EstablecerModo(string? modo)
    {
        _modo = modo == ModoMultiple ? ModoMultiple : ModoUnico;
        BotonUnico.IsChecked = _modo == ModoUnico;
        BotonMultiple.IsChecked = _modo == ModoMultiple;
        Cambiado?.Invoke(this, _modo);
    }

    private void AlElegirUnico(object sender, RoutedEventArgs e) => EstablecerModo(ModoUnico);

    private void AlElegirMultiple(object sender, RoutedEventArgs e) => EstablecerModo(ModoMultiple);
}
