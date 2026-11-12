using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ludopol
{
    /// <summary>
    /// LOGIC: completes the page of a Steam game from the Steam store, for the games that
    /// came without a description (added by the Steam synchronisation, for example).
    ///
    ///   - description, developer, release date, genres, price, French, image: Steam's store
    ///     service (store.steampowered.com/api/appdetails), in the interface language;
    ///   - tags: they are not in that service, so they are read from the game's store page
    ///     (the links "app_tag"), in English like the sheet. If Steam changes its page, the tags
    ///     are simply not found: the rest is kept.
    ///
    /// Only empty fields are filled: nothing written in the sheet or in Ludopol is replaced.
    /// The categories stay mine (a game added from Steam stays in "To sort (Steam)").
    ///
    /// Steam limits the number of requests (about 200 per 5 minutes): CompleteAll waits
    /// 1.5 s between two games.
    /// </summary>
    internal static class SteamStore
    {
        private static readonly HttpClient Http = CreateClient();

        /// <summary>What CompleteAll is doing, shown on the Steam settings page; null when idle.</summary>
        public static string? Status { get; private set; }

        public static bool Running { get; private set; }

        /// <summary>Raised (on the interface thread) each time Status changes.</summary>
        public static event Action? StatusChanged;

        /// <summary>True when the game has a Steam number but no description: worth completing.</summary>
        public static bool NeedsDetails(Game game) => game.SteamAppId.HasValue && string.IsNullOrWhiteSpace(game.Description);

        /// <summary>
        /// Completes one game (its page reads it again afterwards). Returns true if something was
        /// added. Throws on a network error.
        /// </summary>
        public static async Task<bool> CompleteAsync(Game game, Database database)
        {
            if (!game.SteamAppId.HasValue)
            {
                return false;
            }
            long id = game.SteamAppId.Value;
            bool changed = false;

            string language = AppSettings.Current.Language == "fr" ? "french" : "english";
            string json = await Http.GetStringAsync(
                "https://store.steampowered.com/api/appdetails?appids=" + id.ToString(CultureInfo.InvariantCulture)
                + "&l=" + language + "&cc=" + (language == "french" ? "fr" : "us"));
            changed |= ApplyDetails(game, json);

            if (game.Tags.Count == 0)
            {
                try
                {
                    string page = await Http.GetStringAsync(
                        "https://store.steampowered.com/app/" + id.ToString(CultureInfo.InvariantCulture) + "/?l=english");
                    List<string> tags = ReadTags(page);
                    if (tags.Count > 0)
                    {
                        game.Tags = tags;
                        changed = true;
                    }
                }
                catch (Exception ex)
                {
                    App.Log("Steam tags not read for \"" + game.Name + "\": " + ex.Message);
                }
            }

            if (changed)
            {
                database.UpdateFromImport(game);
                App.Log("Steam store: \"" + game.Name + "\" completed");
            }
            return changed;
        }

        /// <summary>Completes every Steam game without a description, one after the other, in the background.</summary>
        public static async Task CompleteAllAsync(Database database)
        {
            if (Running)
            {
                return;
            }
            Running = true;
            List<Game> games = database.GetGames(new GameFilter())
                .Where(NeedsDetails)
                .Select(g => database.GetGame(g.Id))
                .Where(g => g != null)
                .Select(g => g!)
                .ToList();
            int done = 0;
            int completed = 0;
            foreach (Game game in games)
            {
                SetStatus(Texts.Format("steamStore.progress", done, games.Count));
                try
                {
                    if (await CompleteAsync(game, database))
                    {
                        completed++;
                    }
                }
                catch (Exception ex)
                {
                    App.Log("Steam store: \"" + game.Name + "\" not completed: " + ex.Message);
                }
                done++;
                await Task.Delay(1500);
            }
            Running = false;
            SetStatus(Texts.Format("steamStore.done", completed, games.Count));
        }

        /// <summary>
        /// Reads appdetails' answer and fills the empty fields. Returns true if something was added.
        /// {"470780":{"success":true,"data":{"short_description":"…","developers":["…"],…}}}
        /// </summary>
        public static bool ApplyDetails(Game game, string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                JsonElement entry = default;
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    entry = property.Value;
                    break;
                }
                if (entry.ValueKind != JsonValueKind.Object
                    || !entry.TryGetProperty("success", out JsonElement success) || success.ValueKind != JsonValueKind.True
                    || !entry.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Object)
                {
                    return false;   // not (or no longer) in the store
                }

                bool changed = false;
                changed |= Fill(() => game.Description, v => game.Description = v, Clean(String(data, "short_description")));
                changed |= Fill(() => game.Developer, v => game.Developer = v, FirstOf(data, "developers"));
                changed |= Fill(() => game.ReleaseDate, v => game.ReleaseDate = v,
                    data.TryGetProperty("release_date", out JsonElement release) ? String(release, "date") : null);
                changed |= Fill(() => game.Genres, v => game.Genres = v, Descriptions(data, "genres"));
                string? price = data.TryGetProperty("is_free", out JsonElement free) && free.ValueKind == JsonValueKind.True
                    ? Texts.Get("steamStore.free")
                    : data.TryGetProperty("price_overview", out JsonElement overview) ? String(overview, "final_formatted") : null;
                changed |= Fill(() => game.Price, v => game.Price = v, price);
                // The image: Steam's own address replaces an empty one and any Steam address made
                // by Ludopol (…/apps/NUMBER/header.jpg, which no longer exists for recent games).
                // An image of mine (a file, another site) stays.
                string? header = String(data, "header_image");
                if (!string.IsNullOrWhiteSpace(header)
                    && (string.IsNullOrWhiteSpace(game.ImageUrl) || IsSteamImage(game.ImageUrl))
                    && game.ImageUrl != header)
                {
                    game.ImageUrl = header;
                    changed = true;
                }
                if (!game.French.HasValue && String(data, "supported_languages") is string languages)
                {
                    game.French = languages.Contains("Français", StringComparison.OrdinalIgnoreCase)
                                  || languages.Contains("French", StringComparison.OrdinalIgnoreCase);
                    changed = true;
                }
                return changed;
            }
        }

        /// <summary>
        /// The Steam number written in an address or alone: "https://store.steampowered.com/app/470780/She_…",
        /// "steam://store/470780", "470780". Null when none is found.
        /// </summary>
        public static long? ParseAppId(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            Match match = Regex.Match(text.Trim(), @"^(\d+)$|/app/(\d+)|steam://\w+/(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }
            string digits = match.Groups[1].Success ? match.Groups[1].Value
                : match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
            return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out long id) && id > 0 ? id : null;
        }

        /// <summary>
        /// Gives a game another Steam number (typed by hand): what came from the previous Steam
        /// page (description, tags, image…) is removed, so that CompleteAsync fills everything
        /// again from the new one. My own fields (categories, notes, rating…) stay.
        /// </summary>
        public static void ChangeAppId(Game game, long appId, Database database)
        {
            if (game.SteamAppId != appId)
            {
                game.Description = null;
                game.ReleaseDate = null;
                game.Developer = null;
                game.Genres = null;
                game.Price = null;
                game.French = null;
                game.Tags = new List<string>();
                game.OwnedOnSteam = false;   // known again at the next synchronisation
            }
            if (game.SteamAppId != appId || string.IsNullOrWhiteSpace(game.ImageUrl))
            {
                game.ImageUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/"
                                + appId.ToString(CultureInfo.InvariantCulture) + "/header.jpg";
            }
            game.SteamAppId = appId;
            database.UpdateFromImport(game);
            App.Log("Steam number of \"" + game.Name + "\" set by hand: " + appId);
        }

        /// <summary>True for an image address on Steam's servers (not one of my own images).</summary>
        public static bool IsSteamImage(string? address) =>
            Uri.TryCreate(address, UriKind.Absolute, out Uri? uri)
            && (uri.Host.EndsWith("steamstatic.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith("steampowered.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith("steamcdn-a.akamaihd.net", StringComparison.OrdinalIgnoreCase));

        /// <summary>The address of a game's image as Steam gives it (appdetails), or null. Throws on a network error.</summary>
        public static async Task<string?> GetHeaderImageAsync(long appId)
        {
            string json = await Http.GetStringAsync(
                "https://store.steampowered.com/api/appdetails?appids=" + appId.ToString(CultureInfo.InvariantCulture) + "&filters=basic");
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    if (property.Value.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Object)
                    {
                        return String(data, "header_image");
                    }
                }
            }
            return null;
        }

        /// <summary>The tags of a store page: the links with the class "app_tag", in Steam's order.</summary>
        public static List<string> ReadTags(string html)
        {
            List<string> tags = new List<string>();
            foreach (Match match in Regex.Matches(html, "<a[^>]*class=\"app_tag\"[^>]*>\\s*([^<]+?)\\s*</a>"))
            {
                string tag = WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
                if (tag.Length > 0 && tag != "+" && !tags.Contains(tag))
                {
                    tags.Add(tag);
                }
            }
            return tags;
        }

        private static bool Fill(Func<string?> current, Action<string> set, string? value)
        {
            if (!string.IsNullOrWhiteSpace(current()) || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            set(value.Trim());
            return true;
        }

        private static string? String(JsonElement element, string name) =>
            element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static string? FirstOf(JsonElement element, string name) =>
            element.TryGetProperty(name, out JsonElement list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()).FirstOrDefault()
                : null;

        private static string? Descriptions(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out JsonElement list) || list.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            List<string> names = list.EnumerateArray().Select(v => String(v, "description")).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToList();
            return names.Count > 0 ? string.Join(", ", names) : null;
        }

        /// <summary>Steam's short descriptions can hold HTML codes (&amp;quot;…) and tags.</summary>
        private static string? Clean(string? text) =>
            text == null ? null : Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]+>", " ")), "\\s+", " ").Trim();

        private static void SetStatus(string text)
        {
            Status = text;
            StatusChanged?.Invoke();
        }

        private static HttpClient CreateClient()
        {
            // The cookies let the store pages of games for adults through (no age check page).
            HttpClientHandler handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
            Uri store = new Uri("https://store.steampowered.com");
            handler.CookieContainer.Add(store, new Cookie("birthtime", "0"));
            handler.CookieContainer.Add(store, new Cookie("mature_content", "1"));
            handler.CookieContainer.Add(store, new Cookie("wants_mature_content", "1"));
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        }
    }
}
