// Dobles para probar eventos y datos de los controles reales sin ejecutar WinUI.
// El diseño, XAML y la integración nativa se validan en la compilación Windows de CI.
namespace Microsoft.UI.Xaml
{
    public enum HorizontalAlignment { Left, Right, Center, Stretch }
    public enum VerticalAlignment { Center, Stretch }
    public enum TextAlignment { Left, Center }
    public enum Visibility { Visible, Collapsed }
    public enum GridUnitType { Star }
    public class RoutedEventArgs : EventArgs { }
    public class Style { }
    public readonly struct Thickness { public Thickness(double a, double b, double c, double d) { } }
    public readonly struct GridLength { public GridLength(double a, GridUnitType b) { } public static GridLength Auto => new(); }
    public class FrameworkElement
    {
        public Style? Style { get; set; }
        public HorizontalAlignment HorizontalAlignment { get; set; }
        public VerticalAlignment VerticalAlignment { get; set; }
        public Visibility Visibility { get; set; }
        public Thickness Padding { get; set; }
        public double Width { get; set; }
        public double MinHeight { get; set; }
        public event EventHandler? Loaded;
        public void Cargar() => Loaded?.Invoke(this, EventArgs.Empty);
    }
    public class Recursos { public object this[string key] => key is "Ga.Ok" or "Ga.Error" ? new Media.Brush() : new Style(); }
    public class Application { public static Application Current { get; } = new(); public Recursos Resources { get; } = new(); }
}
namespace Microsoft.UI.Xaml.Media { public class Brush { } }
namespace Microsoft.UI.Text { public static class FontWeights { public static int SemiBold => 1; } }
namespace Microsoft.UI.Xaml.Input
{
    public enum InputScopeNameValue { Number }
    public class InputScopeName { public InputScopeName(InputScopeNameValue v) { } }
    public class InputScope { public List<InputScopeName> Names { get; } = new(); }
}
namespace Microsoft.UI.Xaml.Controls
{
    public enum Orientation { Horizontal, Vertical }
    public class UserControl : FrameworkElement { public object? Content { get; set; } }
    public class Border : FrameworkElement { public object? Child { get; set; } }
    public class TextBlock : FrameworkElement
    {
        public string Text { get; set; } = "";
        public double FontSize { get; set; }
        public int FontWeight { get; set; }
        public TextAlignment TextAlignment { get; set; }
        public Media.Brush? Foreground { get; set; }
    }
    public class Button : UserControl
    {
        public event EventHandler<RoutedEventArgs>? Click;
        public void Pulsar() => Click?.Invoke(this, new());
    }
    public class TextChangedEventArgs : EventArgs { }
    public delegate void TextChangedEventHandler(object sender, TextChangedEventArgs e);
    public class BeforeTextChangingEventArgs : EventArgs { public string NewText { get; set; } = ""; public bool Cancel { get; set; } }
    public class TextBox : TextBlock
    {
        private string _texto = "";
        public new string Text
        {
            get => _texto;
            set
            {
                if (_texto == value) return;
                var e = new BeforeTextChangingEventArgs { NewText = value };
                BeforeTextChanging?.Invoke(this, e);
                if (e.Cancel) return;
                _texto = value;
                TextChanged?.Invoke(this, new());
            }
        }
        public bool AcceptsReturn { get; set; }
        public VerticalAlignment VerticalContentAlignment { get; set; }
        public Input.InputScope? InputScope { get; set; }
        public int SelectionStart { get; set; }
        public event TextChangedEventHandler? TextChanged;
        public event EventHandler<BeforeTextChangingEventArgs>? BeforeTextChanging;
    }
    public class StackPanel : FrameworkElement { public Orientation Orientation { get; set; } public double Spacing { get; set; } public List<FrameworkElement> Children { get; } = new(); }
    public class ColumnDefinition { public GridLength Width { get; set; } }
    public class RowDefinition { public GridLength Height { get; set; } }
    public class Grid : FrameworkElement
    {
        public double ColumnSpacing { get; set; }
        public double RowSpacing { get; set; }
        public List<FrameworkElement> Children { get; } = new();
        public List<ColumnDefinition> ColumnDefinitions { get; } = new();
        public List<RowDefinition> RowDefinitions { get; } = new();
        public static void SetRow(FrameworkElement e, int n) { }
        public static void SetColumn(FrameworkElement e, int n) { }
        public static void SetColumnSpan(FrameworkElement e, int n) { }
    }
    public static class ToolTipService { public static void SetToolTip(object o, object t) { } }
}
namespace GeneradorAnexos.WinUI.Services { }
namespace GeneradorAnexos.WinUI.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    public class Icono : FrameworkElement { public string Nombre { get; set; } = ""; public double Tamano { get; set; } }
    public static class CeldaTabla
    {
        public const double AltoCelda = 50;
        public static Border Envolver(FrameworkElement e) => new() { Child = e };
        public static Border Marco(FrameworkElement e) => Envolver(e);
        public static TextBlock Etiqueta() => new();
        public static TextBlock Cabecera(string s) => new() { Text = s };
        public static TextBox Editor(string s, string ayuda) => new() { Text = s };
        public static void Marcar(TextBox t, bool error) { }
    }
}
