using System;
using System.Collections.Generic;

namespace Ludopol
{
    /// <summary>
    /// How well a game can be played with the eyetracker. The numbers are stored in the
    /// database and go from worst to best (Unknown aside): a filter "at least X" keeps
    /// the values >= X.
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

        // --- My own data ---
        public List<string> Categories { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
        public bool Tried { get; set; }
        public Compatibility Compatibility { get; set; } = Compatibility.Unknown;
        /// <summary>My rating from 1 to 5, or null.</summary>
        public int? Rating { get; set; }
        public string? Notes { get; set; }
        public bool ToPublish { get; set; }

        // --- On this computer (step 4) ---
        public bool Installed { get; set; }
        /// <summary>How to start the game: a steam:// address or a program path.</summary>
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

    /// <summary>A name with its number of games, for the Categories, Tags and Developers pages.</summary>
    public sealed class NamedCount
    {
        public NamedCount(string name, int count)
        {
            Name = name;
            Count = count;
        }

        public string Name { get; }
        public int Count { get; }
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
        public bool InstalledOnly { get; set; }
        /// <summary>true: tried games only; false: not tried yet; null: all.</summary>
        public bool? Tried { get; set; }
        /// <summary>Keeps the games at least this compatible (Unknown is then excluded).</summary>
        public Compatibility? MinCompatibility { get; set; }
        public int? MinRating { get; set; }
        /// <summary>true: most recently added first; false: alphabetical order.</summary>
        public bool NewestFirst { get; set; }
    }
}
