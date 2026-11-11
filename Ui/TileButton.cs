using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Ludopol
{
    /// <summary>
    /// VIEW: the big button used everywhere in Ludopol. Its look (size, colours, yellow
    /// frame under the cursor, blue background when selected, hover gauge) is in Ui/Theme.xaml.
    ///
    /// It adds HOVER ACTIVATION (setting HoverActivation, on by default): when the cursor
    /// stays on the button for HoverDelayMs (1 second by default), the button activates by
    /// itself, as if clicked. A yellow gauge fills at the bottom of the button during the delay.
    /// This is made for Prunel's movement mode, where the cursor follows the gaze.
    /// A click always works too.
    ///
    /// Two safeguards:
    ///   - the delay only starts when the cursor really MOVES onto the button. After a page
    ///     change, the new button that appears under a still cursor does not activate by itself
    ///     (otherwise one look could chain several pages);
    ///   - a button activates only once per visit: the cursor must leave it and come back.
    ///     Except the buttons with RepeatOnHover (Up / Down), which go on scrolling, once per
    ///     delay, while the cursor stays on them.
    /// </summary>
    public class TileButton : Button
    {
        /// <summary>How full the hover gauge is, from 0 to 1 (drawn by the style).</summary>
        public static readonly DependencyProperty HoverProgressProperty = DependencyProperty.Register(
            nameof(HoverProgress), typeof(double), typeof(TileButton), new PropertyMetadata(0.0));

        /// <summary>Last cursor position seen on any TileButton, in screen pixels.</summary>
        private static Point _lastScreenPoint = new Point(double.NaN, double.NaN);

        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private bool _activatedThisVisit;

        public TileButton()
        {
            _timer.Tick += OnHoverDelayElapsed;
        }

        public double HoverProgress
        {
            get => (double)GetValue(HoverProgressProperty);
            set => SetValue(HoverProgressProperty, value);
        }

        /// <summary>true: activates again after each delay while the cursor stays on it.</summary>
        public bool RepeatOnHover { get; set; }

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

            if (!AppSettings.Current.HoverActivation || !moved || _activatedThisVisit || _timer.IsEnabled || !IsEnabled)
            {
                return;
            }
            StartDelay();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            StopDelay();
            _activatedThisVisit = false;
        }

        private void StartDelay()
        {
            TimeSpan delay = TimeSpan.FromMilliseconds(Math.Max(200, AppSettings.Current.HoverDelayMs));
            _timer.Interval = delay;
            _timer.Start();
            BeginAnimation(HoverProgressProperty, new DoubleAnimation(0, 1, delay));
        }

        private void StopDelay()
        {
            _timer.Stop();
            BeginAnimation(HoverProgressProperty, null);   // removes the animation…
            HoverProgress = 0;                             // …and empties the gauge
        }

        private void OnHoverDelayElapsed(object? sender, EventArgs e)
        {
            StopDelay();
            if (!IsMouseOver || !IsEnabled || !IsVisible)
            {
                return;
            }
            OnClick();
            if (RepeatOnHover && IsEnabled && IsMouseOver)
            {
                StartDelay();
            }
            else
            {
                _activatedThisVisit = true;
            }
        }
    }
}
