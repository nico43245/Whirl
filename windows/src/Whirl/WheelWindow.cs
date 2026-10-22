using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Whirl.Core;
using static Whirl.Native;

namespace Whirl;

/// <summary>A borderless, transparent, never-activated window that floats under the cursor, so the app you are leaving keeps focus.</summary>
public sealed class WheelWindow : Window
{
    public static WheelWindow Instance { get; } = new();

    public WheelModel Model { get; } = new();
    private readonly Canvas _canvas = new() { Width = WheelGeometry.Size, Height = WheelGeometry.Size };
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private static readonly FontFamily TextFont = new("Segoe UI Variable Display, Segoe UI");

    private POINT _originPx;
    private double _scale = 1;

    /// <summary>Raised when the wheel is clicked, as an alternative to letting go of the keys.</summary>
    public event Action? Clicked;

    private WheelWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Width = Height = WheelGeometry.Size;
        Content = _canvas;
        Title = "Whirl";

        Model.Changed += Render;
        Core.Settings.Current.Changed += Render;
        MouseWheel += (_, e) => Model.SwitchPage(e.Delta > 0 ? -1 : 1);
        MouseLeftButtonDown += (_, e) => { e.Handled = true; Clicked?.Invoke(); };

        SourceInitialized += (_, _) =>
        {
            // Tool window that never takes focus away from the app you are leaving.
            var hwnd = new WindowInteropHelper(this).Handle;
            var ex = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST));
        };
    }

    public bool IsShowing { get; private set; }

    /// <summary>Shows the wheel centred on the cursor (kept on screen) and returns the cursor position in pixels.</summary>
    internal POINT ShowAtCursor()
    {
        Opacity = 0;
        Show();
        IsShowing = true;

        // Place in physical pixels so it lands right under the cursor on any monitor/DPI.
        var hwnd = new WindowInteropHelper(this).Handle;
        GetCursorPos(out var cursor);
        _scale = GetDpiForWindow(hwnd) / 96.0;
        var size = (int)Math.Round(WheelGeometry.Size * _scale);
        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(MonitorFromPoint(cursor, 2 /* nearest */), ref info);
        var x = Math.Clamp(cursor.X - size / 2, info.rcWork.Left, Math.Max(info.rcWork.Left, info.rcWork.Right - size));
        var y = Math.Clamp(cursor.Y - size / 2, info.rcWork.Top, Math.Max(info.rcWork.Top, info.rcWork.Bottom - size));
        _originPx = new POINT { X = x, Y = y };
        SetWindowPos(hwnd, HWND_TOPMOST, x, y, size, size, SWP_NOACTIVATE | SWP_SHOWWINDOW);

        BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
        return cursor;
    }

    public void HideNow()
    {
        IsShowing = false;
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;
        Hide();
        Model.ClearHover();
    }

    /// <summary>A screen position in pixels, in wheel DIPs.</summary>
    internal (double X, double Y) ToWheelPoint(POINT screen) => ((screen.X - _originPx.X) / _scale, (screen.Y - _originPx.Y) / _scale);

    // MARK: Drawing

    private void Render()
    {
        var theme = Theme.Current;
        _canvas.Children.Clear();
        const double size = WheelGeometry.Size, outer = WheelGeometry.OuterRadius;
        var center = new Point(size / 2, size / 2);

        var disc = new Ellipse
        {
            Width = outer * 2 + 24, Height = outer * 2 + 24,
            Fill = theme.Glass.Brush(),
            Stroke = theme.Accent.Brush(0.35), StrokeThickness = 1,
            Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.3 },
        };
        Place(disc, center.X - disc.Width / 2, center.Y - disc.Height / 2);

        var page = Model.PageItems;
        for (var i = 0; i < WheelGeometry.SliceCount; i++) DrawSlice(i, page[i], theme, center);
        DrawHub(theme, center);

        var title = Pill(Model.Mode == SwitchMode.Apps ? "APPS" : "WINDOWS", theme, 11, FontWeights.Heavy);
        title.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Place(title, (size - title.DesiredSize.Width) / 2, 4);

        if (Model.PageCount > 1)
        {
            var dots = new StackPanel { Orientation = Orientation.Horizontal };
            for (var p = 0; p < Model.PageCount; p++)
            {
                var current = p == Model.PageIndex;
                dots.Children.Add(new Rectangle
                {
                    Width = current ? 18 : 6, Height = 6, RadiusX = 3, RadiusY = 3, Margin = new Thickness(3, 0, 3, 0),
                    Fill = current ? theme.Accent.Brush() : Brushes.Gray,
                });
            }
            var dotsBox = new Border { Child = dots, Padding = new Thickness(6), CornerRadius = new CornerRadius(9), Background = theme.Glass.Brush() };
            dotsBox.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Place(dotsBox, (size - dotsBox.DesiredSize.Width) / 2, size - dotsBox.DesiredSize.Height - 6);
        }
    }

    private void DrawSlice(int index, SwitchItem? item, Theme theme, Point c)
    {
        var hovered = Model.Hit == WheelHit.OfSlice(index) && item is not null;
        var angle = WheelGeometry.CenterAngle(index);
        var grow = hovered ? 6 : 0;

        var slice = new System.Windows.Shapes.Path
        {
            Data = Sector(c, WheelGeometry.InnerRadius, WheelGeometry.OuterRadius + grow, angle - 22.5 + 2.6, angle + 22.5 - 2.6),
            Fill = hovered ? theme.Accent.Brush() : theme.Petal.Brush(item is null ? 0.35 : 1),
            Stroke = theme.Accent.Brush(hovered ? 0 : 0.2),
            StrokeThickness = 1,
            Effect = hovered ? new DropShadowEffect { Color = theme.Accent, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.7 } : null,
        };
        _canvas.Children.Add(slice);
        if (item is null) return;

        var mid = (WheelGeometry.InnerRadius + WheelGeometry.OuterRadius) / 2;
        var rad = angle * Math.PI / 180;
        var color = (hovered ? theme.TextOnAccent : theme.Text).Brush();
        var label = new StackPanel { Width = 80 };
        label.Children.Add(new Image
        {
            Source = IconCache.For(item), Width = 36, Height = 36, Opacity = item.IsRunning ? 1 : 0.55,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        label.Children.Add(new TextBlock
        {
            Text = item.Title, FontFamily = TextFont, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = color,
            HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 80,
            Margin = new Thickness(0, 3, 0, 0),
        });
        // A dot for "running", a bar for "you are here". Closed pinned apps show nothing.
        var marker = (hovered ? theme.TextOnAccent : theme.Accent);
        label.Children.Add(new Rectangle
        {
            Width = item.IsForeground ? 14 : 4, Height = 4, RadiusX = 2, RadiusY = 2, Margin = new Thickness(0, 3, 0, 0),
            Fill = item.IsRunning ? marker.Brush() : Brushes.Transparent,
        });
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Place(label, c.X + Math.Cos(rad) * mid - 40, c.Y + Math.Sin(rad) * mid - label.DesiredSize.Height / 2);
    }

    private void DrawHub(Theme theme, Point c)
    {
        var hovered = Model.Hit == WheelHit.Hub;
        var r = WheelGeometry.HubRadius + (hovered ? 3 : 0);
        var hub = new Ellipse
        {
            Width = r * 2, Height = r * 2,
            Fill = hovered ? theme.Accent.Brush() : theme.Petal.Brush(),
            Stroke = theme.Accent.Brush(0.5), StrokeThickness = 1.5,
            Effect = hovered ? new DropShadowEffect { Color = theme.Accent, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.7 } : null,
        };
        Place(hub, c.X - r, c.Y - r);

        var stack = new StackPanel { Width = WheelGeometry.HubRadius * 1.7 };
        void Line(string text, double size, FontWeight weight, Brush brush, FontFamily? font = null) =>
            stack.Children.Add(new TextBlock
            {
                Text = text, FontSize = size, FontWeight = weight, Foreground = brush, FontFamily = font ?? TextFont,
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxHeight = 56,
                TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center,
            });

        if (Model.HoveredItem is { } item)
        {
            Line(item.Subtitle ?? item.Title, 12, FontWeights.Heavy, theme.Accent.Brush());
            Line(item.Subtitle is null ? (item.IsRunning ? "Release to switch" : "Release to open") : item.Title,
                 10, FontWeights.Medium, theme.Text.Brush());
        }
        else
        {
            var brush = (hovered ? theme.TextOnAccent : theme.Text).Brush(hovered ? 1 : 0.75);
            Line("", 14, FontWeights.Normal, brush, IconFont);   // the "cancel" glyph
            Line("Cancel", 10, FontWeights.Bold, brush);
        }
        stack.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Place(stack, c.X - stack.Width / 2, c.Y - stack.DesiredSize.Height / 2);
    }

    private static Border Pill(string text, Theme theme, double size, FontWeight weight) => new()
    {
        Child = new TextBlock { Text = text, FontSize = size, FontWeight = weight, FontFamily = TextFont, Foreground = (theme.IsDark ? Colors.White : theme.Text).Brush() },
        Padding = new Thickness(12, 4, 12, 5),
        CornerRadius = new CornerRadius(12),
        Background = theme.Glass.Brush(),
        BorderBrush = theme.Accent.Brush(0.4),
        BorderThickness = new Thickness(1),
    };

    private static Geometry Sector(Point c, double inner, double outer, double startDeg, double endDeg)
    {
        Point At(double r, double deg) => new(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
        var figure = new PathFigure { StartPoint = At(outer, startDeg), IsClosed = true };
        figure.Segments.Add(new ArcSegment(At(outer, endDeg), new Size(outer, outer), 0, false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(At(inner, endDeg), true));
        figure.Segments.Add(new ArcSegment(At(inner, startDeg), new Size(inner, inner), 0, false, SweepDirection.Counterclockwise, true));
        var g = new PathGeometry([figure]);
        g.Freeze();
        return g;
    }

    private void Place(UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        _canvas.Children.Add(element);
    }
}
