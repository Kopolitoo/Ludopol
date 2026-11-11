using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Ludopol
{
    /// <summary>
    /// VIEW: the big button used everywhere in Ludopol. Its look (size, colours, yellow
    /// frame under the cursor, blue background when selected) is in Ui/Theme.xaml.
    ///
    /// It adds the optional HOVER ACTIVATION (setting HoverActivation): when the cursor
    /// stays on the button for HoverDelayMs, the button activates by itself, as if clicked.
    /// This speeds up navigation with Prunel's movement mode, where the cursor follows the gaze.
    ///
    /// Two safeguards:
    ///   - the delay only starts when the cursor really MOVES onto the button. After a page
    ///     change, the new button that appears under a still cursor does not activate by itself
    ///     (otherwise one look could chain several pages);
    ///   - a button activates only once per visit: the cursor must leave it and come back.
    /// </summary>
    public class TileButton : Button
    {
        /// <summary>Last cursor position seen on any TileButton, in screen pixels.</summary>
        private static Point _lastScreenPoint = new Point(double.NaN, double.NaN);

        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private bool _activatedThisVisit;

        public TileButton()
        {
            _timer.Tick += OnHoverDelayElapsed;
        }

        /// <summary>
        /// Marks the button as chosen (blue background), for example the current rating.
        /// The style reads it through the Tag property.
        /// </summary>
        public bool IsSelected
        {
            get => Equals(Tag, "selected");
            set => Tag = value ? "selected" : null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point screenPoint = PointToScreen(e.GetPosition(this));
            bool moved = screenPoint != _lastScreenPoint;
            _lastScreenPoint = screenPoint;

            AppSettings settings = AppSettings.Current;
            if (!settings.HoverActivation || !moved || _activatedThisVisit || _timer.IsEnabled || !IsEnabled)
            {
                return;
            }
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(200, settings.HoverDelayMs));
            _timer.Start();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _timer.Stop();
            _activatedThisVisit = false;
        }

        private void OnHoverDelayElapsed(object? sender, EventArgs e)
        {
            _timer.Stop();
            if (IsMouseOver && IsEnabled && IsVisible)
            {
                _activatedThisVisit = true;
                OnClick();
            }
        }
    }
}
