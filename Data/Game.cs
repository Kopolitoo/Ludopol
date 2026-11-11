using System;
using System.Collections.Generic;

namespace Ludopol
{
    /// <summary>
    /// How well a game can be played with the eyetracker. The numbers are stored in the
    /// database: never change them (Presumed came later, hence 5).
    /// </summary>
    public enum Compatibility
    {
        Unknown = 0,
        None = 1,
        /// <summary>Partial: needs a switch, or reflexes.</summary>
        PartialWithSwitch = 2,
        /// <summary>Partial: playable with the eyes, but with complex actions.</summary>
        PartialGaze = 3,
        Full = 4,
        /// <summary>
        /// Presumed: the game is in my list of games said to be playable with the eyes (the
        /// imported sheet), but I have not tried it yet. Not in the worst → best order.
        /// </summary>
        Presumed = 5,
    }

    /// <summary>
    /// MODEL: one game of the catalogue, as stored in the database (see Database.cs).
    /// The columns follow the Google sheet (export jeux poursuite occulaire.xlsx).
    /// </summary>
    public sealed class Game
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";

        // --- Steam data (filled by the import, step 3) ---
        public long? SteamAppId { get; set; }
        public string? ImageUrl { get; set; }
        public string? Description { get; set; }
        public string? ReleaseDate { get; set; }
        public string? Developer { get; set; }
        public string? Genres { get; set; }
        public string? Price { get; set; }
        /// <summary>French available: true, false, or null if unknown.</summary>
        public bool? French { get; set; }
        /// <summary>The game is in my Steam library (for the Steam synchronisation, later).</summary>
        public bool OwnedOnSteam { get; set; }

        // --- My own data ---
        public List<string> Categories { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
        public bool Tried { get; set; }
        public Compatibility Compatibility { get; set; } = Compatibility.Unknown;
        /// <summary>My rating from 1 to 5, or null.</summary>
        public int? Rating { get; set; }
        public string? Notes { get; set; }
        public bool ToPublish { get; set; }

        // --- On this computer (step 4, see GameLauncher.cs) ---
        public bool Installed { get; set; }
        /// <summary>
        /// The program chosen to start the game (.exe, or a shortcut .lnk / .url), or null:
        /// a Steam game is then started by Steam.
        /// </summary>
        public string? LaunchTarget { get; set; }

        /// <summary>When the game was added to the catalogue ("Recently added").</summary>
        public DateTime AddedAt { get; set; } = DateTime.Now;

        /// <summary>One or two letters shown on the tile when there is no image.</summary>
        public string Initials
        {
            get
            {
                string[] words = Name.Split(new[] { ' ', '-', ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    return "?";
                }
                string first = words[0].Substring(0, 1);
                return words.Length == 1 ? first.ToUpperInvariant() : (first + words[1].Substring(0, 1)).ToUpperInvariant();
            }
        }
    }

    /// <summary>
    /// A name with its number of games, for the Categories, Tags and Developers pages,
    /// and the image of one of its games (for the category cards).
    /// </summary>
    public sealed class NamedCount
    {
        public NamedCount(string name, int count, string? imageUrl = null)
        {
            Name = name;
            Count = count;
            ImageUrl = imageUrl;
        }

        public string Name { get; }
        public int Count { get; }
        public string? ImageUrl { get; }
    }

    /// <summary>The criteria a list of games can be sorted by.</summary>
    public enum SortBy
    {
        Name,
        Added,
        Rating,
        Compatibility,
    }

    /// <summary>
    /// MODEL: which games a list shows. Every criterion left empty (null / false) keeps
    /// all the games.
    /// </summary>
    public sealed class GameFilter
    {
        public string? Category { get; set; }
        public string? Tag { get; set; }
        public string? Developer { get; set; }
        /// <summary>true: installed games only; false: not installed; null: all.</summary>
        public bool? Installed { get; set; }
        /// <summary>true: tried games only; false: not tried yet; null: all.</summary>
        public bool? Tried { get; set; }
        /// <summary>true: owned on Steam only; false: not owned; null: all.</summary>
        public bool? OwnedOnSteam { get; set; }
        /// <summary>Keeps the games with exactly this compatibility; null: all.</summary>
        public Compatibility? ExactCompatibility { get; set; }
        public int? MinRating { get; set; }
        /// <summary>The order of the list (alphabetical by default).</summary>
        public SortBy SortBy { get; set; } = SortBy.Name;
        /// <summary>true: from Z to A, the newest, the best rated or the most compatible first.</summary>
        public bool Descending { get; set; }
        /// <summary>Keeps the games whose name contains this text (case and accents ignored); null: all.</summary>
        public string? Search { get; set; }
    }
}
