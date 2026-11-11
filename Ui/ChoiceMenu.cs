using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ludopol
{
    /// <summary>
    /// VIEW: a menu of big buttons that covers its page, for a value with too many choices
    /// for a loop (a list's category or compatibility filter, a game's rating…). A title,
    /// one button per choice (the current one in blue), and "Cancel". Choosing a button
    /// closes the menu and runs that choice.
    ///
    /// Put it last in the page's main Grid (so that it is drawn over the rest), hidden; call
    /// Show to open it.
    /// </summary>
    public sealed class ChoiceMenu : Grid
    {
        private readonly TextBlock _title = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(12, 0, 12, 24),
        };

        private readonly WrapPanel _choices = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };

        public ChoiceMenu()
        {
            Visibility = Visibility.Collapsed;
            SetResourceReference(BackgroundProperty, "OverlayBrush");
            _title.SetResourceReference(TextBlock.FontSizeProperty, "TitleFontSize");

            TileButton cancel = new TileButton
            {
                Content = Texts.Get("filter.menu.cancel"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 24, 0, 0),
            };
            cancel.SetResourceReference(WidthProperty, "WideButtonWidth");
            cancel.Click += (s, e) => Hide();

            Children.Add(new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _title, _choices, cancel },
            });
        }

        /// <summary>Opens the menu. After a choice, its Apply runs, then afterChoice (to save, redraw…).</summary>
        public void Show(string title, IEnumerable<MenuChoice> choices, Action afterChoice)
        {
            _title.Text = title;
            _choices.Children.Clear();
            foreach (MenuChoice choice in choices)
            {
                TileButton button = new TileButton { Content = choice.Content, IsSelected = choice.Selected };
                button.SetResourceReference(WidthProperty, "WideButtonWidth");
                button.Click += (s, e) =>
                {
                    Hide();
                    choice.Apply();
                    afterChoice();
                };
                _choices.Children.Add(button);
            }
            Visibility = Visibility.Visible;
        }

        public void Hide() => Visibility = Visibility.Collapsed;
    }

    /// <summary>One button of a ChoiceMenu: what it shows, whether it is the current value, what it sets.</summary>
    public sealed class MenuChoice
    {
        public MenuChoice(object content, bool selected, Action apply)
        {
            Content = content;
            Selected = selected;
            Apply = apply;
        }

        public object Content { get; }
        public bool Selected { get; }
        public Action Apply { get; }
    }
}
