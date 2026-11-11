using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Ludopol
{
    /// <summary>
    /// VIEW: the page of one game.
    ///
    /// The right part is built in code (BuildInfo), section by section. Each choice
    /// (compatibility, owned on Steam, tried, rating, to publish) is a row of big buttons; the chosen one is
    /// blue. A choice is saved in the database at once. "My notes" is an ordinary text field
    /// (Prunel's keyboard, Dragon): it is saved when the page is left.
    /// </summary>
    public partial class GameDetailsPage : UserControl, IPage
    {
        private readonly long _gameId;
        private Game? _game;
        private TextBox? _notesBox;

        public GameDetailsPage(long gameId)
        {
            InitializeComponent();
            _gameId = gameId;
        }

        public string Title => _game?.Name ?? "";

        public UIElement? SideContent => null;

        public void OnShown()
        {
            _game = App.Database.GetGame(_gameId);
            if (_game == null)
            {
                // Should not happen: the game was deleted in the meantime.
                MainWindow.Current.GoBack();
                return;
            }
            ShowImage(_game);
            BuildInfo(_game);
        }

        /// <summary>Saves my notes if they changed.</summary>
        public void OnLeaving()
        {
            if (_game == null || _notesBox == null)
            {
                return;
            }
            string? notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim();
            if (notes != _game.Notes)
            {
                _game.Notes = notes;
                App.Database.SaveUserFields(_game);
            }
        }

        private void ShowImage(Game game)
        {
            InitialsText.Text = game.Initials;
            GameImage.Source = null;
            if (string.IsNullOrEmpty(game.ImageUrl))
            {
                return;
            }
            try
            {
                GameImage.Source = new BitmapImage(new Uri(game.ImageUrl));
            }
            catch (Exception ex)
            {
                // A bad address only hides the image: the letters stay visible.
                App.Log("Image not shown for \"" + game.Name + "\": " + ex.Message);
            }
        }

        private void BuildInfo(Game game)
        {
            Info.Children.Clear();

            // --- What Steam says about the game ---
            AddLine(string.Join("  ·  ", NonEmpty(game.Developer, game.ReleaseDate, game.Price)), muted: true);
            AddLine(game.Description ?? Texts.Get("details.noDescription"));
            if (game.Categories.Count > 0)
            {
                AddTitle("details.categories");
                AddLine(string.Join("  ·  ", game.Categories));
            }
            if (game.Tags.Count > 0)
            {
                AddTitle("details.tags");
                AddLine(string.Join("  ·  ", game.Tags));
            }

            // --- What I note ---
            AddTitle("details.compatibility");
            Info.Children.Add(new CompatibilityBar { Level = game.Compatibility, Height = 24, Margin = new Thickness(12, 12, 12, 0) });
            AddChoices(new[]
            {
                Compatibility.Unknown, Compatibility.None, Compatibility.PartialWithSwitch,
                Compatibility.PartialGaze, Compatibility.Full,
            }, CompatibilityTexts.Name, game.Compatibility, value => game.Compatibility = value);

            AddTitle("details.ownedOnSteam");
            AddChoices(new[] { true, false }, YesNo, game.OwnedOnSteam, value => game.OwnedOnSteam = value);

            AddTitle("details.tried");
            AddChoices(new[] { true, false }, YesNo, game.Tried, value => game.Tried = value);

            AddTitle("details.rating");
            AddChoices(new int?[] { null, 1, 2, 3, 4, 5 },
                value => value?.ToString() ?? Texts.Get("details.rating.none"), game.Rating, value => game.Rating = value);

            AddTitle("details.toPublish");
            AddChoices(new[] { true, false }, YesNo, game.ToPublish, value => game.ToPublish = value);

            AddTitle("details.notes");
            _notesBox = new TextBox
            {
                Text = game.Notes ?? "",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = (double)FindResource("ButtonMinSize") * 1.5,
            };
            Info.Children.Add(_notesBox);
        }

        /// <summary>
        /// A row of big buttons, one per possible value; the current value is blue.
        /// Choosing a value saves it and redraws the page.
        /// </summary>
        private void AddChoices<T>(IEnumerable<T> values, Func<T, string> label, T current, Action<T> apply)
        {
            WrapPanel row = new WrapPanel();
            foreach (T value in values)
            {
                TileButton button = new TileButton
                {
                    Content = label(value),
                    IsSelected = EqualityComparer<T>.Default.Equals(value, current),
                    MaxWidth = (double)FindResource("WideButtonWidth"),
                    FontSize = (double)FindResource("SmallFontSize"),
                };
                button.Click += (s, e) =>
                {
                    if (_game == null)
                    {
                        return;
                    }
                    apply(value);
                    OnLeaving();   // keeps the notes typed so far
                    App.Database.SaveUserFields(_game);
                    double offset = Scroll.Viewer.VerticalOffset;
                    BuildInfo(_game);
                    Scroll.Viewer.ScrollToVerticalOffset(offset);
                };
                row.Children.Add(button);
            }
            Info.Children.Add(row);
        }

        private void AddTitle(string key)
        {
            Info.Children.Add(new TextBlock { Text = Texts.Get(key), Style = (Style)FindResource("SectionTitle") });
        }

        private void AddLine(string text, bool muted = false)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            TextBlock block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 12, 12, 0) };
            if (muted)
            {
                block.Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush");
            }
            Info.Children.Add(block);
        }

        private static string YesNo(bool value) => Texts.Get(value ? "common.yes" : "common.no");

        private static IEnumerable<string> NonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }
        }
    }
}
