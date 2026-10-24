using System.Globalization;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace UsbAutoConnect;

public static class ThemeManager
{
    private static Dictionary<string, string[]> _palette = [];
    private static readonly Dictionary<string, SolidColorBrush> _brushes = [];
    private static readonly Dictionary<string, ThemeColor> _colors = [];
    public static bool IsDark { get; private set; }
    public static void Initialize(bool dark)
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/RootAutoConnect;component/Assets/palette.json"))!.Stream;
        using var reader = new StreamReader(stream);
        _palette = JsonData.Deserialize<Dictionary<string, string[]>>(reader.ReadToEnd())!;
        foreach (var key in _palette.Keys)
        {
            var color = new ThemeColor { Value = (Color)ColorConverter.ConvertFromString(_palette[key][dark ? 1 : 0]) };
            var brush = new SolidColorBrush();
            // A bound brush cannot be frozen by WPF's resource dictionary. Keep the same live brush for every control and log row.
            BindingOperations.SetBinding(brush, SolidColorBrush.ColorProperty, new Binding(nameof(ThemeColor.Value)) { Source = color });
            _colors[key] = color; _brushes[key] = brush;
            Application.Current.Resources[key] = brush;
        }
        Apply(dark, false);
    }
    public static void Apply(bool dark, bool animate = true)
    {
        IsDark = dark;
        foreach (var (key, colors) in _palette)
        {
            var brush = _brushes[key];
            var current = brush.Color;
            var target = (Color)ColorConverter.ConvertFromString(colors[dark ? 1 : 0]);
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            _colors[key].Value = target;
            BindingOperations.GetBindingExpression(brush, SolidColorBrush.ColorProperty)!.UpdateTarget();
            if (animate && SystemParameters.ClientAreaAnimation)
                brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(current, target, TimeSpan.FromMilliseconds(280))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop });
        }
    }
    public static Brush Brush(string hex) => _brushes["Tone" + hex.TrimStart('#').ToUpperInvariant()];
    private sealed class ThemeColor : INotifyPropertyChanged
    {
        private Color _value;
        public Color Value { get => _value; set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}

public sealed class ThemeBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ThemeManager.Brush((string)value);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
