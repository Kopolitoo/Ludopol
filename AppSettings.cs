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
        /// <summary>Interface language: the name of a file of the Languages folder ("fr", "en").</summary>
        public string Language { get; set; } = "fr";

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
        public bool HoverActivation { get; set; } = false;
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
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                App.Log("Settings: unreadable file, default values used: " + ex.Message);
                Current = new AppSettings();
            }
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
            resources["TileWidth"] = TileWidth;
            resources["TileHeight"] = TileHeight;
            resources["ButtonMinSize"] = ButtonMinSize;
            resources["WideButtonWidth"] = ButtonMinSize * 2;
            resources["BaseFontSize"] = FontSize;
            resources["SmallFontSize"] = Math.Round(FontSize * 0.75);
            resources["TitleFontSize"] = TitleFontSize;
            // Each button has half the gap on each side: two neighbours are one full gap apart.
            resources["HalfSpacing"] = new Thickness(Spacing / 2);
        }
    }
}
