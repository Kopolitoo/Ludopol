using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Ludopol
{
    /// <summary>
    /// VIEW: a scrolling area with a big scroll bar on its right: a small arrow button at each
    /// end and, between them, a wide track whose thumb shows where we are in the page.
    ///
    /// The area scrolls with the mouse wheel (so with Prunel's scroll mode), with the arrows
    /// (by most of a screen at a time, and on and on while the gaze stays on them), by
    /// clicking the track above or below the thumb (a screen at a time), by dragging the
    /// thumb, and with the keyboard (HandleKey, called by the main window: Page Up / Page
    /// Down, Home / End, arrows). The bar is hidden when everything fits.
    ///
    /// In XAML, the content goes inside: &lt;local:ScrollPanel&gt; … &lt;/local:ScrollPanel&gt;.
    /// </summary>
    [ContentProperty(nameof(Body))]
    public class ScrollPanel : Grid
    {
        private readonly TileButton _up;
        private readonly TileButton _down;

        /// <summary>The scroll bar: the arrows and, between them, the track with its thumb.</summary>
        private readonly Grid _bar = new Grid { Margin = new Thickness(6, 6, 6, 6) };
        private readonly Grid _track = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        private readonly Border _thumb = new Border { CornerRadius = new CornerRadius(8), VerticalAlignment = VerticalAlignment.Top };

        /// <summary>While the thumb is dragged: where the pointer took it, and the offset then.</summary>
        private double? _dragStartY;
        private double _dragStartOffset;

        public ScrollViewer Viewer { get; } = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            PanningMode = PanningMode.VerticalOnly,
            Focusable = false,
        };

        /// <summary>What is shown in the scrolling area.</summary>
        public object? Body
        {
            get => Viewer.Content;
            set => Viewer.Content = value;
        }

        public ScrollPanel()
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Children.Add(Viewer);

            // RepeatOnHover: with hover activation, the area goes on scrolling while the cursor
            // stays on the arrow.
            _up = new TileButton { Content = "▲", RepeatOnHover = true, Padding = new Thickness(0), Margin = new Thickness(0) };
            _down = new TileButton { Content = "▼", RepeatOnHover = true, Padding = new Thickness(0), Margin = new Thickness(0) };
            foreach (TileButton arrow in new[] { _up, _down })
            {
                arrow.SetResourceReference(WidthProperty, "SmallButtonSize");
                arrow.SetResourceReference(HeightProperty, "SmallButtonSize");
                arrow.SetResourceReference(MinWidthProperty, "SmallButtonSize");
                arrow.SetResourceReference(MinHeightProperty, "SmallButtonSize");
            }
            _up.Click += (s, e) => ScrollBy(-1);
            _down.Click += (s, e) => ScrollBy(1);

            _bar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _bar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _bar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(_track, 1);
            Grid.SetRow(_down, 2);
            _bar.Children.Add(_up);
            _bar.Children.Add(_track);
            _bar.Children.Add(_down);
            SetColumn(_bar, 1);
            Children.Add(_bar);

            Border trackBack = new Border { CornerRadius = new CornerRadius(8) };
            trackBack.SetResourceReference(Border.BackgroundProperty, "GaugeEmptyBrush");
            _thumb.SetResourceReference(Border.BackgroundProperty, "MutedTextBrush");
            _track.Children.Add(trackBack);
            _track.Children.Add(_thumb);

            _track.MouseLeftButtonDown += OnTrackDown;
            _track.MouseMove += OnTrackMove;
            _track.MouseLeftButtonUp += (s, e) => EndDrag();
            _track.LostMouseCapture += (s, e) => _dragStartY = null;

            Viewer.ScrollChanged += (s, e) => UpdateButtons();
            _track.SizeChanged += (s, e) => UpdateButtons();
        }

        /// <summary>On the thumb: starts dragging it. Above or below it: a screen up or down.</summary>
        private void OnTrackDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            double y = e.GetPosition(_track).Y;
            double top = _thumb.Margin.Top;
            if (y < top)
            {
                ScrollBy(-1);
            }
            else if (y > top + _thumb.ActualHeight)
            {
                ScrollBy(1);
            }
            else
            {
                _dragStartY = y;
                _dragStartOffset = Viewer.VerticalOffset;
                _track.CaptureMouse();
            }
            e.Handled = true;
        }

        private void OnTrackMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_dragStartY is not double start)
            {
                return;
            }
            double free = _track.ActualHeight - _thumb.ActualHeight;
            if (free <= 0)
            {
                return;
            }
            double moved = e.GetPosition(_track).Y - start;
            Viewer.ScrollToVerticalOffset(_dragStartOffset + moved * Viewer.ScrollableHeight / free);
        }

        private void EndDrag()
        {
            _dragStartY = null;
            _track.ReleaseMouseCapture();
        }

        /// <summary>
        /// A key pressed while this area is on screen: Page Down / Page Up scroll by a screen,
        /// Home / End go to the top / bottom, Down / Up by a little. Returns true if used.
        /// </summary>
        public bool HandleKey(System.Windows.Input.Key key)
        {
            switch (key)
            {
                case System.Windows.Input.Key.PageDown:
                case System.Windows.Input.Key.Space:
                    ScrollBy(1);
                    return true;
                case System.Windows.Input.Key.PageUp:
                    ScrollBy(-1);
                    return true;
                case System.Windows.Input.Key.Home:
                    Viewer.ScrollToTop();
                    return true;
                case System.Windows.Input.Key.End:
                    Viewer.ScrollToBottom();
                    return true;
                case System.Windows.Input.Key.Down:
                    Viewer.ScrollToVerticalOffset(Viewer.VerticalOffset + Viewer.ViewportHeight * 0.15);
                    return true;
                case System.Windows.Input.Key.Up:
                    Viewer.ScrollToVerticalOffset(Viewer.VerticalOffset - Viewer.ViewportHeight * 0.15);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Goes back to the top (when the content changes, for example).</summary>
        public void ScrollToTop() => Viewer.ScrollToTop();

        /// <summary>Scrolls by 80 % of the visible height: a little of the previous view stays visible.</summary>
        private void ScrollBy(int direction)
        {
            Viewer.ScrollToVerticalOffset(Viewer.VerticalOffset + direction * Viewer.ViewportHeight * 0.8);
        }

        private void UpdateButtons()
        {
            _up.IsEnabled = Viewer.VerticalOffset > 0.5;
            _down.IsEnabled = Viewer.VerticalOffset < Viewer.ScrollableHeight - 0.5;

            // Hidden when everything fits (its room is kept, so the content does not jump).
            // Otherwise the thumb's height is the part of the page on screen, and its
            // position where that part is.
            bool scrolls = Viewer.ScrollableHeight > 0.5 && Viewer.ExtentHeight > 0;
            _bar.Visibility = scrolls ? Visibility.Visible : Visibility.Hidden;
            if (scrolls)
            {
                double track = _track.ActualHeight;
                double height = System.Math.Max(40, track * Viewer.ViewportHeight / Viewer.ExtentHeight);
                _thumb.Height = System.Math.Min(height, track);
                _thumb.Margin = new Thickness(0, (track - _thumb.Height) * Viewer.VerticalOffset / Viewer.ScrollableHeight, 0, 0);
            }
        }
    }
}
