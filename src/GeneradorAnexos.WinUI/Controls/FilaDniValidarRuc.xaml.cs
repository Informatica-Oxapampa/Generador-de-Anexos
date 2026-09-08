using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeneradorAnexos.WinUI.Controls;

/// <summary>Equivalente de <c>ui/widgets.py: FilaDniValidarRuc</c>.</summary>
public sealed partial class FilaDniValidarRuc : UserControl
{
    /// <summary>ui/widgets.py: ANCHO_REFLOW_DNI_RUC.</summary>
    private const double AnchoReflujo = 470;

    private bool? _modoEstrecho;

    public FilaDniValidarRuc() => InitializeComponent();

    /// <summary>Pulsacion del boton «Validar» (consulta de DNI).</summary>
    public event EventHandler? Validar;

    public CampoTexto Dni => CampoDni;

    public CampoTexto Ruc => CampoRuc;

    /// <summary>Equivalente de <c>set_consultando</c>.</summary>
    public void EstablecerConsultando(bool activo)
    {
        BotonValidar.Content = activo ? "Validando" : "Validar";
        BotonValidar.IsEnabled = !activo;
        CampoDni.Enfocar();
    }

    private void AlPulsarValidar(object sender, RoutedEventArgs e)
        => Validar?.Invoke(this, EventArgs.Empty);

    private void AlCambiarTamano(object sender, SizeChangedEventArgs e)
        => Reorganizar(e.NewSize.Width < AnchoReflujo);

    /// <summary>
    /// En estrecho el RUC baja a una segunda fila a ancho completo, igual que
    /// el reflujo del original.
    /// </summary>
    private void Reorganizar(bool estrecho)
    {
        if (_modoEstrecho == estrecho)
        {
            return;
        }

        _modoEstrecho = estrecho;

        if (estrecho)
        {
            Grid.SetRow(CampoRuc, 1);
            Grid.SetColumn(CampoRuc, 0);
            Grid.SetColumnSpan(CampoRuc, 2);
            ColumnaRuc.Width = new GridLength(0);
            return;
        }

        Grid.SetRow(CampoRuc, 0);
        Grid.SetColumn(CampoRuc, 2);
        Grid.SetColumnSpan(CampoRuc, 1);
        ColumnaRuc.Width = new GridLength(1, GridUnitType.Star);
    }
}
