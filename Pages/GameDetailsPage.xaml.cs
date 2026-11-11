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
    /// Under the image (BuildActions): "Play" when the game is installed (with a confirmation,
    /// see PlayPanel), otherwise "Install with Steam" for a Steam game; and "Choose the
    /// program" for a game outside Steam, or to start a Steam game another way. Whether the
    /// game is installed is checked again each time the page is shown and each time Ludopol
    /// comes back to the front (after an installation in Steam, for example).
    ///
    /// The right part is built in code (BuildInfo), section by section. What I note fits on
    /// one row: compatibility and rating open a menu (Ui/ChoiceMenu.cs), owned on Steam,
    /// tried and to publish switch at each activation (blue = yes). A change is saved in the
    /// database at once. "My notes" is an ordinary text field
    /// (Prunel's keyboard, Dragon): it is saved when the page is left.
    /// </summary>
    public partial class GameDetailsPage : UserControl, IPage
    {
        private readonly long _gameId;
        private Game? _game;
        private TextBox? _notesBox;

        /// <summary>What happened after a button under the image (launch error…), shown under them.</summary>
        private string? _actionMessage;

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
            CheckInstalled();
            BuildActions(_game);
            BuildInfo(_game);
            Application.Current.Activated += OnAppActivated;
        }

        /// <summary>Saves my notes if they changed.</summary>
        public void OnLeaving()
        {
            Application.Current.Activated -= OnAppActivated;
            PlayPanel.Visibility = Visibility.Collapsed;
            ChoiceMenu.Hide();
            _actionMessage = null;
            SaveNotes();
        }

        private void SaveNotes()
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

        // ----- Play, install, choose the program -----

        /// <summary>Ludopol is back in front: the game may have been installed or removed meanwhile.</summary>
        private void OnAppActivated(object? sender, EventArgs e)
        {
            if (_game == null)
            {
                return;
            }
            bool before = _game.Installed;
            CheckInstalled();
            if (_game.Installed != before)
            {
                _actionMessage = null;
                BuildActions(_game);
            }
        }

        private void CheckInstalled()
        {
            if (_game == null)
            {
                return;
            }
            try
            {
                GameLauncher.Refresh(_game, App.Database);
            }
            catch (Exception ex)
            {
                App.Log("Installation of \"" + _game.Name + "\" not checked: " + ex.Message);
            }
        }

        private void BuildActions(Game game)
        {
            Actions.Children.Clear();

            if (game.Installed)
            {
                TileButton play = AddAction("details.play");
                play.SetResourceReference(FontSizeProperty, "TitleFontSize");
                play.Click += (s, e) =>
                {
                    PlayQuestion.Text = Texts.Format("play.question", game.Name);
                    PlayPanel.Visibility = Visibility.Visible;
                };
            }
            else
            {
                AddActionText(Texts.Get("details.notInstalled"), muted: true);
                if (game.SteamAppId.HasValue)
                {
                    TileButton install = AddAction("details.installWithSteam");
                    install.Click += (s, e) => RunAction(() =>
                    {
                        GameLauncher.InstallWithSteam(game);
                        _actionMessage = Texts.Get("details.installStarted");
                    });
                }
            }

            TileButton choose = AddAction("details.chooseProgram");
            choose.SetResourceReference(FontSizeProperty, "SmallFontSize");
            choose.Click += (s, e) => ChooseProgram(game);
            if (!string.IsNullOrEmpty(game.LaunchTarget))
            {
                AddActionText(Texts.Format("details.program", game.LaunchTarget), muted: true);
                TileButton forget = AddAction("details.forgetProgram");
                forget.SetResourceReference(FontSizeProperty, "SmallFontSize");
                forget.Click += (s, e) => SetProgram(game, null);
            }

            if (_actionMessage != null)
            {
                AddActionText(_actionMessage, muted: false);
            }
        }

        private void OnPlayConfirmClick(object sender, RoutedEventArgs e)
        {
            PlayPanel.Visibility = Visibility.Collapsed;
            if (_game != null)
            {
                Game game = _game;
                RunAction(() =>
                {
                    GameLauncher.Play(game);
                    _actionMessage = Texts.Get("play.started");
                });
            }
        }

        private void OnPlayCancelClick(object sender, RoutedEventArgs e)
        {
            PlayPanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>Runs a button's action; an error is logged and shown under the buttons.</summary>
        private void RunAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                App.Log("Action failed for \"" + _game?.Name + "\": " + ex);
                _actionMessage = Texts.Format("play.error", ex.Message);
            }
            if (_game != null)
            {
                BuildActions(_game);
            }
        }

        /// <summary>The Windows file picker, to choose the game's program (.exe) or a shortcut to it.</summary>
        private void ChooseProgram(Game game)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = Texts.Get("details.chooseProgram"),
                Filter = Texts.Get("details.programFilter") + " (*.exe, *.lnk, *.url)|*.exe;*.lnk;*.url",
                // Keeps a shortcut as it is: it may carry options for the game.
                DereferenceLinks = false,
            };
            string? folder = System.IO.Path.GetDirectoryName(game.LaunchTarget ?? "");
            if (!string.IsNullOrEmpty(folder) && System.IO.Directory.Exists(folder))
            {
                dialog.InitialDirectory = folder;
            }
            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            {
                SetProgram(game, dialog.FileName);
            }
        }

        private void SetProgram(Game game, string? program)
        {
            game.LaunchTarget = program;
            _actionMessage = null;
            App.Database.SaveInstallation(game);
            CheckInstalled();   // saves "installed" again if it changed
            BuildActions(game);
        }

        private TileButton AddAction(string textKey)
        {
            TileButton button = new TileButton { Content = Texts.Get(textKey) };
            Actions.Children.Add(button);
            return button;
        }

        private void AddActionText(string text, bool muted)
        {
            TextBlock block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 12, 12, 0) };
            if (muted)
            {
                block.Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush");
            }
            Actions.Children.Add(block);
        }

        // ----- The right part -----

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
            AddLine(string.Join("  ·  ", NonEmpty(game.ReleaseDate, game.Price)), muted: true);
            AddLine(game.Description ?? Texts.Get("details.noDescription"));
            // Categories, tags and developer are buttons: each one opens the list of its games.
            if (game.Categories.Count > 0)
            {
                AddTitle("details.categories");
                AddLinks(game.Categories, name => new GameFilter { Category = name });
            }
            if (game.Tags.Count > 0)
            {
                AddTitle("details.tags");
                AddLinks(game.Tags, name => new GameFilter { Tag = name });
            }
            if (!string.IsNullOrWhiteSpace(game.Developer))
            {
                AddTitle("details.developer");
                AddLinks(new[] { game.Developer }, name => new GameFilter { Developer = name });
            }

            // --- What I note: one row of buttons ---
            // Yes/no fields switch at each activation (blue = yes); compatibility and rating
            // open a menu (Ui/ChoiceMenu.cs).
            AddTitle("details.mine");
            WrapPanel row = new WrapPanel();
            row.Children.Add(MenuButton(new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = Field("details.compatibility", CompatibilityTexts.Name(game.Compatibility)),
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                    },
                    new CompatibilityBar { Level = game.Compatibility, Height = 16, Margin = new Thickness(0, 10, 0, 0) },
                },
            }, ShowCompatibilityMenu));
            row.Children.Add(MenuButton(
                Field("details.rating", game.Rating?.ToString() ?? Texts.Get("details.rating.none")), ShowRatingMenu));
            row.Children.Add(Toggle("details.ownedOnSteam", game.OwnedOnSteam, () => game.OwnedOnSteam = !game.OwnedOnSteam));
            row.Children.Add(Toggle("details.tried", game.Tried, () => game.Tried = !game.Tried));
            row.Children.Add(Toggle("details.toPublish", game.ToPublish, () => game.ToPublish = !game.ToPublish));
            Info.Children.Add(row);

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

        /// <summary>A yes/no field: one button, blue when yes; each activation switches it.</summary>
        private TileButton Toggle(string nameKey, bool value, Action change)
        {
            TileButton button = MenuButton(Field(nameKey, YesNo(value)), () => SaveChange(change));
            button.IsSelected = value;
            return button;
        }

        private TileButton MenuButton(object content, Action onClick)
        {
            TileButton button = new TileButton { Content = content };
            button.SetResourceReference(WidthProperty, "WideButtonWidth");
            button.SetResourceReference(FontSizeProperty, "SmallFontSize");
            button.Click += (s, e) => onClick();
            return button;
        }

        private void ShowCompatibilityMenu()
        {
            if (_game == null)
            {
                return;
            }
            Game game = _game;
            List<MenuChoice> choices = new List<MenuChoice>();
            foreach (Compatibility level in new[]
            {
                Compatibility.Full, Compatibility.PartialGaze, Compatibility.PartialWithSwitch,
                Compatibility.Presumed, Compatibility.Unknown, Compatibility.None,
            })
            {
                StackPanel content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = CompatibilityTexts.Name(level), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
                        new CompatibilityBar { Level = level, Height = 16, Margin = new Thickness(0, 10, 0, 0) },
                    },
                };
                choices.Add(new MenuChoice(content, level == game.Compatibility, () => game.Compatibility = level));
            }
            ChoiceMenu.Show(Texts.Get("details.compatibility"), choices, () => SaveChange(() => { }));
        }

        private void ShowRatingMenu()
        {
            if (_game == null)
            {
                return;
            }
            Game game = _game;
            List<MenuChoice> choices = new List<MenuChoice>();
            foreach (int? rating in new int?[] { null, 1, 2, 3, 4, 5 })
            {
                choices.Add(new MenuChoice(rating?.ToString() ?? Texts.Get("details.rating.none"),
                    rating == game.Rating, () => game.Rating = rating));
            }
            ChoiceMenu.Show(Texts.Get("details.rating"), choices, () => SaveChange(() => { }));
        }

        /// <summary>Changes one of my fields, saves it at once and redraws the page.</summary>
        private void SaveChange(Action change)
        {
            if (_game == null)
            {
                return;
            }
            change();
            SaveNotes();   // keeps the notes typed so far
            App.Database.SaveUserFields(_game);
            double offset = Scroll.Viewer.VerticalOffset;
            BuildInfo(_game);
            Scroll.Viewer.ScrollToVerticalOffset(offset);
        }

        /// <summary>"Name : value", in the interface language.</summary>
        private static string Field(string nameKey, string value) => Texts.Format("details.field", Texts.Get(nameKey), value);

        /// <summary>A row of buttons, one per name; each opens the list of the games with that name.</summary>
        private void AddLinks(IEnumerable<string> names, Func<string, GameFilter> filter)
        {
            WrapPanel row = new WrapPanel();
            foreach (string name in names)
            {
                TileButton button = new TileButton { Content = name };
                button.SetResourceReference(MaxWidthProperty, "WideButtonWidth");
                button.SetResourceReference(FontSizeProperty, "SmallFontSize");
                button.Click += (s, e) => MainWindow.Current.Navigate(new GameListPage(filter(name), name));
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
