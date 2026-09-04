using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeneradorAnexos.WinUI.Services;

/// <summary>
/// Equivalente de los <c>QMessageBox</c> del original y de
/// <c>ui/widgets.py: preguntar_si_no</c>.
/// </summary>
/// <remarks>
/// Los <see cref="ContentDialog"/> se alojan fuera del arbol de la ventana, asi
/// que no heredan el tema de la raiz. Cada dialogo recibe explicitamente
/// <see cref="ServicioTema.TemaEfectivo"/>; sin eso, al fijar el tema claro u
/// oscuro desde Configuracion los cuadros de dialogo se quedaban con el tema
/// contrario.
/// </remarks>
public static class ServicioDialogos
{
    private static XamlRoot? RaizActiva => App.Ventana?.Content?.XamlRoot;

    public static Task MostrarInformacionAsync(string titulo, string mensaje)
        => MostrarAsync(titulo, mensaje, "Aceptar");

    public static Task MostrarAdvertenciaAsync(string titulo, string mensaje)
        => MostrarAsync(titulo, mensaje, "Aceptar");

    public static Task MostrarErrorAsync(string titulo, string mensaje)
        => MostrarAsync(titulo, mensaje, "Cerrar");

    /// <summary>Equivalente de <c>preguntar_si_no</c>. Devuelve true si el usuario acepta.</summary>
    public static async Task<bool> PreguntarSiNoAsync(
        string titulo, string mensaje, bool defectoSi = false)
    {
        var raiz = RaizActiva;
        if (raiz is null)
        {
            return false;
        }

        var dialogo = new ContentDialog
        {
            XamlRoot = raiz,
            RequestedTheme = ServicioTema.TemaEfectivo,
            Title = titulo,
            Content = new TextBlock { Text = mensaje, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Sí",
            CloseButtonText = "No",
            DefaultButton = defectoSi ? ContentDialogButton.Primary : ContentDialogButton.Close,
        };

        return await dialogo.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Dialogo de exito con tres acciones, igual que <c>_exito()</c> del
    /// original: abrir documento, abrir carpeta o cerrar.
    /// </summary>
    public static async Task<ResultadoExito> MostrarExitoAsync(string ruta)
    {
        var raiz = RaizActiva;
        if (raiz is null)
        {
            return ResultadoExito.Cerrar;
        }

        var contenido = new StackPanel { Spacing = 6, MaxWidth = 460 };
        contenido.Children.Add(new TextBlock
        {
            Text = "El documento se generó correctamente.",
            TextWrapping = TextWrapping.Wrap,
        });
        contenido.Children.Add(new TextBlock
        {
            Text = ruta,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
        });

        // Un ContentDialog admite tres botones, y aquí hay cuatro acciones
        // posibles. Imprimir y Abrir son las dos que el usuario necesita justo
        // después de generar, así que ocupan los botones principales; «Abrir
        // carpeta» pasa a ser un enlace dentro del contenido, que es donde no
        // estorba pero sigue estando a un clic.
        var carpeta = new HyperlinkButton
        {
            Content = "Abrir la carpeta que lo contiene",
            Padding = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        contenido.Children.Add(carpeta);

        var dialogo = new ContentDialog
        {
            XamlRoot = raiz,
            RequestedTheme = ServicioTema.TemaEfectivo,
            Title = "Documento generado",
            Content = contenido,
            PrimaryButtonText = "Imprimir",
            SecondaryButtonText = "Abrir documento",
            CloseButtonText = "Cerrar",
            DefaultButton = ContentDialogButton.Secondary,
        };

        var abrirCarpeta = false;
        carpeta.Click += (_, _) =>
        {
            abrirCarpeta = true;
            dialogo.Hide();
        };

        var eleccion = await dialogo.ShowAsync();

        if (abrirCarpeta)
        {
            return ResultadoExito.AbrirCarpeta;
        }

        return eleccion switch
        {
            ContentDialogResult.Primary => ResultadoExito.Imprimir,
            ContentDialogResult.Secondary => ResultadoExito.AbrirDocumento,
            _ => ResultadoExito.Cerrar,
        };
    }

    /// <summary>
    /// Muestra las impresoras instaladas y devuelve la que elija el usuario.
    /// </summary>
    /// <remarks>
    /// Devuelve <c>null</c> si cancela. Preselecciona la última que usó y, si
    /// no hay ninguna recordada, la predeterminada de Windows: en una oficina
    /// se imprime casi siempre en la misma bandeja, así que lo normal debe ser
    /// pulsar Imprimir sin tocar nada.
    /// </remarks>
    public static async Task<string?> PedirImpresoraAsync(
        IReadOnlyList<string> impresoras,
        string seleccionInicial)
    {
        var raiz = RaizActiva;
        if (raiz is null || impresoras.Count == 0)
        {
            return null;
        }

        var lista = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 380,
        };

        foreach (var impresora in impresoras)
        {
            lista.Items.Add(impresora);
        }

        // IReadOnlyList no expone IndexOf; se busca a mano y, si la impresora
        // recordada ya no existe, se deja seleccionada la primera.
        var indice = 0;
        for (var i = 0; i < impresoras.Count; i++)
        {
            if (string.Equals(impresoras[i], seleccionInicial, StringComparison.OrdinalIgnoreCase))
            {
                indice = i;
                break;
            }
        }

        lista.SelectedIndex = indice;

        var contenido = new StackPanel { Spacing = 10, MinWidth = 380 };
        contenido.Children.Add(new TextBlock
        {
            Text = "Seleccione la impresora:",
            TextWrapping = TextWrapping.Wrap,
        });
        contenido.Children.Add(lista);
        contenido.Children.Add(new TextBlock
        {
            Text = "El documento se enviará con el formato exacto de la plantilla. "
                   + "Word puede pedirle confirmación antes de imprimir.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.75,
        });

        var dialogo = new ContentDialog
        {
            XamlRoot = raiz,
            RequestedTheme = ServicioTema.TemaEfectivo,
            Title = "Imprimir documento",
            Content = contenido,
            PrimaryButtonText = "Imprimir",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
        };

        return await dialogo.ShowAsync() == ContentDialogResult.Primary
            ? lista.SelectedItem as string
            : null;
    }

    /// <summary>
    /// Pregunta qué hacer cuando una acción va a descartar cambios sin guardar.
    /// </summary>
    /// <remarks>
    /// La usan tanto el cierre de la ventana como «Nuevo registro»: en ambos
    /// casos el usuario está a punto de perder trabajo y merece las mismas tres
    /// salidas, no un simple sí/no que solo permite perderlo o quedarse.
    ///
    /// «Cancelar» es el botón predeterminado a propósito: si la acción se
    /// pulsó por error, Escape e Intro devuelven al trabajo sin perder nada.
    /// </remarks>
    public static async Task<RespuestaCambios> PreguntarCambiosPendientesAsync(
        string nombreRegistro,
        string accion,
        string textoGuardar,
        string textoDescartar)
    {
        var raiz = RaizActiva;
        if (raiz is null)
        {
            return RespuestaCambios.Descartar;
        }

        var detalle = string.IsNullOrWhiteSpace(nombreRegistro)
            ? "El formulario tiene datos que todavía no se han guardado como registro."
            : $"El registro «{nombreRegistro}» tiene cambios sin guardar.";

        var dialogo = new ContentDialog
        {
            XamlRoot = raiz,
            RequestedTheme = ServicioTema.TemaEfectivo,
            Title = "Hay cambios sin guardar",
            Content = new TextBlock
            {
                Text = detalle + Environment.NewLine + Environment.NewLine
                       + $"¿Desea guardarlos antes de {accion}?",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420,
            },
            PrimaryButtonText = textoGuardar,
            SecondaryButtonText = textoDescartar,
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
        };

        return await dialogo.ShowAsync() switch
        {
            ContentDialogResult.Primary => RespuestaCambios.Guardar,
            ContentDialogResult.Secondary => RespuestaCambios.Descartar,
            _ => RespuestaCambios.Cancelar,
        };
    }

    /// <summary>Pide un nombre (guardar o renombrar un registro).</summary>
    public static async Task<string?> PedirTextoAsync(
        string titulo, string etiqueta, string valorInicial)
    {
        var raiz = RaizActiva;
        if (raiz is null)
        {
            return null;
        }

        var caja = new TextBox
        {
            Text = valorInicial,
            SelectionStart = 0,
            SelectionLength = valorInicial.Length,
            MaxLength = 120,
        };

        var contenido = new StackPanel { Spacing = 8 };
        contenido.Children.Add(new TextBlock { Text = etiqueta, TextWrapping = TextWrapping.Wrap });
        contenido.Children.Add(caja);

        var dialogo = new ContentDialog
        {
            XamlRoot = raiz,
            RequestedTheme = ServicioTema.TemaEfectivo,
            Title = titulo,
            Content = contenido,
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
        };

        return await dialogo.ShowAsync() == ContentDialogResult.Primary
            ? caja.Text.Trim()
            : null;
    }

    private static async Task MostrarAsync(string titulo, string mensaje, string boton)
    {
        var raiz = RaizActiva;
        if (raiz is null)
        {
            return;
        }

        await new ContentDialog
        {
            XamlRoot = raiz,
            RequestedTheme = ServicioTema.TemaEfectivo,
            Title = titulo,
            Content = new TextBlock { Text = mensaje, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = boton,
        }.ShowAsync();
    }
}

/// <summary>Respuesta del usuario ante cambios sin guardar.</summary>
public enum RespuestaCambios
{
    /// <summary>Guardar y después continuar con la acción.</summary>
    Guardar,

    /// <summary>Continuar descartando los cambios.</summary>
    Descartar,

    /// <summary>No hacer nada y seguir trabajando.</summary>
    Cancelar,
}

/// <summary>Accion elegida en el dialogo de exito.</summary>
public enum ResultadoExito
{
    Cerrar,
    AbrirDocumento,
    AbrirCarpeta,

    /// <summary>Enviar el documento a la impresora.</summary>
    Imprimir,
}
