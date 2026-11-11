using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Ludopol
{
    /// <summary>The parts of the settings, each on its own page (no scrolling, except lists).</summary>
    public enum SettingsSection
    {
        /// <summary>The first page: one big button per part.</summary>
        Menu,
        Screen,
        Sizes,
        Hover,
        Catalogue,
        Steam,
        /// <summary>The owned Steam games missing from the catalogue (a list: it may scroll).</summary>
        SteamMissing,
    }

    /// <summary>
    /// VIEW: the settings, opened by "Settings" in the left column.
    ///
    /// The first page (Menu) has one big button per part: screen and language, sizes, hover,
    /// catalogue (import, installed games), Steam library. Each part is its own page, small
    /// enough to fit on the screen without scrolling; "Back" returns to the menu.
    ///
    /// Each setting is a row of big buttons (Ui/ChoiceRow.cs). A choice is applied at once
    /// (sizes change on screen straight away) and saved in settings.json. Only the language
    /// waits for the next start, because every page reads its texts when it is built.
    /// Values set by hand in settings.json that are not in the lists stay as they are: no
    /// button is then shown in blue.
    /// </summary>
    public partial class SettingsPage : UserControl, IPage
    {
        private readonly SettingsSection _section;

        /// <summary>The summary of the last import, shown under its buttons until the page is left.</summary>
        private string? _importSummary;

        /// <summary>The result of the last search for installed games, shown until the page is left.</summary>
        private string? _installedSummary;

        /// <summary>The result of the last Steam synchronisation (or addition), shown until the page is left.</summary>
        private string? _steamSummary;
        private bool _syncRunning;

        /// <summary>
        /// The owned games missing from the catalogue, found by the last synchronisation, and
        /// shown by the SteamMissing page. Kept while Ludopol runs.
        /// </summary>
        private static List<OwnedGame>? _missingGames;

        private static readonly int[] Delays = { 500, 750, 1000, 1500, 2000, 3000 };

        /// <summary>The four steps of each size: small, medium, large, very large.</summary>
        private static readonly string[] SizeKeys = { "settings.size.small", "settings.size.medium", "settings.size.large", "settings.size.veryLarge" };
        private static readonly double[] FontSizes = { 24, 28, 32, 38 };
        private static readonly double[] ButtonSizes = { 120, 150, 180, 220 };
        private static readonly double[] TileWidths = { 260, 320, 400, 480 };

        /// <summary>The settings key of each part's name, in the order of the menu.</summary>
        private static readonly (SettingsSection Section, string Key)[] Sections =
        {
            (SettingsSection.Screen, "settings.section.screen"),
            (SettingsSection.Sizes, "settings.section.sizes"),
            (SettingsSection.Hover, "settings.section.hover"),
            (SettingsSection.Catalogue, "settings.section.catalogue"),
            (SettingsSection.Steam, "steamSync.title"),
            (SettingsSection.SteamMissing, "steamSync.missingTitle"),
        };

        public SettingsPage(SettingsSection section = SettingsSection.Menu)
        {
            InitializeComponent();
            _section = section;
        }

        public bool IsMenu => _section == SettingsSection.Menu;

        public string Title => IsMenu
            ? Texts.Get("settings.title")
            : Texts.Get("settings.title") + " : " + Texts.Get(Sections.First(s => s.Section == _section).Key);

        public UIElement? SideContent => null;

        public void OnShown()
        {
            SteamStore.StatusChanged += Build;
            Build();
        }

        public void OnLeaving()
        {
            SteamStore.StatusChanged -= Build;
            _importSummary = null;
            _installedSummary = null;
            _steamSummary = null;
            AppSettings.Current.Save();   // the Steam key typed on the page
        }

        private void Build()
        {
            AppSettings settings = AppSettings.Current;
            Info.Children.Clear();

            switch (_section)
            {
                case SettingsSection.Menu:
                    BuildMenu();
                    break;

                case SettingsSection.Screen:
                    AddTitle("settings.fullScreen");
                    AddChoices(new[] { true, false }, v => Texts.Get(v ? "common.yes" : "common.no"),
                        settings.FullScreen, v =>
                        {
                            settings.FullScreen = v;
                            MainWindow.Current.ApplyWindowMode();
                        });

                    AddTitle("settings.theme");
                    AddChoices(new[] { "dark", "light", "system" }, theme => Texts.Get("settings.theme." + theme),
                        settings.Theme, theme => settings.Theme = theme);

                    AddTitle("settings.language");
                    AddChoices(new[] { "fr", "en" }, code => code == "fr" ? "Français" : "English",
                        settings.Language, code => settings.Language = code);
                    AddText(Texts.Get("settings.languageNote"), muted: true);
                    break;

                case SettingsSection.Sizes:
                    AddTitle("settings.fontSize");
                    AddSizeChoices(FontSizes, settings.FontSize, size =>
                    {
                        settings.FontSize = size;
                        settings.TitleFontSize = Math.Round(size * 1.43);
                    });

                    AddTitle("settings.buttonSize");
                    AddSizeChoices(ButtonSizes, settings.ButtonMinSize, size => settings.ButtonMinSize = size);

                    AddTitle("settings.tileSize");
                    AddSizeChoices(TileWidths, settings.TileWidth, width =>
                    {
                        settings.TileWidth = width;
                        settings.TileHeight = Math.Round(width * 0.94);
                    });
                    break;

                case SettingsSection.Hover:
                    AddTitle("settings.hoverActivation");
                    AddChoices(new[] { true, false }, v => Texts.Get(v ? "common.yes" : "common.no"),
                        settings.HoverActivation, v => settings.HoverActivation = v);

                    AddTitle("settings.hoverDelay");
                    AddChoices(Delays, ms => Texts.Format("settings.seconds", (ms / 1000.0).ToString("0.##", CultureInfo.CurrentCulture)),
                        settings.HoverDelayMs, ms => settings.HoverDelayMs = ms);
                    break;

                case SettingsSection.Catalogue:
                    AddImportSection(settings);
                    AddInstalledSection();
                    break;

                case SettingsSection.Steam:
                    AddSteamSection(settings);
                    break;

                case SettingsSection.SteamMissing:
                    AddMissingList();
                    break;
            }
        }

        /// <summary>The first page: one big button per part of the settings.</summary>
        private void BuildMenu()
        {
            WrapPanel row = new WrapPanel();
            foreach ((SettingsSection section, string key) in Sections)
            {
                if (section == SettingsSection.SteamMissing)
                {
                    continue;   // opened from the Steam page, after a synchronisation
                }
                TileButton button = new TileButton { Content = Texts.Get(key) };
                button.SetResourceReference(WidthProperty, "WideButtonWidth");
                button.SetResourceReference(HeightProperty, "WideButtonWidth");
                button.Click += (s, e) => MainWindow.Current.Navigate(new SettingsPage(section));
                row.Children.Add(button);
            }
            Info.Children.Add(row);
        }

        /// <summary>
        /// "Import my sheet": one button opens the Windows file picker; when a sheet was already
        /// imported, a second button imports the same file again without the picker.
        /// </summary>
        private void AddImportSection(AppSettings settings)
        {
            AddTitle("import.title");
            WrapPanel row = new WrapPanel();

            TileButton choose = new TileButton { Content = Texts.Get("import.choose") };
            choose.SetResourceReference(WidthProperty, "WideButtonWidth");
            choose.Click += (s, e) => ChooseAndImport();
            row.Children.Add(choose);

            if (!string.IsNullOrEmpty(settings.LastImportPath) && System.IO.File.Exists(settings.LastImportPath))
            {
                TileButton again = new TileButton
                {
                    Content = Texts.Format("import.again", System.IO.Path.GetFileName(settings.LastImportPath)),
                };
                again.SetResourceReference(WidthProperty, "WideButtonWidth");
                again.Click += (s, e) => RunImport(settings.LastImportPath);
                row.Children.Add(again);
            }
            Info.Children.Add(row);

            if (_importSummary != null)
            {
                Info.Children.Add(new TextBlock
                {
                    Text = _importSummary,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12, 0, 12, 12),
                });
            }
        }

        /// <summary>
        /// "Search for installed games": looks again at Steam's libraries and at the programs
        /// chosen on the games' pages (also done at each start of Ludopol).
        /// </summary>
        private void AddInstalledSection()
        {
            AddTitle("installed.title");
            TileButton search = new TileButton { Content = Texts.Get("installed.search") };
            search.SetResourceReference(WidthProperty, "WideButtonWidth");
            search.Click += (s, e) => SearchInstalled();
            Info.Children.Add(new WrapPanel { Children = { search } });

            if (_installedSummary != null)
            {
                Info.Children.Add(new TextBlock
                {
                    Text = _installedSummary,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12, 0, 12, 12),
                });
            }
        }

        private void SearchInstalled()
        {
            try
            {
                int count = GameLauncher.RefreshAll(App.Database);
                SteamLibrary steam = GameLauncher.Steam;
                List<string> lines = new List<string> { Texts.Format("installed.result", count) };
                lines.Add(steam.SteamFolder == null
                    ? Texts.Get("installed.noSteam")
                    : Texts.Format("installed.libraries", string.Join(", ", steam.Libraries)));
                _installedSummary = string.Join("\n", lines);
            }
            catch (Exception ex)
            {
                App.Log("Search for installed games failed: " + ex);
                _installedSummary = Texts.Format("installed.error", ex.Message);
            }
            Build();
        }

        /// <summary>
        /// "Steam library": the account, the Steam key, and "Synchronise with Steam" (see
        /// Data/SteamSync.cs). After a synchronisation, a button adds the owned games missing
        /// from the catalogue.
        /// </summary>
        private void AddSteamSection(AppSettings settings)
        {
            AddTitle("steamSync.title");

            List<SteamAccount> accounts = GameLauncher.Steam.Accounts();
            SteamAccount? account = CurrentAccount(accounts, settings);
            if (accounts.Count == 0)
            {
                AddText(Texts.Get("steamSync.noAccount"), muted: true);
            }
            else if (accounts.Count == 1)
            {
                AddText(Texts.Format("steamSync.account", accounts[0].Name), muted: false);
            }
            else
            {
                AddText(Texts.Get("steamSync.chooseAccount"), muted: false);
                AddChoices(accounts.Select(a => a.Id), id => accounts.First(a => a.Id == id).Name,
                    account?.Id ?? "", id => settings.SteamAccountId = id);
            }

            AddText(Texts.Get("steamSync.key"), muted: false);
            TextBox keyBox = new TextBox { Text = settings.SteamApiKey ?? "", Margin = new Thickness(12, 0, 12, 0) };
            keyBox.TextChanged += (s, e) => settings.SteamApiKey = string.IsNullOrWhiteSpace(keyBox.Text) ? null : keyBox.Text.Trim();
            Info.Children.Add(keyBox);

            WrapPanel row = new WrapPanel();
            TileButton keyPage = new TileButton { Content = Texts.Get("steamSync.keyPage") };
            keyPage.SetResourceReference(WidthProperty, "WideButtonWidth");
            keyPage.SetResourceReference(FontSizeProperty, "SmallFontSize");
            keyPage.Click += (s, e) => OpenKeyPage();
            row.Children.Add(keyPage);

            TileButton sync = new TileButton
            {
                Content = Texts.Get(_syncRunning ? "steamSync.running" : "steamSync.run"),
                IsEnabled = !_syncRunning && account != null,
            };
            sync.SetResourceReference(WidthProperty, "WideButtonWidth");
            sync.Click += (s, e) => SyncSteam(account);
            row.Children.Add(sync);
            Info.Children.Add(row);

            if (_steamSummary != null)
            {
                AddText(_steamSummary, muted: false);
            }
            if (_missingGames != null && _missingGames.Count > 0)
            {
                TileButton see = new TileButton { Content = Texts.Format("steamSync.seeMissing", _missingGames.Count) };
                see.SetResourceReference(WidthProperty, "WideButtonWidth");
                see.Click += (s, e) => MainWindow.Current.Navigate(new SettingsPage(SettingsSection.SteamMissing));
                Info.Children.Add(new WrapPanel { Children = { see } });
            }

            // Description, tags… of the Steam games that have none (Data/SteamStore.cs).
            TileButton complete = new TileButton
            {
                Content = Texts.Get(SteamStore.Running ? "steamStore.running" : "steamStore.completeAll"),
                IsEnabled = !SteamStore.Running,
            };
            complete.SetResourceReference(WidthProperty, "WideButtonWidth");
            complete.Click += (s, e) => _ = SteamStore.CompleteAllAsync(App.Database);
            Info.Children.Add(new WrapPanel { Children = { complete } });
            if (SteamStore.Status != null)
            {
                AddText(SteamStore.Status, muted: false);
            }
        }

        /// <summary>
        /// The owned games missing from the catalogue, after a synchronisation: "Add all", then
        /// one button per game, which adds that game alone.
        /// </summary>
        private void AddMissingList()
        {
            if (_steamSummary != null)
            {
                AddText(_steamSummary, muted: false);
            }
            List<OwnedGame> missing = _missingGames ?? new List<OwnedGame>();
            if (missing.Count == 0)
            {
                AddText(Texts.Get("steamSync.noneMissing"), muted: false);
                return;
            }
            TileButton addAll = new TileButton { Content = Texts.Format("steamSync.addMissing", missing.Count) };
            addAll.SetResourceReference(WidthProperty, "WideButtonWidth");
            addAll.Click += (s, e) => AddMissingGames(missing.ToList());
            Info.Children.Add(new WrapPanel { Children = { addAll } });

            AddText(Texts.Get("steamSync.missing"), muted: false);
            WrapPanel games = new WrapPanel();
            foreach (OwnedGame game in missing)
            {
                TileButton button = new TileButton { Content = game.Name };
                button.SetResourceReference(WidthProperty, "WideButtonWidth");
                button.SetResourceReference(FontSizeProperty, "SmallFontSize");
                button.Click += (s, e) => AddMissingGames(new List<OwnedGame> { game });
                games.Children.Add(button);
            }
            Info.Children.Add(games);
        }

        /// <summary>The account chosen in the settings, otherwise the one most recently used in Steam.</summary>
        private static SteamAccount? CurrentAccount(List<SteamAccount> accounts, AppSettings settings) =>
            accounts.FirstOrDefault(a => a.Id == settings.SteamAccountId) ?? accounts.FirstOrDefault();

        private void OpenKeyPage()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(SteamSync.ApiKeyPage) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception ex)
            {
                App.Log("Steam key page not opened: " + ex.Message);
                _steamSummary = Texts.Format("steamSync.error", ex.Message);
                Build();
            }
        }

        private async void SyncSteam(SteamAccount? account)
        {
            AppSettings settings = AppSettings.Current;
            if (account == null || _syncRunning)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(settings.SteamApiKey))
            {
                _steamSummary = Texts.Get("steamSync.noKey");
                Build();
                return;
            }
            settings.Save();
            _syncRunning = true;
            _steamSummary = null;
            _missingGames = null;
            Build();
            try
            {
                List<OwnedGame> owned = await SteamSync.GetOwnedGamesAsync(settings.SteamApiKey, account.Id);
                SyncResult result = SteamSync.Apply(owned, App.Database);
                _missingGames = result.Missing;
                List<string> lines = new List<string>
                {
                    Texts.Format("steamSync.result", result.Owned, result.OwnedInCatalogue, result.Missing.Count),
                };
                _steamSummary = string.Join("\n", lines);
            }
            catch (Exception ex)
            {
                App.Log("Steam sync failed: " + ex);
                _steamSummary = Texts.Format("steamSync.error", ex.Message);
            }
            _syncRunning = false;
            Build();
        }

        /// <summary>Adds some of the missing games (one, or all) and takes them off the list.</summary>
        private void AddMissingGames(List<OwnedGame> games)
        {
            try
            {
                int added = SteamSync.AddMissing(games, App.Database);
                GameLauncher.RefreshAll(App.Database);   // some of them may be installed
                // Their description, tags… from the Steam store, in the background.
                _ = SteamStore.CompleteAllAsync(App.Database);
                _steamSummary = games.Count == 1
                    ? Texts.Format("steamSync.addedOne", games[0].Name, Texts.Get("steamSync.newCategory"))
                    : Texts.Format("steamSync.added", added, Texts.Get("steamSync.newCategory"));
                _missingGames?.RemoveAll(g => games.Contains(g));
            }
            catch (Exception ex)
            {
                App.Log("Adding the Steam games failed: " + ex);
                _steamSummary = Texts.Format("steamSync.error", ex.Message);
            }
            double offset = Scroll.Viewer.VerticalOffset;
            Build();
            Scroll.Viewer.ScrollToVerticalOffset(offset);
        }

        private void AddText(string text, bool muted)
        {
            TextBlock block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 0, 12, 12) };
            if (muted)
            {
                block.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
            }
            Info.Children.Add(block);
        }

        private void ChooseAndImport()
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = Texts.Get("import.title"),
                Filter = Texts.Get("import.filter") + " (*.xlsx)|*.xlsx",
            };
            string? last = AppSettings.Current.LastImportPath;
            if (!string.IsNullOrEmpty(last) && System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(last)))
            {
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(last);
            }
            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            {
                RunImport(dialog.FileName);
            }
        }

        /// <summary>Imports a sheet, remembers its path, and shows the summary.</summary>
        private void RunImport(string path)
        {
            try
            {
                ImportResult result = SheetImporter.Import(path, App.Database);
                GameLauncher.RefreshAll(App.Database);   // the new games may already be installed
                AppSettings.Current.LastImportPath = path;
                AppSettings.Current.Save();

                List<string> lines = new List<string>
                {
                    Texts.Format("import.result", result.Read, result.Added, result.Updated),
                };
                if (result.SamplesRemoved > 0)
                {
                    lines.Add(Texts.Get("import.samplesRemoved"));
                }
                if (result.SteamConflicts.Count > 0)
                {
                    lines.Add(Texts.Format("import.conflicts", string.Join(", ", result.SteamConflicts)));
                }
                _importSummary = string.Join("\n", lines);
            }
            catch (Exception ex)
            {
                App.Log("Import failed for " + path + ": " + ex);
                _importSummary = Texts.Format("import.error", ex.Message);
            }
            Build();
        }

        /// <summary>A size row: four buttons small → very large, for the given values.</summary>
        private void AddSizeChoices(double[] values, double current, Action<double> apply)
        {
            List<int> steps = new List<int> { 0, 1, 2, 3 };
            int currentStep = Array.IndexOf(values, current);
            AddChoices(steps, step => Texts.Get(SizeKeys[step]), currentStep, step => apply(values[step]));
        }

        /// <summary>A row of buttons: the choice is applied, saved, and the page redrawn.</summary>
        private void AddChoices<T>(IEnumerable<T> values, Func<T, string> label, T current, Action<T> apply)
        {
            Info.Children.Add(ChoiceRow.Create(values, label, current, value =>
            {
                apply(value);
                AppSettings.Current.Save();
                AppSettings.Current.ApplyTo(Application.Current.Resources);
                double offset = Scroll.Viewer.VerticalOffset;
                Build();
                Scroll.Viewer.ScrollToVerticalOffset(offset);
            }));
        }

        private void AddTitle(string key)
        {
            Info.Children.Add(new TextBlock { Text = Texts.Get(key), Style = (Style)FindResource("SectionTitle") });
        }
    }
}
