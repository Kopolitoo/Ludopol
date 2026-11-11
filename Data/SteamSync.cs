using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ludopol
{
    /// <summary>
    /// LOGIC: the games bought on Steam, asked to Steam over the Internet.
    ///
    /// Steam's local files only describe the installed games, so the list of the games owned
    /// comes from Steam's web service (IPlayerService/GetOwnedGames). It needs:
    ///   - the account's number (SteamID64), read from Steam's files (SteamLibrary.Accounts);
    ///   - a Steam Web API key, free, created once on https://steamcommunity.com/dev/apikey
    ///     and saved in settings.json (SteamApiKey).
    ///
    /// Then:
    ///   - every game of the catalogue with a Steam number gets "Owned on Steam" = yes or no;
    ///   - the owned games missing from the catalogue are listed, and can be added (name, Steam
    ///     number and image only) in a category "To sort (Steam)".
    /// </summary>
    internal static class SteamSync
    {
        public const string ApiKeyPage = "https://steamcommunity.com/dev/apikey";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>Asks Steam for the games owned by an account. Throws with a readable message on failure.</summary>
        public static async Task<List<OwnedGame>> GetOwnedGamesAsync(string apiKey, string steamId)
        {
            string url = "https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/"
                         + "?key=" + Uri.EscapeDataString(apiKey.Trim())
                         + "&steamid=" + Uri.EscapeDataString(steamId)
                         + "&include_appinfo=1&include_played_free_games=1&format=json";
            using (HttpResponseMessage response = await Http.GetAsync(url))
            {
                if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                {
                    throw new InvalidOperationException(Texts.Get("steamSync.badKey"));
                }
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                return ParseOwnedGames(json);
            }
        }

        /// <summary>
        /// Reads Steam's answer: {"response":{"game_count":2,"games":[{"appid":10,"name":"…"},…]}}.
        /// An answer without "games" means Steam would not tell (profile's game details not public).
        /// </summary>
        public static List<OwnedGame> ParseOwnedGames(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                if (!document.RootElement.TryGetProperty("response", out JsonElement response)
                    || !response.TryGetProperty("games", out JsonElement games)
                    || games.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException(Texts.Get("steamSync.private"));
                }
                List<OwnedGame> owned = new List<OwnedGame>();
                foreach (JsonElement game in games.EnumerateArray())
                {
                    if (!game.TryGetProperty("appid", out JsonElement id) || !id.TryGetInt64(out long appId))
                    {
                        continue;
                    }
                    string name = game.TryGetProperty("name", out JsonElement nameElement) && nameElement.ValueKind == JsonValueKind.String
                        ? nameElement.GetString() ?? ""
                        : "";
                    owned.Add(new OwnedGame(appId, name.Length > 0 ? name : "Steam " + appId.ToString(CultureInfo.InvariantCulture)));
                }
                return owned;
            }
        }

        /// <summary>
        /// Saves "Owned on Steam" for the games of the catalogue and returns what changed,
        /// with the owned games that are not in the catalogue.
        /// </summary>
        public static SyncResult Apply(List<OwnedGame> owned, Database database)
        {
            HashSet<long> ownedIds = new HashSet<long>(owned.Select(g => g.AppId));
            (int ownedInCatalogue, HashSet<long> catalogueIds) = database.UpdateOwned(ownedIds);
            List<OwnedGame> missing = owned
                .Where(g => !catalogueIds.Contains(g.AppId))
                .OrderBy(g => g.Name, StringComparer.Create(new CultureInfo("fr-FR"), true))
                .ToList();
            App.Log("Steam sync: " + owned.Count + " owned, " + ownedInCatalogue + " in the catalogue, " + missing.Count + " missing");
            return new SyncResult(owned.Count, ownedInCatalogue, missing);
        }

        /// <summary>Adds owned games to the catalogue, in the category "To sort (Steam)". Returns how many.</summary>
        public static int AddMissing(IEnumerable<OwnedGame> games, Database database)
        {
            string category = Texts.Get("steamSync.newCategory");
            int added = 0;
            foreach (OwnedGame owned in games)
            {
                // Checked again: the game may have been added by an import meanwhile.
                if (database.FindGame(owned.AppId, owned.Name) != null)
                {
                    continue;
                }
                database.Insert(new Game
                {
                    Name = owned.Name,
                    SteamAppId = owned.AppId,
                    ImageUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/"
                               + owned.AppId.ToString(CultureInfo.InvariantCulture) + "/header.jpg",
                    OwnedOnSteam = true,
                    Categories = new List<string> { category },
                    AddedAt = DateTime.Now,
                });
                added++;
            }
            App.Log("Steam sync: " + added + " games added to the catalogue");
            return added;
        }
    }

    /// <summary>A game owned on Steam: its number and its name.</summary>
    internal sealed class OwnedGame
    {
        public OwnedGame(long appId, string name)
        {
            AppId = appId;
            Name = name;
        }

        public long AppId { get; }
        public string Name { get; }
    }

    /// <summary>What a synchronisation found.</summary>
    internal sealed class SyncResult
    {
        public SyncResult(int owned, int ownedInCatalogue, List<OwnedGame> missing)
        {
            Owned = owned;
            OwnedInCatalogue = ownedInCatalogue;
            Missing = missing;
        }

        /// <summary>Number of games owned on Steam.</summary>
        public int Owned { get; }
        /// <summary>Number of games of the catalogue now marked "Owned on Steam".</summary>
        public int OwnedInCatalogue { get; }
        /// <summary>The owned games that are not in the catalogue.</summary>
        public List<OwnedGame> Missing { get; }
    }
}
