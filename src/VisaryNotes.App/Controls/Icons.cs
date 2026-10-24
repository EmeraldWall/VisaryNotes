using Microsoft.Maui.Controls.Shapes;

namespace VisaryNotes.App.Controls;

/// <summary>Stroke icons on a 24 by 24 grid with square caps, matching the prototype.</summary>
public static class Icons
{
    public const string Back = "M15 5L8 12L15 19";
    public const string Forward = "M9 5L16 12L9 19";
    public const string Search = "M10.5 4A6.5 6.5 0 1 0 10.5 17A6.5 6.5 0 1 0 10.5 4ZM20 20L15.2 15.2";
    public const string Plus = "M12 5V19M5 12H19";
    public const string Folder = "M3 6H9L11 8H21V19H3Z";
    public const string Notes = "M4 3H20V21H4ZM8 8H16M8 12H16M8 16H13";
    public const string Share = "M12 3V15M7 8L12 3L17 8M5 14V20H19V14";
    public const string Preview = "M2 12C2 12 6 5 12 5C18 5 22 12 22 12C22 12 18 19 12 19C6 19 2 12 2 12ZM10 10H14V14H10Z";
    public const string Settings = "M4 7H14M18 7H20M4 17H8M12 17H20M14 5H18V9H14ZM8 15H12V19H8Z";
    public const string Close = "M6 6L18 18M18 6L6 18";
    public const string Edit = "M4 20H8L19 9L15 5L4 16ZM13 7L17 11";
    public const string Check = "M5 12L9 16L19 6";
    public const string More = "M5 12H6M11.5 12H12.5M18 12H19";
    public const string Up = "M6 15L12 9L18 15";
    public const string Down = "M6 9L12 15L18 9";
    public const string Link = "M9.5 14.5L14.5 9.5M11 7L12.5 5.5A4.25 4.25 0 0 1 18.5 11.5L17 13M13 17L11.5 18.5A4.25 4.25 0 0 1 5.5 12.5L7 11";
    public const string Alarm = "M12 5A7.5 7.5 0 1 0 12 20A7.5 7.5 0 1 0 12 5ZM12 9V13L14.5 15M3 6L6 3M21 6L18 3";
    public const string Quote = "M6 17H10V11H7C7 9 8 8 10 8V6C7 6 5 8 5 11V17ZM14 17H18V11H15C15 9 16 8 18 8V6C15 6 13 8 13 11V17Z";

    public static Microsoft.Maui.Controls.Shapes.Path Make(string data, string brushKey = "InkBrush", double size = 24)
    {
        var path = new Microsoft.Maui.Controls.Shapes.Path
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!,
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Square,
            StrokeLineJoin = PenLineJoin.Miter,
            WidthRequest = size,
            HeightRequest = size,
            Aspect = Stretch.None,
            InputTransparent = true,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        path.SetDynamicResource(Shape.StrokeProperty, brushKey);
        return path;
    }
}
