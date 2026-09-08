using System.Threading.Tasks;
using GeneradorAnexos.WinUI.Views;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace GeneradorAnexos.WinUI.Services;

/// <summary>
/// Equivalente de <c>QFileDialog</c>. En WinUI los selectores necesitan el
/// identificador de ventana, que se obtiene con <see cref="WindowNative"/>.
/// </summary>
public static class SelectorArchivos
{
    /// <summary>Dialogo «Guardar como». Devuelve la ruta o <c>null</c> si se cancela.</summary>
    public static async Task<string?> GuardarComoAsync(
        VentanaPrincipal ventana,
        string titulo,
        string nombreSugerido,
        string descripcionTipo,
        string extension,
        string carpetaInicial)
    {
        var selector = new FileSavePicker
        {
            SuggestedStartLocation = UbicacionSugerida(carpetaInicial),
            SuggestedFileName = System.IO.Path.GetFileNameWithoutExtension(nombreSugerido),
            CommitButtonText = titulo,
        };

        selector.FileTypeChoices.Add(descripcionTipo, new[] { extension });
        InitializeWithWindow.Initialize(selector, WindowNative.GetWindowHandle(ventana));

        var archivo = await selector.PickSaveFileAsync();
        return archivo?.Path;
    }

    private static PickerLocationId UbicacionSugerida(string? carpeta)
    {
        if (string.IsNullOrWhiteSpace(carpeta))
        {
            return PickerLocationId.DocumentsLibrary;
        }

        var completa = System.IO.Path.GetFullPath(carpeta)
            .TrimEnd(System.IO.Path.DirectorySeparatorChar);

        bool Es(System.Environment.SpecialFolder especial)
            => string.Equals(
                completa,
                System.Environment.GetFolderPath(especial)
                    .TrimEnd(System.IO.Path.DirectorySeparatorChar),
                System.StringComparison.OrdinalIgnoreCase);

        if (Es(System.Environment.SpecialFolder.DesktopDirectory))
        {
            return PickerLocationId.Desktop;
        }

        if (Es(System.Environment.SpecialFolder.MyPictures))
        {
            return PickerLocationId.PicturesLibrary;
        }

        return PickerLocationId.DocumentsLibrary;
    }

    /// <summary>Dialogo «Abrir». Devuelve la ruta o <c>null</c> si se cancela.</summary>
    public static async Task<string?> AbrirAsync(
        VentanaPrincipal ventana, string extension)
    {
        var selector = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List,
        };

        selector.FileTypeFilter.Add(extension);
        InitializeWithWindow.Initialize(selector, WindowNative.GetWindowHandle(ventana));

        var archivo = await selector.PickSingleFileAsync();
        return archivo?.Path;
    }
}
