using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Ludopol
{
    /// <summary>
    /// VIEW: the settings page, opened by "Settings" in the left column.
    ///
    /// Each setting is a row of big buttons (Ui/ChoiceRow.cs). A choice is applied at once
    /// (sizes change on screen straight away) and saved in settings.json. Only the language
    /// waits for the next start, because every page reads its texts when it is built.
    /// Values set by hand in settings.json that are not in the lists stay as they are: no
    /// button is then shown in blue.
    /// </summary>
    public partial class SettingsPage : UserControl, IPage
    {
        /// <summary>The summary of the last import, shown under its buttons until the page is left.</summary>
        private string? _importSummary;

        /// <summary>The result of the last search for installed games, shown until the page is left.</summary>
        private string? _installedSummary;

        private static readonly int[] Delays = { 500, 750, 1000, 1500, 2000, 3000 };

        /// <summary>The four steps of each size: small, medium, large, very large.</summary>
        private static readonly string[] SizeKeys = { "settings.size.small", "settings.size.medium", "settings.size.large", "settings.size.veryLarge" };
        private static readonly double[] FontSizes = { 24, 28, 32, 38 };
        private static readonly double[] ButtonSizes = { 120, 150, 180, 220 };
        private static readonly double[] TileWidths = { 260, 320, 400, 480 };

        public SettingsPage()
        {
            InitializeComponent();
        }

        public string Title => Texts.Get("settings.title");

        public UIElement? SideContent => null;

        public void OnShown() => Build();

        public void OnLeaving()
        {
            _importSummary = null;
            _installedSummary = null;
        }

        private void Build()
        {
            AppSettings settings = AppSettings.Current;
            Info.Children.Clear();

            AddImportSection(settings);
            AddInstalledSection();

            AddTitle("settings.fullScreen");
            AddChoices(new[] { true, false }, v => Texts.Get(v ? "common.yes" : "common.no"),
                settings.FullScreen, v =>
                {
                    settings.FullScreen = v;
                    MainWindow.Current.ApplyWindowMode();
                });

            AddTitle("settings.hoverActivation");
            AddChoices(new[] { true, false }, v => Texts.Get(v ? "common.yes" : "common.no"),
                settings.HoverActivation, v => settings.HoverActivation = v);

            AddTitle("settings.hoverDelay");
            AddChoices(Delays, ms => Texts.Format("settings.seconds", (ms / 1000.0).ToString("0.##", CultureInfo.CurrentCulture)),
                settings.HoverDelayMs, ms => settings.HoverDelayMs = ms);

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

            AddTitle("settings.language");
            AddChoices(new[] { "fr", "en" }, code => code == "fr" ? "Français" : "English",
                settings.Language, code => settings.Language = code);
            Info.Children.Add(new TextBlock
            {
                Text = Texts.Get("settings.languageNote"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush"),
                Margin = new Thickness(12, 0, 12, 24),
            });
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
