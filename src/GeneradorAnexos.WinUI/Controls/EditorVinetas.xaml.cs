using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeneradorAnexos.WinUI.Controls;

/// <summary>Fila de una lista editable (viñeta o requisito adicional).</summary>
public sealed partial class FilaLista : System.ComponentModel.INotifyPropertyChanged
{
    private string _texto = string.Empty;
    private bool _puedeSubir = true;
    private bool _puedeBajar = true;
    private bool _mostrarReordenar = true;
    private string _marcador = string.Empty;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public string Texto
    {
        get => _texto;
        set => Establecer(ref _texto, value);
    }

    /// <summary>Texto de ayuda del campo; lo fija el editor al crear la fila.</summary>
    public string Marcador
    {
        get => _marcador;
        set => Establecer(ref _marcador, value);
    }

    public bool PuedeSubir
    {
        get => _puedeSubir;
        set => Establecer(ref _puedeSubir, value);
    }

    public bool PuedeBajar
    {
        get => _puedeBajar;
        set => Establecer(ref _puedeBajar, value);
    }

    /// <summary>
    /// Indica si la fila muestra los botones de subir y bajar. Se refleja en
    /// cada fila (y no solo en el editor) para que la plantilla de datos pueda
    /// enlazarlo directamente con x:Bind.
    /// </summary>
    public bool MostrarReordenar
    {
        get => _mostrarReordenar;
        set => Establecer(ref _mostrarReordenar, value);
    }

    private void Establecer<T>(ref T campo, T valor, [System.Runtime.CompilerServices.CallerMemberName] string? nombre = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor))
        {
            return;
        }

        campo = valor;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nombre));
    }
}

/// <summary>
/// Equivalente de <c>ui/tab_tdr.py: EditorVinetas</c> y, con
/// <see cref="MostrarReordenar"/> en <c>false</c>, de <c>ListaRequisitos</c>.
/// </summary>
/// <remarks>
/// Cada elemento se convierte en una viñeta del documento Word. Permite
/// agregar, editar, reordenar y eliminar, igual que el original.
/// </remarks>
public sealed partial class EditorVinetas : UserControl
{
    public EditorVinetas()
    {
        InitializeComponent();
        Filas.CollectionChanged += (_, _) => Actualizar();
        Actualizar();
    }

    /// <summary>Elementos actuales de la lista.</summary>
    public ObservableCollection<FilaLista> Filas { get; } = new();

    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(EditorVinetas),
            new PropertyMetadata(string.Empty));

    public string Marcador
    {
        get => (string)GetValue(MarcadorProperty);
        set => SetValue(MarcadorProperty, value);
    }

    public static readonly DependencyProperty MarcadorProperty =
        DependencyProperty.Register(nameof(Marcador), typeof(string), typeof(EditorVinetas),
            new PropertyMetadata("Escriba un elemento…"));

    public string TextoBoton
    {
        get => (string)GetValue(TextoBotonProperty);
        set => SetValue(TextoBotonProperty, value);
    }

    public static readonly DependencyProperty TextoBotonProperty =
        DependencyProperty.Register(nameof(TextoBoton), typeof(string), typeof(EditorVinetas),
            new PropertyMetadata("Agregar elemento"));

    public string TextoVacio
    {
        get => (string)GetValue(TextoVacioProperty);
        set => SetValue(TextoVacioProperty, value);
    }

    public static readonly DependencyProperty TextoVacioProperty =
        DependencyProperty.Register(nameof(TextoVacio), typeof(string), typeof(EditorVinetas),
            new PropertyMetadata("Aún no agregó elementos."));

    /// <summary>Los requisitos adicionales no se reordenan; las viñetas sí.</summary>
    public bool MostrarReordenar
    {
        get => (bool)GetValue(MostrarReordenarProperty);
        set => SetValue(MostrarReordenarProperty, value);
    }

    public static readonly DependencyProperty MostrarReordenarProperty =
        DependencyProperty.Register(nameof(MostrarReordenar), typeof(bool), typeof(EditorVinetas),
            new PropertyMetadata(true, (d, _) => ((EditorVinetas)d).PropagarReordenar()));

    /// <summary>Refleja el modo de reordenado en las filas ya creadas.</summary>
    private void PropagarReordenar()
    {
        foreach (var fila in Filas)
        {
            fila.MostrarReordenar = MostrarReordenar;
        }
    }

    /// <summary>Muestra la etiqueta superior (ListaRequisitos no la usa).</summary>
    public bool MostrarTitulo
    {
        get => (bool)GetValue(MostrarTituloProperty);
        set => SetValue(MostrarTituloProperty, value);
    }

    public static readonly DependencyProperty MostrarTituloProperty =
        DependencyProperty.Register(nameof(MostrarTitulo), typeof(bool), typeof(EditorVinetas),
            new PropertyMetadata(true));

    public bool Obligatorio { get; set; }

    /// <summary>Valores no vacios, en orden. Equivale a <c>valores()</c>.</summary>
    public List<string> Valores() => Filas
        .Select(f => f.Texto?.Trim() ?? string.Empty)
        .Where(t => !string.IsNullOrEmpty(t))
        .ToList();

    public void Agregar(string texto = "")
    {
        Filas.Add(new FilaLista
        {
            Texto = texto,
            Marcador = Marcador,
            MostrarReordenar = MostrarReordenar,
        });
        Actualizar();
    }

    public void Cargar(IEnumerable<string>? valores)
    {
        Filas.Clear();
        foreach (var valor in valores ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(valor))
            {
                Filas.Add(new FilaLista
                {
                    Texto = valor,
                    Marcador = Marcador,
                    MostrarReordenar = MostrarReordenar,
                });
            }
        }

        Actualizar();
    }

    public void Limpiar()
    {
        Filas.Clear();
        Actualizar();
    }

    public bool EsValido => !Obligatorio || Valores().Count > 0;

    public bool ForzarValidacion()
    {
        Aviso.Visibility = EsValido ? Visibility.Collapsed : Visibility.Visible;
        return EsValido;
    }

    private void AlAgregar(object sender, RoutedEventArgs e) => Agregar();

    private void AlEliminar(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FilaLista fila })
        {
            Filas.Remove(fila);
            Actualizar();
        }
    }

    private void AlSubir(object sender, RoutedEventArgs e) => Mover(sender, -1);

    private void AlBajar(object sender, RoutedEventArgs e) => Mover(sender, 1);

    private void Mover(object sender, int direccion)
    {
        if (sender is not FrameworkElement { DataContext: FilaLista fila })
        {
            return;
        }

        var indice = Filas.IndexOf(fila);
        var destino = indice + direccion;
        if (indice < 0 || destino < 0 || destino >= Filas.Count)
        {
            return;
        }

        Filas.Move(indice, destino);
        Actualizar();
    }

    /// <summary>Refresca el aviso de lista vacia y los limites de reordenado.</summary>
    private void Actualizar()
    {
        Vacio.Visibility = Filas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        for (var i = 0; i < Filas.Count; i++)
        {
            Filas[i].PuedeSubir = i > 0;
            Filas[i].PuedeBajar = i < Filas.Count - 1;
        }

        if (EsValido)
        {
            Aviso.Visibility = Visibility.Collapsed;
        }
    }
}
