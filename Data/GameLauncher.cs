using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Ludopol
{
    /// <summary>
    /// LOGIC: which games are installed on this computer, and starting them.
    ///
    /// A game is installed when:
    ///   - a program was chosen for it (Game.LaunchTarget) and that file still exists; or
    ///   - it has a Steam number and Steam has fully installed it (see SteamLibrary.cs).
    ///
    /// "Play" starts the chosen program when there is one, otherwise asks Steam to start the
    /// game (address steam://rungameid/NUMBER). "Install with Steam" opens Steam's own install
    /// window (steam://install/NUMBER).
    /// </summary>
    internal static class GameLauncher
    {
        private static SteamLibrary? _steam;

        /// <summary>Steam and its libraries, found at the first need and kept until the next RefreshAll.</summary>
        public static SteamLibrary Steam => _steam ??= SteamLibrary.Find();

        /// <summary>
        /// Looks for Steam again and recomputes "installed" for the whole catalogue.
        /// Returns how many games are installed.
        /// </summary>
        public static int RefreshAll(Database database)
        {
            _steam = SteamLibrary.Find();
            var steamInstalled = _steam.InstalledAppIds();
            int count = database.UpdateInstalled((steamId, target) =>
                ProgramExists(target) || (steamId.HasValue && steamInstalled.Contains(steamId.Value)));
            App.Log("Installed games: " + count + " (Steam: " + steamInstalled.Count + " installed in total)");
            return count;
        }

        /// <summary>
        /// Checks one game again (when its page is shown: it may have been installed or
        /// uninstalled meanwhile) and saves the answer if it changed.
        /// </summary>
        public static void Refresh(Game game, Database database)
        {
            bool now = ProgramExists(game.LaunchTarget)
                       || (game.SteamAppId.HasValue && Steam.IsInstalled(game.SteamAppId.Value));
            if (now != game.Installed)
            {
                game.Installed = now;
                database.SaveInstallation(game);
            }
        }

        /// <summary>Starts the game. Throws if Windows cannot start it (the page shows the message).</summary>
        public static void Play(Game game)
        {
            if (ProgramExists(game.LaunchTarget))
            {
                App.Log("Play \"" + game.Name + "\": " + game.LaunchTarget);
                Open(game.LaunchTarget!, Path.GetDirectoryName(game.LaunchTarget));
            }
            else if (game.SteamAppId.HasValue)
            {
                App.Log("Play \"" + game.Name + "\" with Steam (" + game.SteamAppId.Value + ")");
                Open("steam://rungameid/" + game.SteamAppId.Value.ToString(CultureInfo.InvariantCulture), null);
            }
            else
            {
                throw new InvalidOperationException(Texts.Get("play.nothing"));
            }
        }

        /// <summary>Opens Steam's window to install the game (Steam asks where, then downloads it).</summary>
        public static void InstallWithSteam(Game game)
        {
            if (!game.SteamAppId.HasValue)
            {
                return;
            }
            App.Log("Install \"" + game.Name + "\" with Steam (" + game.SteamAppId.Value + ")");
            Open("steam://install/" + game.SteamAppId.Value.ToString(CultureInfo.InvariantCulture), null);
        }

        public static bool ProgramExists(string? target) => !string.IsNullOrWhiteSpace(target) && File.Exists(target);

        /// <summary>Opens a file or an address as a double click would (shortcuts and steam:// included).</summary>
        private static void Open(string target, string? workingFolder)
        {
            ProcessStartInfo start = new ProcessStartInfo(target) { UseShellExecute = true };
            if (!string.IsNullOrEmpty(workingFolder))
            {
                // Many games look for their files next to their program.
                start.WorkingDirectory = workingFolder;
            }
            Process.Start(start)?.Dispose();
        }
    }
}
