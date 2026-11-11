using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Markup;

namespace Ludopol
{
    /// <summary>
    /// LOGIC: the texts shown to the user, in the chosen language (same system as Prunel).
    ///
    /// Every text of the interface lives in a language file of the Languages folder, next to
    /// Ludopol.exe: Languages/en.json, Languages/fr.json. A language file is a flat JSON
    /// object, one entry per text:
    ///     {
    ///       "language.name": "Français",
    ///       "home.categories": "Catégories",
    ///       "game.count": "{0} jeu(x)"
    ///     }
    /// "{0}", "{1}"… are replaced by values (string.Format), see Format.
    ///
    /// In the code, a text is read by its key: Texts.Get("home.categories"); in XAML, with
    /// {local:Tr home.categories} (see TrExtension below).
    /// If the key is missing from the chosen language, the English text (the reference) is
    /// used; if it is missing there too, the key itself is shown, so a forgotten text is
    /// visible instead of crashing. Each missing key is logged once.
    /// </summary>
    internal static class Texts
    {
        public const string FolderName = "Languages";
        public const string FallbackLanguage = "en";

        private static Dictionary<string, string> _current = new Dictionary<string, string>();
        private static Dictionary<string, string> _fallback = new Dictionary<string, string>();
        private static readonly HashSet<string> _reportedMissing = new HashSet<string>();

        /// <summary>The code of the language in use ("fr", "en").</summary>
        public static string Language { get; private set; } = FallbackLanguage;

        /// <summary>Loads a language ("fr" → Languages/fr.json), or English if it is missing.</summary>
        public static void Load(string language)
        {
            language = string.IsNullOrWhiteSpace(language) ? FallbackLanguage : language.Trim().ToLowerInvariant();

            _fallback = ReadFile(FallbackLanguage) ?? new Dictionary<string, string>();
            Dictionary<string, string>? chosen = language == FallbackLanguage ? _fallback : ReadFile(language);
            if (chosen == null)
            {
                App.Log("Texts: language \"" + language + "\" not available, using " + FallbackLanguage);
                chosen = _fallback;
                language = FallbackLanguage;
            }
            _current = chosen;
            Language = language;
            _reportedMissing.Clear();
            App.Log("Texts: language " + language + " (" + _current.Count + " texts)");
        }

        /// <summary>The text of a key in the current language.</summary>
        public static string Get(string key)
        {
            if (_current.TryGetValue(key, out string? text) || _fallback.TryGetValue(key, out text))
            {
                return text;
            }
            if (_reportedMissing.Add(key))
            {
                App.Log("Texts: missing key \"" + key + "\"");
            }
            return key;
        }

        /// <summary>The text of a key with its "{0}", "{1}"… replaced by the given values.</summary>
        public static string Format(string key, params object[] values)
        {
            string pattern = Get(key);
            try
            {
                return string.Format(CultureInfo.CurrentCulture, pattern, values);
            }
            catch (FormatException)
            {
                // A badly written translation ("{1" without "}") must not crash Ludopol.
                App.Log("Texts: bad format in \"" + key + "\"");
                return pattern;
            }
        }

        /// <summary>Reads Languages/&lt;code&gt;.json, or null if missing or unreadable.</summary>
        private static Dictionary<string, string>? ReadFile(string code)
        {
            string path = Path.Combine(AppContext.BaseDirectory, FolderName, code + ".json");
            if (!File.Exists(path))
            {
                App.Log("Texts: file not found: " + path);
                return null;
            }
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                    ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                App.Log("Texts: unreadable file " + path + ": " + ex.Message);
                return null;
            }
        }
    }

    /// <summary>
    /// Lets XAML show a text of the language file: Text="{local:Tr home.categories}".
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class TrExtension : MarkupExtension
    {
        public TrExtension() { }

        public TrExtension(string key)
        {
            Key = key;
        }

        [ConstructorArgument("key")]
        public string Key { get; set; } = "";

        public override object ProvideValue(IServiceProvider serviceProvider) => Texts.Get(Key);
    }
}
