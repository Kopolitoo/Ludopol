using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Opens the menu to choose several names (a game's categories): each button switches
        /// its name on (blue) or off; a field and "Add" create a new name; "Done" calls done
        /// with the names chosen. "Cancel" changes nothing.
        /// </summary>
        public void ShowSeveral(string title, IEnumerable<string> names, IEnumerable<string> chosen, Action<List<string>> done)
        {
            List<string> all = new List<string>(names);
            HashSet<string> selected = new HashSet<string>(chosen);
            foreach (string name in selected)
            {
                if (!all.Contains(name))
                {
                    all.Add(name);
                }
            }

            _title.Text = title;
            _choices.Children.Clear();
            foreach (string name in all)
            {
                AddToggle(name, selected);
            }

            // A new name: an ordinary field (Prunel's keyboard, Dragon) and "Add".
            TextBox newName = new TextBox { VerticalContentAlignment = VerticalAlignment.Center };
            newName.SetResourceReference(WidthProperty, "WideButtonWidth");
            TileButton add = new TileButton { Content = Texts.Get("menu.add") };
            add.SetResourceReference(WidthProperty, "WideButtonWidth");
            add.Click += (s, e) =>
            {
                string name = newName.Text.Trim();
                if (name.Length > 0 && !all.Contains(name))
                {
                    all.Add(name);
                    selected.Add(name);
                    // Before the field, with the other names.
                    _choices.Children.Insert(_choices.Children.IndexOf(newName), Toggle(name, selected));
                }
                newName.Text = "";
            };
            TileButton finished = new TileButton { Content = Texts.Get("menu.done") };
            finished.SetResourceReference(WidthProperty, "WideButtonWidth");
            finished.Click += (s, e) =>
            {
                Hide();
                done(all.Where(selected.Contains).ToList());
            };
            _choices.Children.Add(newName);
            _choices.Children.Add(add);
            _choices.Children.Add(finished);
            Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Opens the menu to type a text (a new name): an ordinary field (Prunel's keyboard,
        /// Dragon), and a button that calls done with the text, if it is not empty.
        /// </summary>
        public void ShowText(string title, string initial, string okText, Action<string> done)
        {
            _title.Text = title;
            _choices.Children.Clear();
            TextBox box = new TextBox { Text = initial, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(12) };
            box.SetResourceReference(MinHeightProperty, "SmallButtonSize");
            box.SetResourceReference(WidthProperty, "TextFieldWidth");
            TileButton ok = new TileButton { Content = okText };
            ok.SetResourceReference(WidthProperty, "WideButtonWidth");
            void Validate()
            {
                string text = box.Text.Trim();
                if (text.Length > 0)
                {
                    Hide();
                    done(text);
                }
            }
            ok.Click += (s, e) => Validate();
            box.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    Validate();
                }
            };
            _choices.Children.Add(box);
            _choices.Children.Add(ok);
            Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                System.Windows.Input.Keyboard.Focus(box);
                box.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void AddToggle(string name, HashSet<string> selected) => _choices.Children.Add(Toggle(name, selected));

        private static TileButton Toggle(string name, HashSet<string> selected)
        {
            TileButton button = new TileButton { Content = name, IsSelected = selected.Contains(name) };
            button.SetResourceReference(WidthProperty, "WideButtonWidth");
            button.SetResourceReference(Control.FontSizeProperty, "SmallFontSize");
            button.Click += (s, e) =>
            {
                if (!selected.Remove(name))
                {
                    selected.Add(name);
                }
                button.IsSelected = selected.Contains(name);
            };
            return button;
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
