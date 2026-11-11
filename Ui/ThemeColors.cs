using System;
using System.Windows;
using System.Windows.Media;

namespace Ludopol
{
    /// <summary>
    /// VIEW: the colours of the theme chosen in the settings (AppSettings.Theme):
    /// "dark", "light", or "system" (as Windows: Settings → Personalisation → Colours →
    /// app mode). They replace the brushes of Ui/Theme.xaml in the application resources;
    /// every view reads them with {DynamicResource …}, so a change shows at once.
    /// </summary>
    internal static class ThemeColors
    {
        public static void Apply(ResourceDictionary resources, string theme)
        {
            bool light = theme == "light" || (theme == "system" && WindowsUsesLightTheme());
            if (light)
            {
                Set(resources, "BackgroundBrush", 0xF0, 0xF0, 0xF4);
                Set(resources, "SurfaceBrush", 0xFF, 0xFF, 0xFF);
                Set(resources, "PressedBrush", 0xD2, 0xD2, 0xDE);
                Set(resources, "PlaceholderBrush", 0xDA, 0xDA, 0xE4);
                Set(resources, "TextBrush", 0x1A, 0x1A, 0x1F);
                Set(resources, "MutedTextBrush", 0x50, 0x50, 0x64);
                Set(resources, "AccentBrush", 0x2F, 0x6F, 0xD0);
                // Orange rather than yellow: visible on a light background.
                Set(resources, "HoverBrush", 0xE0, 0x86, 0x00);
                Set(resources, "OverlayBrush", 0xF0, 0xF0, 0xF4, 0xF2);
                Set(resources, "GaugeEmptyBrush", 0xC8, 0xC8, 0xD2);
            }
            else
            {
                Set(resources, "BackgroundBrush", 0x1E, 0x1E, 0x24);
                Set(resources, "SurfaceBrush", 0x2E, 0x2E, 0x38);
                Set(resources, "PressedBrush", 0x45, 0x45, 0x5A);
                Set(resources, "PlaceholderBrush", 0x3A, 0x3A, 0x4A);
                Set(resources, "TextBrush", 0xF4, 0xF4, 0xF4);
                Set(resources, "MutedTextBrush", 0xB4, 0xB4, 0xBE);
                Set(resources, "AccentBrush", 0x2F, 0x6F, 0xD0);
                Set(resources, "HoverBrush", 0xFF, 0xD5, 0x4A);
                Set(resources, "OverlayBrush", 0x1E, 0x1E, 0x24, 0xF2);
                Set(resources, "GaugeEmptyBrush", 0x45, 0x45, 0x4F);
            }
        }

        /// <summary>Windows' app mode (registry value AppsUseLightTheme). Dark if it cannot be read.</summary>
        private static bool WindowsUsesLightTheme()
        {
            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    return false;
                }
                using (Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                           @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    return key?.GetValue("AppsUseLightTheme") is int value && value == 1;
                }
            }
            catch (Exception ex)
            {
                App.Log("Windows theme not read: " + ex.Message);
                return false;
            }
        }

        private static void Set(ResourceDictionary resources, string key, byte r, byte g, byte b, byte a = 0xFF)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            resources[key] = brush;
        }
    }
}
