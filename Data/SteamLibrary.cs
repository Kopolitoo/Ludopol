using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Ludopol
{
    /// <summary>
    /// LOGIC: what Steam has installed on this computer, read from Steam's own files (Steam
    /// does not need to be running).
    ///
    ///   1. Steam's folder is written in the registry by Steam itself
    ///      (HKEY_CURRENT_USER\Software\Valve\Steam, value SteamPath).
    ///   2. steamapps\libraryfolders.vdf in that folder lists every Steam library
    ///      (C:\Program Files (x86)\Steam, D:\SteamLibrary…).
    ///   3. Each library has one steamapps\appmanifest_&lt;number&gt;.acf per game. Its
    ///      "StateFlags" tells whether the game is fully installed (bit 4) or still
    ///      downloading.
    /// </summary>
    internal sealed class SteamLibrary
    {
        /// <summary>The "fully installed" bit of StateFlags in an appmanifest file.</summary>
        private const int FullyInstalled = 4;

        private SteamLibrary(string? steamFolder, List<string> libraries)
        {
            SteamFolder = steamFolder;
            Libraries = libraries;
        }

        /// <summary>Steam's folder, or null when Steam was not found on this computer.</summary>
        public string? SteamFolder { get; }

        /// <summary>The steamapps folders of every Steam library (empty when Steam was not found).</summary>
        public IReadOnlyList<string> Libraries { get; }

        /// <summary>Finds Steam and its libraries. Never throws: Steam not found gives an empty list.</summary>
        public static SteamLibrary Find()
        {
            string? steamFolder = FindSteamFolder();
            List<string> libraries = new List<string>();
            if (steamFolder == null)
            {
                App.Log("Steam: not found");
                return new SteamLibrary(null, libraries);
            }

            AddLibrary(libraries, Path.Combine(steamFolder, "steamapps"));
            string listFile = Path.Combine(steamFolder, "steamapps", "libraryfolders.vdf");
            try
            {
                if (File.Exists(listFile))
                {
                    foreach (string folder in ReadLibraryFolders(File.ReadAllText(listFile)))
                    {
                        AddLibrary(libraries, Path.Combine(folder, "steamapps"));
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Steam: libraryfolders.vdf not read: " + ex.Message);
            }
            App.Log("Steam: " + steamFolder + ", libraries: " + string.Join(" | ", libraries));
            return new SteamLibrary(steamFolder, libraries);
        }

        /// <summary>The numbers (AppIDs) of the games fully installed in every library.</summary>
        public HashSet<long> InstalledAppIds()
        {
            HashSet<long> ids = new HashSet<long>();
            foreach (string library in Libraries)
            {
                string[] files;
                try
                {
                    files = Directory.GetFiles(library, "appmanifest_*.acf");
                }
                catch (Exception ex)
                {
                    App.Log("Steam: library not read " + library + ": " + ex.Message);
                    continue;
                }
                foreach (string file in files)
                {
                    long? id = ReadInstalledAppId(file);
                    if (id.HasValue)
                    {
                        ids.Add(id.Value);
                    }
                }
            }
            return ids;
        }

        /// <summary>Whether one game is fully installed (reads only its own appmanifest files).</summary>
        public bool IsInstalled(long appId)
        {
            foreach (string library in Libraries)
            {
                string file = Path.Combine(library, "appmanifest_" + appId.ToString(CultureInfo.InvariantCulture) + ".acf");
                if (File.Exists(file) && ReadInstalledAppId(file) == appId)
                {
                    return true;
                }
            }
            return false;
        }

        // ----- Reading Steam's files -----

        /// <summary>The library folders listed in libraryfolders.vdf (old and new layout).</summary>
        public static List<string> ReadLibraryFolders(string vdfText)
        {
            List<string> folders = new List<string>();
            Dictionary<string, object> root = Vdf.Parse(vdfText);
            foreach (object top in root.Values)
            {
                if (!(top is Dictionary<string, object> list))
                {
                    continue;
                }
                foreach (KeyValuePair<string, object> entry in list)
                {
                    // Only the numbered entries are libraries ("0", "1"…).
                    if (!int.TryParse(entry.Key, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    {
                        continue;
                    }
                    if (entry.Value is string oldPath)
                    {
                        folders.Add(oldPath);   // old layout: "1" "D:\\SteamLibrary"
                    }
                    else if (entry.Value is Dictionary<string, object> library
                             && library.TryGetValue("path", out object? path) && path is string newPath)
                    {
                        folders.Add(newPath);   // current layout: "1" { "path" "D:\\SteamLibrary" … }
                    }
                }
            }
            return folders;
        }

        /// <summary>
        /// Reads the text of an appmanifest file: the game's number if it is fully installed,
        /// otherwise null (downloading, or unreadable).
        /// </summary>
        public static long? ReadInstalledAppIdFromText(string acfText)
        {
            Dictionary<string, object> root = Vdf.Parse(acfText);
            if (!root.TryGetValue("AppState", out object? stateObject) || !(stateObject is Dictionary<string, object> state))
            {
                return null;
            }
            if (!state.TryGetValue("appid", out object? idText) || !(idText is string idString)
                || !long.TryParse(idString, NumberStyles.None, CultureInfo.InvariantCulture, out long id))
            {
                return null;
            }
            if (!state.TryGetValue("StateFlags", out object? flagsText) || !(flagsText is string flagsString)
                || !long.TryParse(flagsString, NumberStyles.None, CultureInfo.InvariantCulture, out long flags))
            {
                return null;
            }
            return (flags & FullyInstalled) != 0 ? id : (long?)null;
        }

        private static long? ReadInstalledAppId(string file)
        {
            try
            {
                return ReadInstalledAppIdFromText(File.ReadAllText(file));
            }
            catch (Exception ex)
            {
                App.Log("Steam: " + file + " not read: " + ex.Message);
                return null;
            }
        }

        private static string? FindSteamFolder()
        {
            List<string> candidates = new List<string>();
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    using (Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    {
                        if (key?.GetValue("SteamPath") is string path && path.Length > 0)
                        {
                            // Steam writes it with "/": c:/program files (x86)/steam.
                            candidates.Add(path.Replace('/', '\\'));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Steam: registry not read: " + ex.Message);
            }
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (programFiles.Length > 0)
            {
                candidates.Add(Path.Combine(programFiles, "Steam"));
            }

            foreach (string candidate in candidates)
            {
                if (Directory.Exists(Path.Combine(candidate, "steamapps")))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static void AddLibrary(List<string> libraries, string steamapps)
        {
            if (!Directory.Exists(steamapps))
            {
                return;
            }
            string full = Path.GetFullPath(steamapps).TrimEnd('\\', '/');
            foreach (string known in libraries)
            {
                if (string.Equals(known, full, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            libraries.Add(full);
        }

        /// <summary>
        /// Steam's text format (VDF / ACF): "key" "value" pairs and "key" { … } blocks.
        /// Gives nested dictionaries whose values are strings or dictionaries; keys ignore case.
        /// </summary>
        private static class Vdf
        {
            public static Dictionary<string, object> Parse(string text)
            {
                int position = 0;
                return ReadBlock(text, ref position);
            }

            private static Dictionary<string, object> ReadBlock(string text, ref int position)
            {
                Dictionary<string, object> block = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                while (true)
                {
                    string? key = NextToken(text, ref position, out bool keyQuoted);
                    if (key == null || (!keyQuoted && key == "}"))
                    {
                        return block;
                    }
                    string? value = NextToken(text, ref position, out bool valueQuoted);
                    if (value == null)
                    {
                        return block;
                    }
                    if (!valueQuoted && value == "{")
                    {
                        block[key] = ReadBlock(text, ref position);
                    }
                    else
                    {
                        block[key] = value;
                    }
                }
            }

            /// <summary>The next "quoted string", { or }, skipping spaces and // comments. Null at the end.</summary>
            private static string? NextToken(string text, ref int position, out bool quoted)
            {
                quoted = false;
                while (position < text.Length)
                {
                    char c = text[position];
                    if (char.IsWhiteSpace(c))
                    {
                        position++;
                    }
                    else if (c == '/' && position + 1 < text.Length && text[position + 1] == '/')
                    {
                        while (position < text.Length && text[position] != '\n')
                        {
                            position++;
                        }
                    }
                    else if (c == '{' || c == '}')
                    {
                        position++;
                        return c.ToString();
                    }
                    else if (c == '"')
                    {
                        quoted = true;
                        position++;
                        StringBuilder token = new StringBuilder();
                        while (position < text.Length && text[position] != '"')
                        {
                            if (text[position] == '\\' && position + 1 < text.Length)
                            {
                                position++;   // \\ → \   and   \" → "
                            }
                            token.Append(text[position]);
                            position++;
                        }
                        position++;   // closing quote
                        return token.ToString();
                    }
                    else
                    {
                        // A word without quotes: read up to the next space.
                        StringBuilder token = new StringBuilder();
                        while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] != '{' && text[position] != '}')
                        {
                            token.Append(text[position]);
                            position++;
                        }
                        quoted = true;
                        return token.ToString();
                    }
                }
                return null;
            }
        }
    }
}
