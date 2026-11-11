using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Ludopol
{
    /// <summary>
    /// VIEW: the compatibility gauge, four segments from red to green.
    ///
    ///   Unknown                     all segments grey
    ///   None                        1 segment, red
    ///   Partial (switch, reflexes)  2 segments, orange
    ///   Partial (gaze, complex)     3 segments, yellow
    ///   Full                        4 segments, green
    ///   Presumed (not tried yet)    4 segments, blue-grey
    /// </summary>
    public class CompatibilityBar : UniformGrid
    {
        private static readonly Brush EmptyBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x45, 0x45, 0x4F)));

        public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
            nameof(Level), typeof(Compatibility), typeof(CompatibilityBar),
            new PropertyMetadata(Compatibility.Unknown, (d, e) => ((CompatibilityBar)d).Redraw()));

        public Compatibility Level
        {
            get => (Compatibility)GetValue(LevelProperty);
            set => SetValue(LevelProperty, value);
        }

        public CompatibilityBar()
        {
            Rows = 1;
            Columns = 4;
            for (int i = 0; i < 4; i++)
            {
                Children.Add(new Border { CornerRadius = new CornerRadius(3), Margin = new Thickness(2) });
            }
            Redraw();
        }

        /// <summary>The colour of a compatibility level (also used by other views).</summary>
        public static Brush BrushOf(Compatibility level)
        {
            switch (level)
            {
                case Compatibility.None: return Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0x45, 0x3D)));
                case Compatibility.PartialWithSwitch: return Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0x89, 0x2B)));
                case Compatibility.PartialGaze: return Freeze(new SolidColorBrush(Color.FromRgb(0xD8, 0xC2, 0x3A)));
                case Compatibility.Full: return Freeze(new SolidColorBrush(Color.FromRgb(0x3D, 0xAF, 0x5A)));
                case Compatibility.Presumed: return Freeze(new SolidColorBrush(Color.FromRgb(0x5B, 0x7D, 0xB1)));
                default: return EmptyBrush;
            }
        }

        private void Redraw()
        {
            // Unknown = 0, None = 1 … Full = 4: the number of coloured segments. Presumed: all, in blue-grey.
            int filled = Level == Compatibility.Presumed ? Children.Count : (int)Level;
            Brush brush = BrushOf(Level);
            for (int i = 0; i < Children.Count; i++)
            {
                ((Border)Children[i]).Background = i < filled ? brush : EmptyBrush;
            }
        }

        private static Brush Freeze(Brush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
