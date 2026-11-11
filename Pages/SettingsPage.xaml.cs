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

        public void OnLeaving() { }

        private void Build()
        {
            AppSettings settings = AppSettings.Current;
            Info.Children.Clear();

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
