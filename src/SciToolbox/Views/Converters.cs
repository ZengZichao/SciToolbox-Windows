using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SciToolbox.Views;

/// <summary>bool → Visibility（true=Visible）。</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool x && x;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>非空字符串 → Visible；空/null → Collapsed。</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool empty = value == null || string.IsNullOrEmpty(value.ToString());
        bool visible = Invert ? empty : !empty;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>集合非空 → Visible。</summary>
public sealed class CollectionToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int count = value is System.Collections.IEnumerable e ? e.Cast<object>().Count() : 0;
        bool visible = Invert ? count == 0 : count > 0;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>多值：两个值相等 → true（用于单选器 IsChecked 绑定）。</summary>
public sealed class EqualConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.Length == 2 && values[0] != null && Equals(values[0].ToString(), values[1]?.ToString());
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>字符串 == ConverterParameter → true；选中时将 ConverterParameter 写回源（单选器双向）。</summary>
public sealed class StringEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value != null && Equals(value.ToString(), parameter?.ToString());
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is true) ? parameter! : DependencyProperty.UnsetValue;
}

/// <summary>字符串为空 / null → true。用于模板里控制占位文字显示。</summary>
public sealed class IsEmptyConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool empty = value == null || string.IsNullOrWhiteSpace(value.ToString());
        return Invert ? !empty : empty;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>把图片地址转成 ImageSource；空 / null 返回 null，避免默认类型转换器对 null 抛异常。</summary>
public sealed class ImageUrlConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var url = value as string;
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(url, UriKind.RelativeOrAbsolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 输入框占位文字：空输入框也能提示该数据库期望的查询格式。
/// </summary>
public static class Placeholder
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text", typeof(string), typeof(Placeholder), new PropertyMetadata(""));

    public static void SetText(DependencyObject o, string v) => o.SetValue(TextProperty, v);
    public static string GetText(DependencyObject o) => (string)o.GetValue(TextProperty);
}

/// <summary>图标按钮的无障碍名称：仅有私有区字形的按钮对读屏是一串乱码。</summary>
public static class A11y
{
    public static readonly DependencyProperty NameProperty =
        DependencyProperty.RegisterAttached(
            "Name", typeof(string), typeof(A11y),
            new PropertyMetadata(null, OnNameChanged));

    public static void SetName(DependencyObject o, string v) => o.SetValue(NameProperty, v);
    public static string? GetName(DependencyObject o) => (string?)o.GetValue(NameProperty);

    private static void OnNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe) return;
        var name = e.NewValue as string;
        System.Windows.Automation.AutomationProperties.SetName(fe, name ?? "");
        if (string.IsNullOrEmpty(fe.ToolTip as string) && !string.IsNullOrEmpty(name))
            fe.ToolTip = name;
    }
}
