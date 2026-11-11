using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Ludopol
{
    /// <summary>
    /// VIEW: a row of big buttons, one per possible value, the current value in blue.
    /// Used instead of a drop-down list, which is too small for the gaze
    /// (game page: rating, compatibility…; settings page).
    /// </summary>
    internal static class ChoiceRow
    {
        /// <summary>
        /// Makes the row. Choosing a value calls onChoose with it; the caller saves it and
        /// redraws its page, so that the new value shows in blue.
        /// </summary>
        public static WrapPanel Create<T>(IEnumerable<T> values, Func<T, string> label, T current, Action<T> onChoose)
        {
            WrapPanel row = new WrapPanel();
            foreach (T value in values)
            {
                TileButton button = new TileButton
                {
                    Content = label(value),
                    IsSelected = EqualityComparer<T>.Default.Equals(value, current),
                };
                button.SetResourceReference(FrameworkElement.MaxWidthProperty, "WideButtonWidth");
                button.SetResourceReference(Control.FontSizeProperty, "SmallFontSize");
                button.Click += (s, e) => onChoose(value);
                row.Children.Add(button);
            }
            return row;
        }
    }
}
