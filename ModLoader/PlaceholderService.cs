using System.Windows;
using System.Windows.Media;

namespace ModManager
{
    public static class PlaceholderService
    {
        // Existing placeholder property
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.RegisterAttached(
                "Placeholder",
                typeof(string),
                typeof(PlaceholderService),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        // Border brush property for normal state
        public static readonly DependencyProperty BorderBrushProperty =
            DependencyProperty.RegisterAttached(
                "BorderBrush",
                typeof(Brush),
                typeof(PlaceholderService),
                new FrameworkPropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")), FrameworkPropertyMetadataOptions.AffectsRender));

        // Border brush property for focused state
        public static readonly DependencyProperty FocusedBorderBrushProperty =
            DependencyProperty.RegisterAttached(
                "FocusedBorderBrush",
                typeof(Brush),
                typeof(PlaceholderService),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        // Placeholder property methods
        public static void SetPlaceholder(UIElement element, string value)
        {
            element.SetValue(PlaceholderProperty, value);
        }

        public static string GetPlaceholder(UIElement element)
        {
            return (string)element.GetValue(PlaceholderProperty);
        }

        // Border brush property methods
        public static void SetBorderBrush(UIElement element, Brush value)
        {
            element.SetValue(BorderBrushProperty, value);
        }

        public static Brush GetBorderBrush(UIElement element)
        {
            return (Brush)element.GetValue(BorderBrushProperty);
        }

        // Focused border brush property methods
        public static void SetFocusedBorderBrush(UIElement element, Brush value)
        {
            element.SetValue(FocusedBorderBrushProperty, value);
        }

        public static Brush GetFocusedBorderBrush(UIElement element)
        {
            return (Brush)element.GetValue(FocusedBorderBrushProperty);
        }
    }
}