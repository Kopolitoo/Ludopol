using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Ludopol
{
    /// <summary>
    /// MODEL: the settings, saved in %LocalAppData%\Ludopol\settings.json.
    ///
    /// The file is created with the default values below at the first start. To adjust the
    /// interface to the eyetracker, close Ludopol, edit the file with a text editor, and start
    /// Ludopol again. Sizes are in WPF units (1 unit = 1/96 inch): on a 4K screen with Windows
    /// scaling at 200 %, 1 unit = 2 screen pixels.
    /// </summary>
    internal sealed class AppSettings
    {
        /// <summary>
        /// Version of the settings file. When a default value changes in a new version of Ludopol,
        /// Load() updates an older file (see Upgrade).
        /// </summary>
        public int SettingsVersion { get; set; } = CurrentVersion;

        private const int CurrentVersion = 2;

        /// <summary>Interface language: the name of a file of the Languages folder ("fr", "en").</summary>
        public string Language { get; set; } = "fr";

        /// <summary>Colours: "dark", "light", or "system" (as Windows). See Ui/ThemeColors.cs.</summary>
        public string Theme { get; set; } = "dark";

        /// <summary>
        /// true: full screen, without frame; false (at the first start): an ordinary window,
        /// maximised, which can be moved and resized.
        /// </summary>
        public bool FullScreen { get; set; }

        /// <summary>The last games sheet imported (settings page, "Import again").</summary>
        public string? LastImportPath { get; set; }

        /// <summary>
        /// Steam synchronisation (settings page): the Steam Web API key, created on
        /// https://steamcommunity.com/dev/apikey, and the account chosen (SteamID64; empty: the
        /// account most recently used in Steam on this computer). Keep the key to yourself.
        /// </summary>
        public string? SteamApiKey { get; set; }
        public string? SteamAccountId { get; set; }

        /// <summary>Width and height of a game tile in the lists.</summary>
        public double TileWidth { get; set; } = 320;
        public double TileHeight { get; set; } = 300;

        /// <summary>Smallest width and height of any button.</summary>
        public double ButtonMinSize { get; set; } = 150;

        /// <summary>Gap between two buttons.</summary>
        public double Spacing { get; set; } = 24;

        /// <summary>Size of the ordinary text, and of the titles.</summary>
        public double FontSize { get; set; } = 28;
        public double TitleFontSize { get; set; } = 40;

        /// <summary>
        /// Hover activation: when true, a button activates by itself once the cursor (moved by
        /// Prunel's movement mode) has stayed on it for HoverDelayMs milliseconds. A click
        /// always works too.
        /// </summary>
        public bool HoverActivation { get; set; } = true;
        public int HoverDelayMs { get; set; } = 1000;

        /// <summary>The settings in use.</summary>
        public static AppSettings Current { get; private set; } = new AppSettings();

        private static string FilePath => Path.Combine(App.DataFolder, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        /// <summary>
        /// Reads settings.json. A missing file is created with the default values; an
        /// unreadable file is logged and the default values are used (the file is left as it is,
        /// so a typing mistake can be fixed by hand).
        /// </summary>
        public static void Load()
        {
            if (!File.Exists(FilePath))
            {
                Current = new AppSettings();
                Current.Save();
                return;
            }
            try
            {
                string json = File.ReadAllText(FilePath);
                Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    // A file written before SettingsVersion existed is version 1.
                    if (!document.RootElement.TryGetProperty(nameof(SettingsVersion), out _))
                    {
                        Current.SettingsVersion = 1;
                    }
                }
                Current.Upgrade();
            }
            catch (Exception ex)
            {
                App.Log("Settings: unreadable file, default values used: " + ex.Message);
                Current = new AppSettings();
            }
        }

        /// <summary>Brings a file of an older version up to date, and saves it.</summary>
        private void Upgrade()
        {
            if (SettingsVersion >= CurrentVersion)
            {
                return;
            }
            if (SettingsVersion < 2)
            {
                // Version 2: hover activation on by default, after one second.
                HoverActivation = true;
                HoverDelayMs = 1000;
            }
            App.Log("Settings: file upgraded from version " + SettingsVersion + " to " + CurrentVersion);
            SettingsVersion = CurrentVersion;
            Save();
        }

        /// <summary>Writes settings.json.</summary>
        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch (Exception ex)
            {
                App.Log("Settings: cannot save: " + ex.Message);
            }
        }

        /// <summary>
        /// Puts the sizes in the application resources, where the styles (Ui/Theme.xaml) read
        /// them with {DynamicResource TileWidth} and so on.
        /// </summary>
        public void ApplyTo(ResourceDictionary resources)
        {
            ThemeColors.Apply(resources, Theme);
            resources["TileWidth"] = TileWidth;
            resources["TileHeight"] = TileHeight;
            // A tile and its margins: the smallest column of the tile grids (Ui/TileGrid.cs).
            resources["TileSlotWidth"] = TileWidth + Spacing;
            resources["ButtonMinSize"] = ButtonMinSize;
            resources["WideButtonWidth"] = ButtonMinSize * 2;
            // Half-size buttons: the tags of a game's page.
            resources["SmallButtonSize"] = Math.Round(ButtonMinSize / 2);
            // A text field for a name (rename a category…): the width of three wide buttons.
            resources["TextFieldWidth"] = ButtonMinSize * 6;
            resources["SideButtonWidth"] = Math.Round(ButtonMinSize * 1.4);
            // The left navigation column: one side button and its margins, as wide as the filter column.
            resources["SideColumnWidth"] = Math.Round(ButtonMinSize * 1.4) + Spacing;
            resources["TileImageHeight"] = Math.Round(TileHeight * 0.6);
            resources["BaseFontSize"] = FontSize;
            resources["SmallFontSize"] = Math.Round(FontSize * 0.75);
            resources["TitleFontSize"] = TitleFontSize;
            // Each button has half the gap on each side: two neighbours are one full gap apart.
            resources["HalfSpacing"] = new Thickness(Spacing / 2);
        }
    }
}
