using System;
using System.Collections.Generic;

namespace Ludopol
{
    /// <summary>
    /// MODEL: two FICTITIOUS games, put in an empty catalogue so that the interface can be
    /// tried with the eyetracker before the real import (step 3). They do not exist: no image,
    /// no Steam page.
    /// </summary>
    internal static class SampleGames
    {
        public static IEnumerable<Game> Create()
        {
            yield return new Game
            {
                Name = "Le Jardin des Lucioles",
                Description = "Jeu fictif pour essayer Ludopol. Un récit tranquille : on soigne un jardin "
                    + "la nuit et on découvre l'histoire de ses anciens jardiniers, une lettre à la fois.",
                ReleaseDate = "3 mars 2025",
                Developer = "Studio Imaginaire",
                Genres = "Aventure, Indépendant",
                Price = "9,99€",
                French = true,
                OwnedOnSteam = true,
                Categories = new List<string> { "Romans visuels / Narratif" },
                Tags = new List<string> { "Visual Novel", "Story Rich", "Relaxing", "Singleplayer" },
                Tried = true,
                Compatibility = Compatibility.Full,
                Rating = 4,
                Notes = "Tout se fait au regard, les textes défilent tout seuls.",
                AddedAt = DateTime.Now.AddDays(-1),
            };
            yield return new Game
            {
                Name = "Les Échecs du Phare",
                Description = "Jeu fictif pour essayer Ludopol. Des problèmes d'échecs posés par le "
                    + "gardien d'un phare, sans limite de temps.",
                ReleaseDate = "12 sept. 2024",
                Developer = "Atelier Fictif",
                Genres = "Stratégie, Indépendant",
                Price = "4,99€",
                French = false,
                Categories = new List<string> { "Réflexion / Logique / Échecs", "Jeux de plateau / de cartes adaptés" },
                Tags = new List<string> { "Chess", "Puzzle", "Turn-Based", "Singleplayer" },
                Tried = false,
                Compatibility = Compatibility.Unknown,
                AddedAt = DateTime.Now,
            };
        }
    }
}
