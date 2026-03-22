using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using FSMExpress.Common.Logic.Util;
using FSMExpress.Silksong.Logic.MapLoader;
using System.Numerics;

namespace FSMExpress.Silksong.Controls.MapSelector;
public class MapControl : Grid
{
    private readonly Canvas _can;
    private readonly MatrixTransform _mt;
    private Point _lastPosition = new(0, 0);
    private bool _beingDragged = false;
    private Matrix4x4 _vt = Matrix4x4.Identity;

    private MapLevelCollection? _mapCol;
    private string? _selectedLevel = "";

    public static readonly DirectProperty<MapControl, MapLevelCollection?> MapColProperty =
        AvaloniaProperty.RegisterDirect<MapControl, MapLevelCollection?>(nameof(MapCol), o => o.MapCol, (o, v) => o.MapCol = v);
    public static readonly DirectProperty<MapControl, string?> SelectedLevelProperty =
        AvaloniaProperty.RegisterDirect<MapControl, string?>(nameof(SelectedLevel), o => o.SelectedLevel, (o, v) => o.SelectedLevel = v,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public MapLevelCollection? MapCol
    {
        get => _mapCol;
        set
        {
            SetAndRaise(MapColProperty, ref _mapCol, value);
            RebuildGraph();
        }
    }

    public string? SelectedLevel
    {
        get => _selectedLevel;
        set => SetAndRaise(SelectedLevelProperty, ref _selectedLevel, value);
    }

    public MapControl() : base()
    {
        _mt = new MatrixTransform();
        _can = new Canvas()
        {
            RenderTransform = _mt
        };

        Children.Add(_can);
        ClipToBounds = true;

        Background = Brushes.Black;

        PointerPressed += MouseDownCanvas;
        PointerReleased += MouseUpCanvas;
        PointerMoved += MouseMoveCanvas;
        PointerWheelChanged += MouseScrollCanvas;
    }

    private void MouseDownCanvas(object? sender, PointerPressedEventArgs e)
    {
        var clickProps = e.GetCurrentPoint(this).Properties;
        if (clickProps.IsLeftButtonPressed)
        {
            // todo
        }
        else if (clickProps.IsRightButtonPressed)
        {
            _lastPosition = e.GetPosition(this);
            _beingDragged = true;
            Cursor = new Cursor(StandardCursorType.Hand);
        }
    }

    private void MouseUpCanvas(object? sender, PointerReleasedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            return;

        _beingDragged = false;
        Cursor = new Cursor(StandardCursorType.Arrow);
        _vt = MathUtils.AvaloniaToSystemMatrix(_mt.Matrix);
    }

    private void MouseMoveCanvas(object? sender, PointerEventArgs e)
    {
        if (!_beingDragged)
            return;

        var curPos = e.GetPosition(this);
        _mt.Matrix *= Matrix.CreateTranslation(curPos.X - _lastPosition.X, curPos.Y - _lastPosition.Y);
        _lastPosition = curPos;
    }

    private void MouseScrollCanvas(object? sender, PointerWheelEventArgs e)
    {
        var scale = 1 + e.Delta.Y / 10;

        var halfWidth = _can.Bounds.Width / 2;
        var halfHeight = _can.Bounds.Height / 2;

        var curPos = e.GetPosition(this);
        var zoomX = curPos.X - halfWidth;
        var zoomY = curPos.Y - halfHeight;

        _mt.Matrix *= Matrix.CreateTranslation(-zoomX, -zoomY);
        _mt.Matrix *= Matrix.CreateScale(scale, scale);
        _mt.Matrix *= Matrix.CreateTranslation(zoomX, zoomY);
        _vt = MathUtils.AvaloniaToSystemMatrix(_mt.Matrix);
    }

    private void RebuildGraph()
    {
        _can.Children.Clear();

        if (_mapCol is null)
            return;

        foreach (var item in _mapCol.Levels)
        {
            var image = new Image()
            {
                Source = item.Image,
                Width = item.Image.Size.Width,
                Height = item.Image.Size.Height,
                RenderTransform = new ScaleTransform()
                {
                    ScaleY = -1
                },
            };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.LowQuality);

            image.PointerPressed += (sender, e) =>
            {
                if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    return;

                SelectedLevel = item.LevelName;

                // prevent click through
                e.Handled = true;
            };

            Canvas.SetLeft(image, item.Position.X * 100 - item.Image.Size.Width / 2);
            Canvas.SetTop(image, -item.Position.Y * 100 - item.Image.Size.Height / 2);
            _can.Children.Add(image);
        }

        _mt.Matrix = MathUtils.SystemToAvaloniaMatrix(_vt);
    }
}
