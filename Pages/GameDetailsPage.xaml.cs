using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ludopol
{
    /// <summary>
    /// VIEW: the page of one game.
    ///
    /// Under the image (BuildActions): "Play" when the game is installed (with a confirmation,
    /// see PlayPanel), otherwise "Install with Steam" for a game bought on Steam; "See the
    /// game in the Steam store" for every game on Steam; and "Choose the program" for a game
    /// not bought on Steam. Whether the
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
            CompleteFromSteam(_game);
        }

        public void OnLeaving()
        {
            Application.Current.Activated -= OnAppActivated;
            PlayPanel.Visibility = Visibility.Collapsed;
            ChoiceMenu.Hide();
            CloseSteamLink();
            _actionMessage = null;
        }

        /// <summary>
        /// A Steam game without a description (added from Steam): its description, tags… are
        /// fetched from the Steam store, and the page is redrawn when they arrive.
        /// </summary>
        private async void CompleteFromSteam(Game game)
        {
            if (!SteamStore.NeedsDetails(game))
            {
                return;
            }
            try
            {
                if (await SteamStore.CompleteAsync(game, App.Database) && _game == game && IsLoaded)
                {
                    ShowImage(game);
                    double offset = Scroll.Viewer.VerticalOffset;
                    BuildInfo(game);
                    Scroll.Viewer.ScrollToVerticalOffset(offset);
                }
            }
            catch (Exception ex)
            {
                App.Log("Steam store: \"" + game.Name + "\" not completed: " + ex.Message);
            }
        }

        /// <summary>"Complete from Steam": asks the store now, then says what happened under the buttons.</summary>
        private async void CompleteFromSteamNow(Game game)
        {
            _actionMessage = Texts.Get("details.completing");
            BuildActions(game);
            try
            {
                bool changed = await SteamStore.CompleteAsync(game, App.Database);
                _actionMessage = Texts.Get(changed ? "details.completed" : "details.nothingToComplete");
            }
            catch (Exception ex)
            {
                App.Log("Steam store: \"" + game.Name + "\" not completed: " + ex.Message);
                _actionMessage = Texts.Format("play.error", ex.Message);
            }
            if (_game != game || !IsLoaded)
            {
                return;
            }
            ShowImage(game);
            BuildActions(game);
            double offset = Scroll.Viewer.VerticalOffset;
            BuildInfo(game);
            Scroll.Viewer.ScrollToVerticalOffset(offset);
        }

        // ----- The Steam page typed by hand -----

        /// <summary>The panel over the page where the Steam address is typed, or null.</summary>
        private Grid? _steamLinkPanel;

        /// <summary>
        /// Asks for the game's Steam address (or its number), with Prunel's keyboard, Dragon or
        /// a paste. Then the game takes that number and its page is filled from the store.
        /// </summary>
        private void ShowSteamLink(Game game)
        {
            CloseSteamLink();
            TextBox box = new TextBox
            {
                Text = game.SteamAppId.HasValue
                    ? "https://store.steampowered.com/app/" + game.SteamAppId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/"
                    : "",
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12),
            };
            box.SetResourceReference(MinHeightProperty, "SmallButtonSize");
            TextBlock message = new TextBlock { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(12) };

            TileButton ok = new TileButton { Content = Texts.Get("details.steamLink.ok") };
            TileButton cancel = new TileButton { Content = Texts.Get("filter.menu.cancel") };
            ok.SetResourceReference(WidthProperty, "WideButtonWidth");
            cancel.SetResourceReference(WidthProperty, "WideButtonWidth");
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            TextBlock question = new TextBlock
            {
                Text = Texts.Format("details.steamLink.question", game.Name),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(12),
            };
            question.SetResourceReference(TextBlock.FontSizeProperty, "TitleFontSize");
            TextBlock hint = new TextBlock
            {
                Text = Texts.Get("details.steamLink.hint"),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(12, 0, 12, 0),
            };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");

            StackPanel content = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 1600, Margin = new Thickness(24) };
            content.Children.Add(question);
            content.Children.Add(hint);
            content.Children.Add(box);
            content.Children.Add(message);
            content.Children.Add(buttons);
            _steamLinkPanel = new Grid { Children = { content } };
            _steamLinkPanel.SetResourceReference(Panel.BackgroundProperty, "OverlayBrush");
            ((Grid)Content).Children.Add(_steamLinkPanel);

            void Validate()
            {
                long? appId = SteamStore.ParseAppId(box.Text);
                if (appId == null)
                {
                    message.Text = Texts.Get("details.steamLink.invalid");
                    return;
                }
                CloseSteamLink();
                SteamStore.ChangeAppId(game, appId.Value, App.Database);
                ShowImage(game);
                CheckInstalled();
                CompleteFromSteamNow(game);   // fills the page from the store, then redraws it
            }
            ok.Click += (s, e) => Validate();
            cancel.Click += (s, e) => CloseSteamLink();
            box.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    Validate();
                }
            };
            // Ready for Prunel's keyboard, Dragon or a paste, the text selected to be replaced.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                System.Windows.Input.Keyboard.Focus(box);
                box.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void CloseSteamLink()
        {
            if (_steamLinkPanel != null)
            {
                ((Grid)Content).Children.Remove(_steamLinkPanel);
                _steamLinkPanel = null;
            }
        }

        // ----- The game's image -----

        private void ShowImageMenu(Game game)
        {
            List<MenuChoice> choices = new List<MenuChoice>
            {
                new MenuChoice(Texts.Get("details.image.file"), false, () => ChooseImageFile(game)),
                new MenuChoice(Texts.Get("details.image.address"), false, () =>
                    ChoiceMenu.ShowText(Texts.Format("details.image.addressTitle", game.Name),
                        game.ImageUrl != null && game.ImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? game.ImageUrl : "",
                        Texts.Get("details.image.use"), address =>
                        {
                            if (Uri.TryCreate(address, UriKind.Absolute, out Uri? uri)
                                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                            {
                                SetImage(game, uri.ToString());
                            }
                        })),
            };
            if (game.SteamAppId.HasValue)
            {
                choices.Add(new MenuChoice(Texts.Get("details.image.steam"), false, () => SetImage(game,
                    "https://cdn.cloudflare.steamstatic.com/steam/apps/"
                    + game.SteamAppId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/header.jpg")));
            }
            ChoiceMenu.Show(Texts.Get("details.image.change"), choices, () => { });
        }

        /// <summary>
        /// An image file of mine: copied into Ludopol's folder (Images), so that it stays even
        /// if the original moves. A new name each time: Windows would otherwise show the old one.
        /// </summary>
        private void ChooseImageFile(Game game)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = Texts.Get("details.image.file"),
                Filter = Texts.Get("details.image.filter") + " (*.png, *.jpg, *.jpeg, *.bmp, *.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            {
                return;
            }
            try
            {
                string folder = System.IO.Path.Combine(App.DataFolder, "Images");
                System.IO.Directory.CreateDirectory(folder);
                string prefix = "game-" + game.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-";
                foreach (string old in System.IO.Directory.GetFiles(folder, prefix + "*"))
                {
                    System.IO.File.Delete(old);
                }
                string copy = System.IO.Path.Combine(folder, prefix + DateTime.Now.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + System.IO.Path.GetExtension(dialog.FileName).ToLowerInvariant());
                System.IO.File.Copy(dialog.FileName, copy);
                SetImage(game, copy);
            }
            catch (Exception ex)
            {
                App.Log("Image not copied for \"" + game.Name + "\": " + ex);
                _actionMessage = Texts.Format("play.error", ex.Message);
                BuildActions(game);
            }
        }

        private void SetImage(Game game, string image)
        {
            game.ImageUrl = image;
            App.Database.UpdateFromImport(game);
            App.Log("Image of \"" + game.Name + "\": " + image);
            ShowImage(game);
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
                if (game.SteamAppId.HasValue && game.OwnedOnSteam)
                {
                    TileButton install = AddAction("details.installWithSteam", SteamIconMark);
                    install.Click += (s, e) => RunAction(() =>
                    {
                        GameLauncher.InstallWithSteam(game);
                        _actionMessage = Texts.Get("details.installStarted");
                    });
                }
            }

            // A game on Steam: its store page (to buy it, or to read about it).
            if (game.SteamAppId.HasValue)
            {
                TileButton store = AddAction("details.steamStore", SteamIconMark);
                store.Click += (s, e) => RunAction(() => GameLauncher.OpenSteamStore(game));

                // Fills what is missing (description, tags…) from the Steam store; replaces nothing.
                TileButton complete = AddAction("details.completeFromSteam", SteamIconMark);
                complete.Click += (s, e) => CompleteFromSteamNow(game);
            }

            // The Steam page typed by hand: for a game not found on Steam, or linked to the
            // wrong page.
            TileButton link = AddAction(game.SteamAppId.HasValue ? "details.steamLink.change" : "details.steamLink.add", SteamIconMark, "🌐");
            link.Click += (s, e) => ShowSteamLink(game);

            // The image: a file of mine, an Internet address, or Steam's.
            TileButton image = AddAction("details.image.change", "🖼");
            image.Click += (s, e) => ShowImageMenu(game);

            // Choosing a program is for games outside Steam: not offered for a game bought on
            // Steam (a program already chosen can still be removed).
            if (!game.OwnedOnSteam)
            {
                TileButton choose = AddAction("details.chooseProgram");
                choose.Click += (s, e) => ChooseProgram(game);
            }
            if (!string.IsNullOrEmpty(game.LaunchTarget))
            {
                AddActionText(Texts.Format("details.program", game.LaunchTarget), muted: true);
                TileButton forget = AddAction("details.forgetProgram");
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

        /// <summary>The Steam icon, to put before a button's text.</summary>
        private const string SteamIconMark = "steam";

        /// <summary>
        /// A button under the image: half height, its text after small icons (SteamIconMark for
        /// Steam's logo, or a symbol such as "🌐"). "Play" stays a big button.
        /// </summary>
        private TileButton AddAction(string textKey, params string[] icons)
        {
            TileButton button = new TileButton();
            if (textKey == "details.play")
            {
                button.Content = Texts.Get(textKey);
            }
            else
            {
                StackPanel content = new StackPanel { Orientation = Orientation.Horizontal };
                double size = (double)FindResource("SmallFontSize") * 1.2;
                foreach (string icon in icons)
                {
                    UIElement element = icon == SteamIconMark
                        ? SteamIcon(size)
                        : new TextBlock { Text = icon, FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol"), VerticalAlignment = VerticalAlignment.Center };
                    ((FrameworkElement)element).Margin = new Thickness(0, 0, 8, 0);
                    content.Children.Add(element);
                }
                content.Children.Add(new TextBlock { Text = Texts.Get(textKey), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
                button.Content = content;
                button.SetResourceReference(MinHeightProperty, "SmallButtonSize");
                button.SetResourceReference(FontSizeProperty, "SmallFontSize");
                button.Padding = new Thickness(8, 4, 8, 4);
                button.Margin = new Thickness(0, 4, 0, 4);
            }
            Actions.Children.Add(button);
            return button;
        }

        /// <summary>Steam's logo, read from the Steam site like the games' images; the word "Steam" without Internet.</summary>
        private static UIElement SteamIcon(double size)
        {
            ContentControl holder = new ContentControl { Focusable = false, VerticalAlignment = VerticalAlignment.Center };
            TextBlock word = new TextBlock { Text = "Steam", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            try
            {
                BitmapImage logo = new BitmapImage(new Uri("https://store.steampowered.com/favicon.ico"));
                logo.DownloadFailed += (s, e) => holder.Content = word;
                holder.Content = new Image { Source = logo, Width = size, Height = size };
            }
            catch (Exception ex)
            {
                App.Log("Steam logo not shown: " + ex.Message);
                holder.Content = word;
            }
            return holder;
        }

        private void AddActionText(string text, bool muted)
        {
            TextBlock block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 12, 12, 0) };
            if (muted)
            {
                block.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
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

        /// <summary>
        /// The right part, made to fit on one screen: small buttons, each name of a row
        /// written at its start rather than as a title above it, yes/no fields as switches.
        /// </summary>
        private void BuildInfo(Game game)
        {
            Info.Children.Clear();

            // --- What Steam says about the game ---
            // First line: date, price and, for a game I own on Steam, Steam's logo with a green
            // tick (only information: "owned" is updated by the synchronisation, in the settings).
            WrapPanel top = new WrapPanel { Margin = new Thickness(12, 6, 12, 0) };
            // No price for a game I own: it costs me nothing more.
            string dateAndPrice = string.Join("  ·  ", NonEmpty(game.ReleaseDate, game.OwnedOnSteam ? null : game.Price));
            if (dateAndPrice.Length > 0)
            {
                TextBlock line = new TextBlock { Text = dateAndPrice, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
                line.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
                top.Children.Add(line);
            }
            if (game.OwnedOnSteam)
            {
                top.Children.Add(SteamBadge());
            }
            if (top.Children.Count > 0)
            {
                Info.Children.Add(top);
            }
            AddLine(game.Description ?? Texts.Get("details.noDescription"));

            // Categories, tags and developer are buttons: each one opens the list of its games.
            WrapPanel categories = LinkRow("details.categories", game.Categories, name => new GameFilter { Category = name });
            categories.Children.Add(SmallButton(Texts.Get("details.editCategories"), EditCategories));
            if (game.Tags.Count > 0)
            {
                LinkRow("details.tags", game.Tags, name => new GameFilter { Tag = name });
            }
            if (!string.IsNullOrWhiteSpace(game.Developer))
            {
                // The developer's button gives its number of games: "Nicalis (4)".
                WrapPanel developer = LabelRow("details.developer");
                string name = game.Developer;
                int count = App.Database.GetGames(new GameFilter { Developer = name }).Count;
                developer.Children.Add(SmallButton(Texts.Format("list.titleCount", name, count),
                    () => MainWindow.Current.Navigate(new GameListPage(new GameFilter { Developer = name }, name))));
            }

            // --- What I note: one row ---
            // Compatibility and rating open a menu (Ui/ChoiceMenu.cs); tried and to publish
            // are switches, changed at each activation.
            WrapPanel row = LabelRow("details.mine");
            TileButton compatibility = SmallButton(new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = Field("details.compatibility", CompatibilityTexts.Name(game.Compatibility)),
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                    },
                    new CompatibilityBar { Level = game.Compatibility, Height = 12, Margin = new Thickness(0, 6, 0, 0) },
                },
            }, ShowCompatibilityMenu);
            compatibility.SetResourceReference(WidthProperty, "WideButtonWidth");
            row.Children.Add(compatibility);
            row.Children.Add(SmallButton(
                Field("details.rating", game.Rating?.ToString() ?? Texts.Get("details.rating.none")), ShowRatingMenu));
            row.Children.Add(Switch("details.tried", game.Tried, () => game.Tried = !game.Tried));
            row.Children.Add(Switch("details.toPublish", game.ToPublish, () => game.ToPublish = !game.ToPublish));

            // No text field on this page: it would take the keyboard (shortcuts) and Dragon
            // cannot dictate into a WPF field. The notes are written on their own page
            // (NotesPage), in a Windows field Dragon dictates into.
            WrapPanel notes = LabelRow("details.notes");
            notes.Children.Add(SmallButton(Texts.Get("details.editNotes"),
                () => MainWindow.Current.Navigate(new NotesPage(game.Id))));
            AddLine(string.IsNullOrWhiteSpace(game.Notes) ? Texts.Get("details.notes.none") : game.Notes!,
                muted: string.IsNullOrWhiteSpace(game.Notes));
        }

        /// <summary>
        /// Steam's logo (read from the Steam site, like the games' images) with a green tick.
        /// Without Internet, the word "Steam" replaces the logo.
        /// </summary>
        private UIElement SteamBadge()
        {
            StackPanel badge = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            badge.Children.Add(SteamIcon((double)FindResource("BaseFontSize") * 1.2));
            badge.Children.Add(new TextBlock
            {
                Text = "✔",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x3B, 0xB5, 0x4A)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
            });
            badge.ToolTip = Texts.Get("details.ownedOnSteam");
            return badge;
        }

        /// <summary>A yes/no field as a switch: a track with a knob (on the right and blue when yes), then its name.</summary>
        private TileButton Switch(string nameKey, bool value, Action change)
        {
            Border knob = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = Brushes.White,
                Margin = new Thickness(3),
                HorizontalAlignment = value ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            };
            Border track = new Border { Width = 52, Height = 26, CornerRadius = new CornerRadius(13), Child = knob, VerticalAlignment = VerticalAlignment.Center };
            track.SetResourceReference(Border.BackgroundProperty, value ? "AccentBrush" : "GaugeEmptyBrush");
            StackPanel content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(track);
            content.Children.Add(new TextBlock { Text = Texts.Get(nameKey), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
            return SmallButton(content, () => SaveChange(change));
        }

        /// <summary>A half-size button (the links and my fields of the right part).</summary>
        private static TileButton SmallButton(object content, Action onClick)
        {
            TileButton button = new TileButton { Content = content, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(6) };
            button.SetResourceReference(MinWidthProperty, "SmallButtonSize");
            button.SetResourceReference(MinHeightProperty, "SmallButtonSize");
            button.SetResourceReference(MaxWidthProperty, "WideButtonWidth");
            button.SetResourceReference(FontSizeProperty, "SmallFontSize");
            button.Click += (s, e) => onClick();
            return button;
        }

        /// <summary>A row starting with its name ("Tags :"), added to the page; the caller adds the buttons.</summary>
        private WrapPanel LabelRow(string nameKey)
        {
            WrapPanel row = new WrapPanel { Margin = new Thickness(6, 6, 6, 0) };
            row.Children.Add(new TextBlock
            {
                Text = Texts.Format("details.label", Texts.Get(nameKey)),
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0),
            });
            Info.Children.Add(row);
            return row;
        }

        /// <summary>A row of half-size buttons, one per name; each opens the list of the games with that name.</summary>
        private WrapPanel LinkRow(string nameKey, IEnumerable<string> names, Func<string, GameFilter> filter)
        {
            WrapPanel row = LabelRow(nameKey);
            foreach (string name in names)
            {
                row.Children.Add(SmallButton(name, () => MainWindow.Current.Navigate(new GameListPage(filter(name), name))));
            }
            return row;
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

        /// <summary>
        /// Choosing the game's categories among those of the catalogue (or a new one). Choosing
        /// a real category takes the game out of "To sort (Steam)".
        /// </summary>
        private void EditCategories()
        {
            if (_game == null)
            {
                return;
            }
            Game game = _game;
            string toSort = Texts.Get("steamSync.newCategory");
            List<string> names = App.Database.GetCategories().Select(c => c.Name).ToList();
            ChoiceMenu.ShowSeveral(Texts.Get("details.editCategories"), names, game.Categories, chosen =>
            {
                if (chosen.Any(c => c != toSort))
                {
                    chosen.Remove(toSort);
                }
                SaveCategories(game, chosen);
            });
        }

        private void SaveCategories(Game game, List<string> categories)
        {
            game.Categories = categories;
            App.Database.UpdateFromImport(game);   // saves the game with its categories and tags
            double offset = Scroll.Viewer.VerticalOffset;
            BuildInfo(game);
            Scroll.Viewer.ScrollToVerticalOffset(offset);
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
            App.Database.SaveUserFields(_game);
            double offset = Scroll.Viewer.VerticalOffset;
            BuildInfo(_game);
            Scroll.Viewer.ScrollToVerticalOffset(offset);
        }

        /// <summary>"Name : value", in the interface language.</summary>
        private static string Field(string nameKey, string value) => Texts.Format("details.field", Texts.Get(nameKey), value);

        private void AddLine(string text, bool muted = false)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            TextBlock block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 6, 12, 0) };
            if (muted)
            {
                block.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
            }
            Info.Children.Add(block);
        }

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
