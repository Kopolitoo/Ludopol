using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Ludopol
{
    /// <summary>
    /// VIEW: a scrolling area with two big buttons on its right, "Up" and "Down", each
    /// taking half of the height of the screen.
    ///
    /// The area scrolls with the mouse wheel, so with Prunel's scroll mode, with the
    /// buttons (by most of a screen at a time), and with the keyboard (HandleKey, called by
    /// the main window: Page Up / Page Down, Home / End, arrows). A button is greyed out when
    /// there is nothing more to show on its side. The scroll bar itself is hidden, too thin
    /// for the gaze; a thin gauge on the far right only shows where we are in the page.
    ///
    /// In XAML, the content goes inside: &lt;local:ScrollPanel&gt; … &lt;/local:ScrollPanel&gt;.
    /// </summary>
    [ContentProperty(nameof(Body))]
    public class ScrollPanel : Grid
    {
        private readonly TileButton _up;
        private readonly TileButton _down;

        /// <summary>The thin position gauge on the right: its track and the part shown on screen.</summary>
        private readonly Grid _gauge = new Grid { Width = 10, Margin = new Thickness(4, 12, 0, 12) };
        private readonly Border _thumb = new Border { CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Top };

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
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            SetRowSpan(Viewer, 2);
            Children.Add(Viewer);

            // RepeatOnHover: with hover activation, the area goes on scrolling while the cursor
            // stays on the button.
            _up = new TileButton { Content = "▲\n" + Texts.Get("scroll.up"), RepeatOnHover = true };
            _down = new TileButton { Content = "▼\n" + Texts.Get("scroll.down"), RepeatOnHover = true };
            _up.SetResourceReference(WidthProperty, "SideButtonWidth");
            _down.SetResourceReference(WidthProperty, "SideButtonWidth");
            SetColumn(_up, 1);
            SetColumn(_down, 1);
            SetRow(_down, 1);
            Children.Add(_up);
            Children.Add(_down);

            _up.Click += (s, e) => ScrollBy(-1);
            _down.Click += (s, e) => ScrollBy(1);
            Border track = new Border { CornerRadius = new CornerRadius(5) };
            track.SetResourceReference(Border.BackgroundProperty, "GaugeEmptyBrush");
            _thumb.SetResourceReference(Border.BackgroundProperty, "MutedTextBrush");
            _gauge.Children.Add(track);
            _gauge.Children.Add(_thumb);
            SetColumn(_gauge, 2);
            SetRowSpan(_gauge, 2);
            Children.Add(_gauge);

            Viewer.ScrollChanged += (s, e) => UpdateButtons();
            _gauge.SizeChanged += (s, e) => UpdateButtons();
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

            // The gauge: hidden when everything fits; otherwise the thumb's height is the part
            // of the page on screen, and its position where that part is.
            bool scrolls = Viewer.ScrollableHeight > 0.5 && Viewer.ExtentHeight > 0;
            _gauge.Visibility = scrolls ? Visibility.Visible : Visibility.Hidden;
            if (scrolls)
            {
                double track = _gauge.ActualHeight;
                double height = System.Math.Max(20, track * Viewer.ViewportHeight / Viewer.ExtentHeight);
                _thumb.Height = System.Math.Min(height, track);
                _thumb.Margin = new Thickness(0, (track - _thumb.Height) * Viewer.VerticalOffset / Viewer.ScrollableHeight, 0, 0);
            }
        }
    }
}
