using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ScreenGuides
{
    public partial class MainWindow : Window
    {
        private const double GuideStartOffset = 20;
        private const double GuideSpacing = 30;
        private const double TransparentOverlayOpacity = 0.35;
        private const double HitTestAreaSize = 15; // extra area so easy to drag and iteract

        private static readonly string SaveFilePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScreenGuides", "guides.json");

        private record GuideEntry(string Orientation, double Position, string Color, double Thickness);
        private record GuidesState(List<GuideEntry> Guides, string HorizontalColor, string VerticalColor, double Thickness);

        private int _horizontalGuideCount;
        private int _verticalGuideCount;
        private bool _isBackgroundHidden;
        private Line? _draggedGuide;
        private Point _dragStartPoint;
        private Color _horizontalGuideColor = Colors.Red;
        private Color _verticalGuideColor = Colors.DodgerBlue;
        private double _guideThickness = 1;
        private ContextMenu? _guideContextMenu;
        private Line? _hoveredGuide;
        private Dictionary<Line, Rectangle> _lineHitTestMap = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void AddHorizontalGuideButton_Click(object sender, RoutedEventArgs e)
        {
            var height = GuidesCanvas.ActualHeight;
            if (height <= 0) return;

            var y = GuideStartOffset + (_horizontalGuideCount * GuideSpacing);
            if (y > height - GuideStartOffset)
            {
                _horizontalGuideCount = 0;
                y = GuideStartOffset;
            }

            AddHorizontalGuideAt(y, _horizontalGuideColor, _guideThickness);
            _horizontalGuideCount++;
            SaveGuides();
        }

        private void AddHorizontalGuideAt(double y, Color color, double thickness)
        {
            var line = new Line
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                X1 = 0,
                Y1 = y,
                X2 = GuidesCanvas.ActualWidth,
                Y2 = y,
                Tag = "H",
                Cursor = _isBackgroundHidden ? Cursors.Arrow : Cursors.SizeNS,
                IsHitTestVisible = !_isBackgroundHidden
            };

            // dis rect for the extra area so can easyly interact
            var hitTestRect = new Rectangle
            {
                Width = GuidesCanvas.ActualWidth,
                Height = HitTestAreaSize * 2,
                Fill = Brushes.Transparent,
                IsHitTestVisible = true,
                Cursor = _isBackgroundHidden ? Cursors.Arrow : Cursors.SizeNS
            };
            Canvas.SetLeft(hitTestRect, 0);
            Canvas.SetTop(hitTestRect, y - HitTestAreaSize);

            hitTestRect.MouseEnter += (s, args) => Guide_MouseEnter(line, args);
            hitTestRect.MouseLeave += (s, args) => Guide_MouseLeave(line, args);
            hitTestRect.PreviewMouseLeftButtonDown += (s, args) => Guide_PreviewMouseLeftButtonDown(line, args);
            hitTestRect.MouseRightButtonDown += (s, args) =>
            {
                ShowGuideContextMenu(line);
                args.Handled = true;
            };

            _lineHitTestMap[line] = hitTestRect;

            line.PreviewMouseLeftButtonDown += (s, args) => Guide_PreviewMouseLeftButtonDown(line, args);
            line.MouseEnter += (s, args) => Guide_MouseEnter(line, args);
            line.MouseLeave += (s, args) => Guide_MouseLeave(line, args);
            line.MouseRightButtonDown += (s, args) =>
            {
                ShowGuideContextMenu(line);
                args.Handled = true;
            };

            GuidesCanvas.Children.Add(line);
            GuidesCanvas.Children.Add(hitTestRect);
        }

        private void AddVerticalGuideButton_Click(object sender, RoutedEventArgs e)
        {
            var width = GuidesCanvas.ActualWidth;
            if (width <= 0) return;

            var x = GuideStartOffset + (_verticalGuideCount * GuideSpacing);
            if (x > width - GuideStartOffset)
            {
                _verticalGuideCount = 0;
                x = GuideStartOffset;
            }

            AddVerticalGuideAt(x, _verticalGuideColor, _guideThickness);
            _verticalGuideCount++;
            SaveGuides();
        }

        private void AddVerticalGuideAt(double x, Color color, double thickness)
        {
            var line = new Line
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = GuidesCanvas.ActualHeight,
                Tag = "V",
                Cursor = _isBackgroundHidden ? Cursors.Arrow : Cursors.SizeWE,
                IsHitTestVisible = !_isBackgroundHidden
            };

            // Create invisible hit-test rectangle
            var hitTestRect = new Rectangle
            {
                Width = HitTestAreaSize * 2,
                Height = GuidesCanvas.ActualHeight,
                Fill = Brushes.Transparent,
                IsHitTestVisible = true,
                Cursor = _isBackgroundHidden ? Cursors.Arrow : Cursors.SizeWE
            };
            Canvas.SetLeft(hitTestRect, x - HitTestAreaSize);
            Canvas.SetTop(hitTestRect, 0);

            hitTestRect.MouseEnter += (s, args) => Guide_MouseEnter(line, args);
            hitTestRect.MouseLeave += (s, args) => Guide_MouseLeave(line, args);
            hitTestRect.PreviewMouseLeftButtonDown += (s, args) => Guide_PreviewMouseLeftButtonDown(line, args);
            hitTestRect.MouseRightButtonDown += (s, args) =>
            {
                ShowGuideContextMenu(line);
                args.Handled = true;
            };

            _lineHitTestMap[line] = hitTestRect;

            line.PreviewMouseLeftButtonDown += (s, args) => Guide_PreviewMouseLeftButtonDown(line, args);
            line.MouseEnter += (s, args) => Guide_MouseEnter(line, args);
            line.MouseLeave += (s, args) => Guide_MouseLeave(line, args);
            line.MouseRightButtonDown += (s, args) =>
            {
                ShowGuideContextMenu(line);
                args.Handled = true;
            };

            GuidesCanvas.Children.Add(line);
            GuidesCanvas.Children.Add(hitTestRect);
        }

        private void ToggleGuidesButton_Checked(object sender, RoutedEventArgs e)
        {
            if (GuidesCanvas == null) return;

            GuidesCanvas.Visibility = Visibility.Visible;
        }

        private void ToggleGuidesButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (GuidesCanvas == null) return;

            GuidesCanvas.Visibility = Visibility.Collapsed;
        }

        private void ToggleBackgroundButton_Checked(object sender, RoutedEventArgs e)
        {
            if (BackgroundLayer == null) return;
            
            BackgroundLayer.Visibility = Visibility.Visible;
            if (WrapPanel != null) WrapPanel.Visibility = Visibility.Visible;
            if (ControlsStackPanel != null) ControlsStackPanel.Visibility = Visibility.Visible;
            
            _isBackgroundHidden = false;
            UpdateGuideInteractionState();
        }
 
        private void ToggleBackgroundButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (BackgroundLayer == null) return;
            
            BackgroundLayer.Visibility = Visibility.Collapsed;
            if (WrapPanel != null) WrapPanel.Visibility = Visibility.Collapsed;
            if (ControlsStackPanel != null) ControlsStackPanel.Visibility = Visibility.Collapsed;
            
            _isBackgroundHidden = true;
            UpdateGuideInteractionState();
        }

        private void ToggleTopmostButton_Checked(object sender, RoutedEventArgs e)
        {
            this.Topmost = true;
        }

        private void ToggleTopmostButton_Unchecked(object sender, RoutedEventArgs e)
        {
            this.Topmost = false;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ClearGuidesButton_Click(object sender, RoutedEventArgs e)
        {
            var guidesToRemove = GuidesCanvas.Children.OfType<Line>().ToList();
            foreach (var line in guidesToRemove)
            {
                RemoveGuide(line);
            }
            _horizontalGuideCount = 0;
            _verticalGuideCount = 0;
            SaveGuides();
        }

        private void GuidesCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            foreach (var child in GuidesCanvas.Children)
            {
                if (child is not Line line || line.Tag is not string orientation)
                {
                    continue;
                }

                if (orientation == "H")
                {
                    line.X2 = GuidesCanvas.ActualWidth;
                    if (_lineHitTestMap.TryGetValue(line, out var hitRect))
                    {
                        hitRect.Width = GuidesCanvas.ActualWidth;
                    }
                }
                else if (orientation == "V")
                {
                    line.Y2 = GuidesCanvas.ActualHeight;
                    if (_lineHitTestMap.TryGetValue(line, out var hitRect))
                    {
                        hitRect.Height = GuidesCanvas.ActualHeight;
                    }
                }
            }
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
            this.Topmost = true;
            GuidesCanvas.PreviewMouseLeftButtonDown += GuidesCanvas_PreviewMouseLeftButtonDown;
            GuidesCanvas.PreviewMouseMove += GuidesCanvas_PreviewMouseMove;
            GuidesCanvas.PreviewMouseLeftButtonUp += GuidesCanvas_PreviewMouseLeftButtonUp;
            LoadGuides();
        }

        private void GuidesCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (BackgroundLayer.Visibility == Visibility.Visible && this.WindowState != WindowState.Maximized)
            {
                this.DragMove();
            }
        }

        private void FullScreenToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
        }

        private void FullScreenToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Normal;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void Guide_PreviewMouseLeftButtonDown(Line line, MouseButtonEventArgs e)
        {
            if (line != null)
            {
                _draggedGuide = line;
                _dragStartPoint = e.GetPosition(GuidesCanvas);
                _draggedGuide.CaptureMouse();
                e.Handled = true;
            }
        }

        private void GuidesCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedGuide != null && _draggedGuide.IsMouseCaptured)
            {
                Point currentPoint = e.GetPosition(GuidesCanvas);
                string? orientation = _draggedGuide.Tag as string;

                if (orientation == "H")
                {
                    // Horizontal guide - move only vertically
                    double deltaY = currentPoint.Y - _dragStartPoint.Y;
                    double newY = _draggedGuide.Y1 + deltaY;

                    // Constrain to canvas bounds
                    newY = Math.Max(0, Math.Min(newY, GuidesCanvas.ActualHeight));

                    _draggedGuide.Y1 = newY;
                    _draggedGuide.Y2 = newY;
                    
                    if (_lineHitTestMap.TryGetValue(_draggedGuide, out var hitRect))
                    {
                        Canvas.SetTop(hitRect, newY - HitTestAreaSize);
                    }
                    
                    _dragStartPoint = currentPoint;
                }
                else if (orientation == "V")
                {
                    // Vertical guide - move only horizontally
                    double deltaX = currentPoint.X - _dragStartPoint.X;
                    double newX = _draggedGuide.X1 + deltaX;

                    // Constrain to canvas bounds
                    newX = Math.Max(0, Math.Min(newX, GuidesCanvas.ActualWidth));

                    _draggedGuide.X1 = newX;
                    _draggedGuide.X2 = newX;
                    
                    if (_lineHitTestMap.TryGetValue(_draggedGuide, out var hitRect))
                    {
                        Canvas.SetLeft(hitRect, newX - HitTestAreaSize);
                    }
                    
                    _dragStartPoint = currentPoint;
                }
            }
        }

        private void GuidesCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedGuide != null && _draggedGuide.IsMouseCaptured)
            {
                _draggedGuide.ReleaseMouseCapture();
                _draggedGuide = null;
                SaveGuides();
            }
        }

        private void ChangeHorizontalColorButton_Click(object sender, RoutedEventArgs e)
        {
            Color selectedColor = ShowColorPicker(_horizontalGuideColor);
            if (selectedColor != Colors.Transparent)
            {
                _horizontalGuideColor = selectedColor;
                SaveGuides();
            }
        }

        private void ChangeVerticalColorButton_Click(object sender, RoutedEventArgs e)
        {
            Color selectedColor = ShowColorPicker(_verticalGuideColor);
            if (selectedColor != Colors.Transparent)
            {
                _verticalGuideColor = selectedColor;
                SaveGuides();
            }
        }

        private void Guide_MouseEnter(Line line, MouseEventArgs e)
        {
            if (line != null && _draggedGuide == null)
            {
                _hoveredGuide = line;
            }
        }

        private void Guide_MouseLeave(Line line, MouseEventArgs e)
        {
            if (line != null && line == _hoveredGuide)
            {
                _hoveredGuide = null;
            }
        }

        private void ShowGuideContextMenu(Line line)
        {
            _guideContextMenu = new ContextMenu();

            var removeItem = new MenuItem { Header = "Remove Line" };
            removeItem.Click += (s, args) => RemoveGuide(line);
            _guideContextMenu.Items.Add(removeItem);

            var changeColorItem = new MenuItem { Header = "Change Color" };
            changeColorItem.Click += (s, args) => ChangeGuideColor(line);
            _guideContextMenu.Items.Add(changeColorItem);

            line.ContextMenu = _guideContextMenu;
            _guideContextMenu.IsOpen = true;
        }

        private void RemoveGuide(Line line)
        {
            if (_lineHitTestMap.TryGetValue(line, out var hitRect))
            {
                GuidesCanvas.Children.Remove(hitRect);
                _lineHitTestMap.Remove(line);
            }
            GuidesCanvas.Children.Remove(line);
            _hoveredGuide = null;
            SaveGuides();
        }

        private void ChangeGuideColor(Line line)
        {
            SolidColorBrush? brush = line.Stroke as SolidColorBrush;
            Color currentColor = brush?.Color ?? Colors.Black;
            
            Color selectedColor = ShowColorPicker(currentColor);
            if (selectedColor != Colors.Transparent)
            {
                line.Stroke = new SolidColorBrush(selectedColor);
                SaveGuides();
            }
        }

        private Color ShowColorPicker(Color currentColor)
        {
            var colors = new[] 
            { 
                Colors.Red, Colors.Green, Colors.Blue, Colors.Yellow, 
                Colors.Cyan, Colors.Magenta, Colors.White, Colors.Black,
                Colors.Orange, Colors.Purple, Colors.Pink, Colors.Brown,
                Colors.DodgerBlue, Colors.Lime, Colors.Aqua, Colors.Gray
            };

            var window = new Window
            {
                Title = "Select Color",
                Width = 350,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = SystemColors.ControlBrush
            };

            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            
            Color selectedColor = Colors.Transparent;

            var grid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 10, 0, 10) };
            foreach (var color in colors)
            {
                var button = new Button
                {
                    Width = 50,
                    Height = 50,
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush(color),
                    ToolTip = color.ToString()
                };
                button.Click += (s, e) =>
                {
                    selectedColor = color;
                    window.DialogResult = true;
                    window.Close();
                };
                grid.Children.Add(button);
            }

            stackPanel.Children.Add(new TextBlock { Text = "Select a color:", Margin = new Thickness(0, 0, 0, 5) });
            stackPanel.Children.Add(grid);
            
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var cancelButton = new Button { Content = "Cancel", Width = 80, Margin = new Thickness(5, 0, 0, 0) };
            cancelButton.Click += (s, e) => window.Close();
            buttonPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(buttonPanel);
            window.Content = stackPanel;

            window.ShowDialog();
            return selectedColor;
        }
        
        private void UpdateGuideInteractionState()
        {
            foreach (var child in GuidesCanvas.Children)
            {
                if (child is Line line && line.Tag is string orientation)
                {
                    line.IsHitTestVisible = !_isBackgroundHidden;
                    line.Cursor = _isBackgroundHidden
                        ? Cursors.Arrow
                        : orientation == "H"
                            ? Cursors.SizeNS
                            : Cursors.SizeWE;

                    if (_lineHitTestMap.TryGetValue(line, out var hitRect))
                    {
                        hitRect.IsHitTestVisible = !_isBackgroundHidden;
                        hitRect.Cursor = line.Cursor;
                    }
                }
            }
        }

        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _guideThickness = e.NewValue;

            // Guard against controls not yet initialized during InitializeComponent
            if (ThicknessValueLabel == null || GuidesCanvas == null)
                return;

            ThicknessValueLabel.Text = _guideThickness.ToString("F1");

            // Update thickness of all existing guides
            foreach (var child in GuidesCanvas.Children)
            {
                if (child is Line line)
                {
                    line.StrokeThickness = _guideThickness;
                }
            }

            SaveGuides();
        }

        private void SaveGuides()
        {
            try
            {
                var guides = GuidesCanvas.Children.OfType<Line>()
                    .Select(l => new GuideEntry(
                        l.Tag as string ?? "H",
                        l.Tag as string == "H" ? l.Y1 : l.X1,
                        ((SolidColorBrush)l.Stroke).Color.ToString(),
                        l.StrokeThickness))
                    .ToList();

                var state = new GuidesState(
                    guides,
                    _horizontalGuideColor.ToString(),
                    _verticalGuideColor.ToString(),
                    _guideThickness);

                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SaveFilePath)!);
                File.WriteAllText(SaveFilePath, json);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to save guides: {e.Message}");
            }
        }

        private void LoadGuides()
        {
            if (!File.Exists(SaveFilePath)) return;

            try
            {
                var json = File.ReadAllText(SaveFilePath);
                var state = JsonSerializer.Deserialize<GuidesState>(json);
                if (state == null) return;

                if (ColorConverter.ConvertFromString(state.HorizontalColor) is Color hColor)
                    _horizontalGuideColor = hColor;
                if (ColorConverter.ConvertFromString(state.VerticalColor) is Color vColor)
                    _verticalGuideColor = vColor;

                _guideThickness = state.Thickness;
                ThicknessSlider.Value = _guideThickness;

                foreach (var guide in state.Guides)
                {
                    var color = ColorConverter.ConvertFromString(guide.Color) is Color c ? c : Colors.Red;
                    if (guide.Orientation == "H")
                        AddHorizontalGuideAt(guide.Position, color, guide.Thickness);
                    else if (guide.Orientation == "V")
                        AddVerticalGuideAt(guide.Position, color, guide.Thickness);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to load guides: {e.Message}");
            }
        }
    }
}