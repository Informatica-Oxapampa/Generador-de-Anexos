using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using GeneradorAnexos.WinUI.Services;

namespace GeneradorAnexos.WinUI.Controls;

/// <summary>
/// Marca brevemente un campo que acaba de recibir un valor por sincronización
/// (TDR → Anexos) y no por escritura del usuario.
/// </summary>
/// <remarks>
/// Al terminar el destello, cada propiedad vuelve exactamente al estado que
/// tenía: si el control no tenía un valor local, se llama a
/// <see cref="DependencyObject.ClearValue"/> en lugar de reasignar el pincel.
/// Esto es importante porque reasignarlo dejaría un valor local permanente y
/// el control perdería sus estados nativos de puntero, foco y deshabilitado.
/// Los colores provienen del tema, así que el destello se ve bien en claro,
/// en oscuro y en alto contraste.
/// </remarks>
public static class EfectoDestello
{
    private const int DuracionMs = 650;

    public static void Aplicar(Control control)
    {
        if (control is null)
        {
            return;
        }

        var fondoLocal = control.ReadLocalValue(Control.BackgroundProperty);
        var bordeLocal = control.ReadLocalValue(Control.BorderBrushProperty);

        control.Background = Paleta.Pincel("Ga.SyncFondo");
        control.BorderBrush = Paleta.Pincel("Ga.SyncBorde");

        var temporizador = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(DuracionMs),
        };

        temporizador.Tick += (_, _) =>
        {
            temporizador.Stop();
            Restaurar(control, Control.BackgroundProperty, fondoLocal);
            Restaurar(control, Control.BorderBrushProperty, bordeLocal);
        };

        temporizador.Start();
    }

    private static void Restaurar(
        DependencyObject control,
        DependencyProperty propiedad,
        object? valorLocalPrevio)
    {
        if (valorLocalPrevio is null || valorLocalPrevio == DependencyProperty.UnsetValue)
        {
            control.ClearValue(propiedad);
            return;
        }

        control.SetValue(propiedad, valorLocalPrevio);
    }
}
