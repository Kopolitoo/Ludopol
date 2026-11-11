using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Ludopol
{
    /// <summary>
    /// VIEW: a scrolling area with two big buttons on its right, "Up" and "Down", each
    /// taking half of the height of the screen.
    ///
    /// The area scrolls with the mouse wheel, so with Prunel's scroll mode, and with the
    /// buttons (by most of a screen at a time). A button is greyed out when there is nothing
    /// more to show on its side. The scroll bar itself is hidden: too thin for the gaze.
    ///
    /// In XAML, the content goes inside: &lt;local:ScrollPanel&gt; … &lt;/local:ScrollPanel&gt;.
    /// </summary>
    [ContentProperty(nameof(Body))]
    public class ScrollPanel : Grid
    {
        private readonly TileButton _up;
        private readonly TileButton _down;

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
            Viewer.ScrollChanged += (s, e) => UpdateButtons();
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
        }
    }
}
