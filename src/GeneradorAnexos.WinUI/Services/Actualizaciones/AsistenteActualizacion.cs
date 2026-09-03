using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeneradorAnexos.WinUI.Services.Actualizaciones;

/// <summary>
/// Diálogos de actualización: aviso de versión disponible, progreso de descarga
/// e instalación, tanto del programa como de las plantillas.
/// </summary>
/// <remarks>
/// Cancelar es siempre seguro: la descarga se detiene, el archivo parcial se
/// borra y no se ha tocado nada de lo instalado, porque nada se ejecuta ni se
/// extrae hasta después de verificar el hash.
/// </remarks>
public static class AsistenteActualizacion
{
    /// <summary>
    /// Ofrece las actualizaciones pendientes. Si hay de programa y de
    /// plantillas a la vez, primero se ofrece la del programa: el instalador
    /// suele traer ya las plantillas nuevas.
    /// </summary>
    public static async Task OfrecerAsync(ResultadoComprobacion resultado)
    {
        var raiz = App.Ventana?.Content?.XamlRoot;
        if (raiz is null || resultado.Estado != EstadoComprobacion.Correcta)
        {
            return;
        }

        var preferencias = new PreferenciasUi();

        if (resultado.AppPendiente(out var versionApp) is { } app)
        {
            if (!app.Obligatoria
                && string.Equals(preferencias.VersionOmitida, versionApp.ToString(), StringComparison.Ordinal))
            {
                Registro.Info("UPDATE_SKIPPED_BY_USER");
            }
            else
            {
                await OfrecerAppAsync(app, versionApp, raiz, preferencias);
                return;
            }
        }

        var plantillas = resultado.PlantillasPendientes(
            ServicioPlantillas.VersionInstalada(), out var versionPlantillas);

        if (plantillas is not null)
        {
            await OfrecerPlantillasAsync(plantillas, versionPlantillas, raiz);
        }
    }

    // ═══════════════════════ Programa ═══════════════════════

    private static async Task OfrecerAppAsync(
        PaqueteActualizacion app,
        VersionSemantica version,
        XamlRoot raiz,
        PreferenciasUi preferencias)
    {
        var aviso = new ContentDialog
        {
            XamlRoot = raiz,
            RequestedTheme = ServicioTema.TemaEfectivo,
            Title = "Actualización disponible",
            Content = Detalle(
                "Versión instalada", Constantes.AppVersion,
                "Nueva versión", app.Version,
                app),
            PrimaryButtonText = "Actualizar ahora",
            SecondaryButtonText = app.Obligatoria ? string.Empty : "Omitir esta versión",
            CloseButtonText = "Más tarde",
            DefaultButton = ContentDialogButton.Primary,
        };

        var eleccion = await aviso.ShowAsync();

        if (eleccion == ContentDialogResult.Secondary)
        {
            preferencias.VersionOmitida = version.ToString();
            Registro.Info("UPDATE_SKIPPED");
            return;
        }

        if (eleccion != ContentDialogResult.Primary)
        {
            Registro.Info("UPDATE_POSTPONED");
            return;
        }

        var instalador = await DescargarAsync(
            app, $"GeneradorAnexos-{version}-Setup.exe", "Actualizando aplicación", raiz);

        if (instalador is null)
        {
            return;
        }

        if (!await ServicioDialogos.PreguntarSiNoAsync(
                "Instalar la actualización",
                "La actualización se descargó y se verificó correctamente."
                + Environment.NewLine + Environment.NewLine
                + "La aplicación se cerrará para instalarla y volverá a abrirse al terminar. "
                + "Guarde su trabajo antes de continuar." + Environment.NewLine + Environment.NewLine
                + "¿Desea instalarla ahora?",
                defectoSi: true))
        {
            return;
        }

        var resultado = await ServicioActualizaciones.InstalarAsync(
            instalador, app, CancellationToken.None);

        switch (resultado)
        {
            case ResultadoInstalacion.Lanzado:
                Microsoft.UI.Xaml.Application.Current.Exit();
                return;

            case ResultadoInstalacion.ElevacionRechazada:
                await ServicioDialogos.MostrarInformacionAsync(
                    "Actualización cancelada",
                    "La instalación necesita permisos de administrador y no se "
                    + "autorizaron." + Environment.NewLine + Environment.NewLine
                    + "El programa se instala en Archivos de programa, una carpeta "
                    + "protegida por Windows, así que la actualización requiere esa "
                    + "confirmación. Puede volver a intentarlo desde Configuración.");
                return;

            case ResultadoInstalacion.Manipulado:
                await ServicioDialogos.MostrarErrorAsync(
                    "Actualización descartada",
                    "El instalador descargado cambió después de comprobarse y se "
                    + "ha eliminado por seguridad." + Environment.NewLine + Environment.NewLine
                    + "No se instaló nada. Vuelva a intentarlo; si se repite, "
                    + "descargue la versión a mano desde la página oficial.");
                return;

            default:
                await ServicioDialogos.MostrarErrorAsync(
                    "No se pudo iniciar la instalación",
                    "El instalador se descargó correctamente pero no pudo ejecutarse."
                    + Environment.NewLine + Environment.NewLine
                    + "Puede instalarlo a mano desde la carpeta de actualizaciones, "
                    + "accesible en Configuración › Datos y diagnóstico.");
                return;
        }
    }

    // ═══════════════════════ Plantillas ═══════════════════════

    private static async Task OfrecerPlantillasAsync(
        PaqueteActualizacion paquete,
        VersionSemantica version,
        XamlRoot raiz)
    {
        var aviso = new ContentDialog
        {
            XamlRoot = raiz,
            RequestedTheme = ServicioTema.TemaEfectivo,
            Title = "Plantillas actualizadas disponibles",
            Content = Detalle(
                "Plantillas instaladas", ServicioPlantillas.VersionInstalada().ToString(),
                "Nuevas plantillas", paquete.Version,
                paquete),
            PrimaryButtonText = "Actualizar plantillas",
            CloseButtonText = "Más tarde",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await aviso.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var descargado = await DescargarAsync(
            paquete, $"plantillas-{version}.zip", "Actualizando plantillas", raiz);

        if (descargado is null)
        {
            return;
        }

        var instaladas = await ServicioPlantillas.InstalarAsync(
            descargado, version, CancellationToken.None);

        if (instaladas)
        {
            await ServicioDialogos.MostrarInformacionAsync(
                "Plantillas actualizadas",
                $"Las plantillas se actualizaron a la versión {version}."
                + Environment.NewLine + Environment.NewLine
                + "Los documentos que genere a partir de ahora usarán las nuevas. "
                + "No hace falta reiniciar el programa.");
            return;
        }

        await ServicioDialogos.MostrarErrorAsync(
            "No se pudieron actualizar las plantillas",
            "El paquete se descargó pero no pudo instalarse."
            + Environment.NewLine + Environment.NewLine
            + "Se conservan las plantillas anteriores, así que puede seguir "
            + "generando documentos con normalidad.");
    }

    // ═══════════════════════ Descarga con progreso ═══════════════════════

    private static async Task<string?> DescargarAsync(
        PaqueteActualizacion paquete,
        string nombreArchivo,
        string titulo,
        XamlRoot raiz)
    {
        using var cancelacion = new CancellationTokenSource();

        var etiqueta = new TextBlock
        {
            Text = "Preparando actualización…",
            TextWrapping = TextWrapping.Wrap,
        };

        var barra = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            IsIndeterminate = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var contenido = new StackPanel { Spacing = 14, MinWidth = 360 };
        contenido.Children.Add(etiqueta);
        contenido.Children.Add(barra);
        contenido.Children.Add(new TextBlock
        {
            Text = "Puede cancelar en cualquier momento. Nada de lo instalado se "
                   + "modifica hasta que la descarga se verifica por completo.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
            FontSize = 12,
        });

        var dialogo = new ContentDialog
        {
            XamlRoot = raiz,
            RequestedTheme = ServicioTema.TemaEfectivo,
            Title = titulo,
            Content = contenido,
            CloseButtonText = "Cancelar",
        };

        dialogo.CloseButtonClick += (_, _) => cancelacion.Cancel();

        var progreso = new Progress<EstadoDescarga>(estado =>
        {
            etiqueta.Text = estado.Texto;
            barra.IsIndeterminate = estado.Indeterminado;
            barra.Value = estado.Fraccion;
        });

        var mostrando = dialogo.ShowAsync();

        string? archivo;
        try
        {
            archivo = await ServicioActualizaciones.DescargarVerificadoAsync(
                paquete, nombreArchivo, progreso, cancelacion.Token);
        }
        finally
        {
            // WinUI no admite dos ContentDialog a la vez y justo después se
            // muestra el aviso de resultado, así que se espera al cierre.
            dialogo.Hide();
            try
            {
                _ = await mostrando;
            }
            catch (Exception excepcion)
            {
                Registro.Advertencia("UPDATE_DIALOG_CLOSE_" + excepcion.GetType().Name);
            }
        }

        if (cancelacion.IsCancellationRequested)
        {
            return null;
        }

        if (archivo is null)
        {
            await ServicioDialogos.MostrarErrorAsync(
                "No se pudo actualizar",
                "La descarga falló o el archivo recibido no superó la comprobación "
                + "de integridad." + Environment.NewLine + Environment.NewLine
                + "No se modificó nada y puede seguir trabajando con normalidad. "
                + "Vuelva a intentarlo más tarde desde Configuración.");
        }

        return archivo;
    }

    // ═══════════════════════ Presentación ═══════════════════════

    private static StackPanel Detalle(
        string etiquetaActual,
        string valorActual,
        string etiquetaNueva,
        string valorNuevo,
        PaqueteActualizacion paquete)
    {
        var panel = new StackPanel { Spacing = 12, MinWidth = 380 };

        panel.Children.Add(Linea(etiquetaActual, valorActual));
        panel.Children.Add(Linea(etiquetaNueva, valorNuevo));

        var fecha = paquete.FechaLegible();
        if (!string.IsNullOrWhiteSpace(fecha))
        {
            panel.Children.Add(Linea("Fecha de publicación", fecha));
        }

        var tamano = paquete.TamanoLegible();
        if (!string.IsNullOrWhiteSpace(tamano))
        {
            panel.Children.Add(Linea("Tamaño de la descarga", tamano));
        }

        if (paquete.Notas.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Cambios principales",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 6, 0, 0),
            });

            var lista = new StackPanel { Spacing = 4 };
            foreach (var nota in paquete.Notas)
            {
                lista.Children.Add(new TextBlock
                {
                    Text = "•  " + nota,
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            panel.Children.Add(lista);
        }

        return panel;
    }

    private static Grid Linea(string etiqueta, string valor)
    {
        var fila = new Grid { ColumnSpacing = 12 };
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var derecha = new TextBlock
        {
            Text = valor,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };

        Grid.SetColumn(derecha, 1);
        fila.Children.Add(new TextBlock { Text = etiqueta, Opacity = 0.75 });
        fila.Children.Add(derecha);
        return fila;
    }
}
