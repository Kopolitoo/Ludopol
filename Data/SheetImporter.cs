using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ludopol
{
    /// <summary>What an import did, for the summary shown at the end.</summary>
    public sealed class ImportResult
    {
        /// <summary>Game rows read in the sheet.</summary>
        public int Read { get; set; }
        public int Added { get; set; }
        public int Updated { get; set; }
        /// <summary>
        /// Games whose Steam link is the same as another row's: imported without Steam data,
        /// to be checked in the sheet.
        /// </summary>
        public List<string> SteamConflicts { get; } = new List<string>();
        /// <summary>The fictitious games removed (first import only).</summary>
        public int SamplesRemoved { get; set; }
    }

    /// <summary>
    /// LOGIC: imports the games sheet (export "jeux poursuite occulaire.xlsx" of the Google sheet).
    ///
    /// The columns are found by their header, so their order does not matter:
    ///   Jeu, Catégories, Essayé avec ma poursuite, Compatibilité, Ma note (1-5), Mes remarques,
    ///   À publier, Lien Steam, Image, Description, Date de sortie, Développeur, Genres,
    ///   Tags Steam, Prix, Français, and optionally Possédé sur Steam.
    /// "Lien Steam" gives the Steam number of the game through its hyperlink
    /// (https://store.steampowered.com/app/1815570); "Image" gives the image address through its
    /// formula =IMAGE("https://…").
    ///
    /// Two rows can point to the same Steam page by mistake ("Deponia" and "Deponia Doomsday"
    /// both linked to Deponia Doomsday). The row whose name is the link's text keeps the Steam
    /// data; the other is imported without it (name, categories and my fields only) and is
    /// listed in the summary, to be fixed in the sheet.
    ///
    /// A game already in the catalogue (same Steam number, otherwise same name) is UPDATED:
    /// its Steam data, categories and tags come from the sheet, but my own fields (owned,
    /// tried, compatibility, rating, notes, to publish) are only taken from the sheet while they
    /// are still empty in Ludopol: what was filled in Ludopol is never overwritten.
    /// </summary>
    public static class SheetImporter
    {
        private static readonly CultureInfo French = new CultureInfo("fr-FR");
        private static readonly Regex SteamAppId = new Regex(@"/app/(\d+)", RegexOptions.Compiled);
        private static readonly Regex ImageFormula = new Regex(@"IMAGE\(\s*""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Reads the file and turns each row into a Game (nothing is saved). Rows sharing a
        /// Steam link are sorted out as explained above; their names go to conflicts.
        /// </summary>
        public static List<Game> ReadGames(string path, List<string> conflicts)
        {
            List<Game> games = new List<Game>();
            Dictionary<Game, string> linkTexts = new Dictionary<Game, string>();
            DateTime now = DateTime.Now;
            foreach (Dictionary<string, XlsxCell> row in XlsxReader.ReadFirstSheet(path))
            {
                Game? game = ToGame(row, now);
                if (game != null)
                {
                    games.Add(game);
                    linkTexts[game] = Text(row, "Lien Steam");
                }
            }

            foreach (IGrouping<long, Game> group in games.Where(g => g.SteamAppId.HasValue)
                         .GroupBy(g => g.SteamAppId!.Value).Where(g => g.Count() > 1))
            {
                Game keeper = group.FirstOrDefault(g => string.Equals(g.Name.Trim(), linkTexts[g].Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? group.First();
                foreach (Game other in group.Where(g => g != keeper))
                {
                    ClearSteamData(other);
                    conflicts.Add(other.Name);
                }
            }
            return games;
        }

        /// <summary>Reads the file and merges its games into the catalogue.</summary>
        internal static ImportResult Import(string path, Database database)
        {
            ImportResult result = new ImportResult();
            List<Game> incoming = ReadGames(path, result.SteamConflicts);
            result.Read = incoming.Count;
            if (incoming.Count > 0)
            {
                result.SamplesRemoved = database.DeleteSampleGames();
            }

            foreach (Game game in incoming)
            {
                Game? existing = database.FindGame(game.SteamAppId, game.Name);
                if (existing == null)
                {
                    database.Insert(game);
                    result.Added++;
                }
                else
                {
                    MergeInto(existing, game);
                    database.UpdateFromImport(existing);
                    result.Updated++;
                }
            }
            App.Log("Import " + path + ": " + result.Read + " read, " + result.Added + " added, "
                + result.Updated + " updated, " + result.SteamConflicts.Count + " Steam link conflicts");
            return result;
        }

        /// <summary>
        /// Copies the sheet's data into a game of the catalogue: Steam data, categories and tags
        /// always; my own fields only where Ludopol still has nothing.
        /// </summary>
        public static void MergeInto(Game existing, Game sheet)
        {
            existing.Name = sheet.Name;
            existing.SteamAppId = sheet.SteamAppId ?? existing.SteamAppId;
            existing.ImageUrl = sheet.ImageUrl ?? existing.ImageUrl;
            existing.Description = sheet.Description ?? existing.Description;
            existing.ReleaseDate = sheet.ReleaseDate ?? existing.ReleaseDate;
            existing.Developer = sheet.Developer ?? existing.Developer;
            existing.Genres = sheet.Genres ?? existing.Genres;
            existing.Price = sheet.Price ?? existing.Price;
            existing.French = sheet.French ?? existing.French;
            existing.Categories = sheet.Categories;
            if (sheet.Tags.Count > 0)
            {
                existing.Tags = sheet.Tags;
            }

            if (!existing.OwnedOnSteam) existing.OwnedOnSteam = sheet.OwnedOnSteam;
            if (!existing.Tried) existing.Tried = sheet.Tried;
            if (existing.Compatibility == Compatibility.Unknown) existing.Compatibility = sheet.Compatibility;
            if (existing.Rating == null) existing.Rating = sheet.Rating;
            if (string.IsNullOrWhiteSpace(existing.Notes)) existing.Notes = sheet.Notes;
            if (!existing.ToPublish) existing.ToPublish = sheet.ToPublish;
        }

        private static void ClearSteamData(Game game)
        {
            game.SteamAppId = null;
            game.ImageUrl = null;
            game.Description = null;
            game.ReleaseDate = null;
            game.Developer = null;
            game.Genres = null;
            game.Price = null;
            game.French = null;
            game.Tags = new List<string>();
        }

        /// <summary>One row of the sheet → a Game, or null if the row has no name.</summary>
        public static Game? ToGame(Dictionary<string, XlsxCell> row, DateTime addedAt)
        {
            string name = Text(row, "Jeu");
            if (name.Length == 0)
            {
                return null;
            }
            Game game = new Game { Name = name, AddedAt = addedAt };

            game.Categories = SplitList(Text(row, "Catégories"), ';');
            game.Tags = SplitList(Text(row, "Tags Steam"), ',');
            game.Tried = IsYes(Text(row, "Essayé avec ma poursuite"));
            game.Compatibility = ParseCompatibility(Text(row, "Compatibilité"));
            game.Rating = int.TryParse(Text(row, "Ma note (1-5)").Split('.')[0], out int rating) && rating >= 1 && rating <= 5 ? rating : null;
            game.Notes = NullIfEmpty(Text(row, "Mes remarques"));
            game.ToPublish = IsYes(Text(row, "À publier"));
            game.OwnedOnSteam = IsYes(Text(row, "Possédé sur Steam"));

            if (row.TryGetValue("Lien Steam", out XlsxCell? link) && link.Hyperlink != null)
            {
                Match match = SteamAppId.Match(link.Hyperlink);
                if (match.Success)
                {
                    game.SteamAppId = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                }
            }
            if (row.TryGetValue("Image", out XlsxCell? image) && image.Formula != null)
            {
                Match match = ImageFormula.Match(image.Formula);
                if (match.Success)
                {
                    game.ImageUrl = match.Groups[1].Value;
                }
            }

            game.Description = NullIfEmpty(Text(row, "Description"));
            game.ReleaseDate = NullIfEmpty(DateText(row, "Date de sortie"));
            game.Developer = NullIfEmpty(Text(row, "Développeur"));
            game.Genres = NullIfEmpty(Text(row, "Genres"));
            game.Price = NullIfEmpty(PriceText(row, "Prix"));
            string french = Text(row, "Français");
            game.French = french.Length == 0 ? null : IsYes(french);
            return game;
        }

        /// <summary>Compatibility as written in the sheet → its level.</summary>
        public static Compatibility ParseCompatibility(string text)
        {
            string t = text.Trim().ToLowerInvariant();
            if (t.StartsWith("total")) return Compatibility.Full;
            if (t.StartsWith("aucun")) return Compatibility.None;
            if (t.StartsWith("partiel"))
            {
                return t.Contains("contacteur") || t.Contains("réflexe") || t.Contains("reflexe")
                    ? Compatibility.PartialWithSwitch
                    : Compatibility.PartialGaze;
            }
            return Compatibility.Unknown;
        }

        private static string Text(Dictionary<string, XlsxCell> row, string header) =>
            row.TryGetValue(header, out XlsxCell? cell) ? cell.Text.Trim() : "";

        /// <summary>A date is a number of days in Excel: written back as "17 sept. 2019".</summary>
        private static string DateText(Dictionary<string, XlsxCell> row, string header)
        {
            if (row.TryGetValue(header, out XlsxCell? cell) && cell.IsNumber
                && double.TryParse(cell.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double days))
            {
                return DateTime.FromOADate(days).ToString("d MMM yyyy", French);
            }
            return Text(row, header);
        }

        /// <summary>A price is a number in Excel: written back as "19,50€".</summary>
        private static string PriceText(Dictionary<string, XlsxCell> row, string header)
        {
            if (row.TryGetValue(header, out XlsxCell? cell) && cell.IsNumber
                && double.TryParse(cell.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double price))
            {
                return price.ToString("0.00", French) + "€";
            }
            return Text(row, header);
        }

        private static List<string> SplitList(string text, char separator) =>
            text.Split(separator).Select(s => s.Trim()).Where(s => s.Length > 0).Distinct().ToList();

        private static bool IsYes(string text)
        {
            string t = text.Trim().ToLowerInvariant();
            return t == "oui" || t == "yes" || t == "true" || t == "1" || t == "x";
        }

        private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;
    }
}
