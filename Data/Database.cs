using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Ludopol
{
    /// <summary>
    /// LOGIC: the catalogue, stored in a local SQLite database (one file, ludopol.db).
    ///
    /// Three tables:
    ///   games            one row per game (the columns of Game.cs);
    ///   game_categories  the categories of each game (a game can have several);
    ///   game_tags        the Steam tags of each game, in Steam's order (position).
    ///
    /// Each method opens its own connection and closes it: simple, and fast enough for
    /// a few hundred games.
    /// </summary>
    internal sealed class Database
    {
        /// <summary>
        /// Version of the table layout. When it changes in a later step, Open() will update
        /// an older file instead of losing the catalogue.
        /// </summary>
        private const int SchemaVersion = 2;

        private readonly string _connectionString;

        /// <summary>Sorts names the French way (accents, no case difference).</summary>
        private static readonly StringComparer NameOrder = StringComparer.Create(new CultureInfo("fr-FR"), true);

        public Database(string path)
        {
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                ForeignKeys = true,
            }.ToString();
        }

        /// <summary>Creates the tables if needed, and the two fictitious games if the catalogue is empty.</summary>
        public void Open()
        {
            using (SqliteConnection connection = Connect())
            {
                long version = (long)(Scalar(connection, "PRAGMA user_version") ?? 0L);
                if (version < 1)
                {
                    Execute(connection, @"
                        CREATE TABLE IF NOT EXISTS games (
                            id             INTEGER PRIMARY KEY AUTOINCREMENT,
                            name           TEXT NOT NULL,
                            steam_app_id   INTEGER,
                            image_url      TEXT,
                            description    TEXT,
                            release_date   TEXT,
                            developer      TEXT,
                            genres         TEXT,
                            price          TEXT,
                            french         INTEGER,
                            tried          INTEGER NOT NULL DEFAULT 0,
                            compatibility  INTEGER NOT NULL DEFAULT 0,
                            rating         INTEGER,
                            notes          TEXT,
                            to_publish     INTEGER NOT NULL DEFAULT 0,
                            installed      INTEGER NOT NULL DEFAULT 0,
                            launch_target  TEXT,
                            added_at       TEXT NOT NULL
                        );
                        CREATE TABLE IF NOT EXISTS game_categories (
                            game_id  INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
                            name     TEXT NOT NULL,
                            PRIMARY KEY (game_id, name)
                        );
                        CREATE TABLE IF NOT EXISTS game_tags (
                            game_id   INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
                            name      TEXT NOT NULL,
                            position  INTEGER NOT NULL,
                            PRIMARY KEY (game_id, name)
                        );
                        PRAGMA user_version = 1;");
                    App.Log("Database: tables created (version 1)");
                }
                if (version < 2)
                {
                    // Version 2: "owned on Steam".
                    Execute(connection, @"
                        ALTER TABLE games ADD COLUMN owned_on_steam INTEGER NOT NULL DEFAULT 0;
                        PRAGMA user_version = 2;");
                    App.Log("Database: updated to version 2");
                }

                long count = (long)(Scalar(connection, "SELECT COUNT(*) FROM games") ?? 0L);
                if (count == 0)
                {
                    foreach (Game game in SampleGames.Create())
                    {
                        Insert(game);
                    }
                    App.Log("Database: empty catalogue, fictitious games added");
                }
            }
        }

        /// <summary>Adds a game with its categories and tags. Sets game.Id.</summary>
        public void Insert(Game game)
        {
            using (SqliteConnection connection = Connect())
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                SqliteCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT INTO games (name, steam_app_id, image_url, description, release_date, developer,
                                       genres, price, french, owned_on_steam, tried, compatibility, rating, notes, to_publish,
                                       installed, launch_target, added_at)
                    VALUES ($name, $steam, $image, $description, $release, $developer,
                            $genres, $price, $french, $owned, $tried, $compatibility, $rating, $notes, $publish,
                            $installed, $launch, $added)";
                command.Parameters.AddWithValue("$name", game.Name);
                command.Parameters.AddWithValue("$steam", (object?)game.SteamAppId ?? DBNull.Value);
                command.Parameters.AddWithValue("$image", (object?)game.ImageUrl ?? DBNull.Value);
                command.Parameters.AddWithValue("$description", (object?)game.Description ?? DBNull.Value);
                command.Parameters.AddWithValue("$release", (object?)game.ReleaseDate ?? DBNull.Value);
                command.Parameters.AddWithValue("$developer", (object?)game.Developer ?? DBNull.Value);
                command.Parameters.AddWithValue("$genres", (object?)game.Genres ?? DBNull.Value);
                command.Parameters.AddWithValue("$price", (object?)game.Price ?? DBNull.Value);
                command.Parameters.AddWithValue("$french", game.French.HasValue ? (object)(game.French.Value ? 1 : 0) : DBNull.Value);
                command.Parameters.AddWithValue("$owned", game.OwnedOnSteam ? 1 : 0);
                command.Parameters.AddWithValue("$tried", game.Tried ? 1 : 0);
                command.Parameters.AddWithValue("$compatibility", (int)game.Compatibility);
                command.Parameters.AddWithValue("$rating", (object?)game.Rating ?? DBNull.Value);
                command.Parameters.AddWithValue("$notes", (object?)game.Notes ?? DBNull.Value);
                command.Parameters.AddWithValue("$publish", game.ToPublish ? 1 : 0);
                command.Parameters.AddWithValue("$installed", game.Installed ? 1 : 0);
                command.Parameters.AddWithValue("$launch", (object?)game.LaunchTarget ?? DBNull.Value);
                command.Parameters.AddWithValue("$added", game.AddedAt.ToString("o", CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();

                SqliteCommand lastId = connection.CreateCommand();
                lastId.Transaction = transaction;
                lastId.CommandText = "SELECT last_insert_rowid()";
                game.Id = (long)lastId.ExecuteScalar()!;

                InsertNames(connection, transaction, game);
                transaction.Commit();
            }
        }

        /// <summary>Saves what I change on a game's page: owned on Steam, tried, compatibility, rating, notes, to publish.</summary>
        public void SaveUserFields(Game game)
        {
            using (SqliteConnection connection = Connect())
            {
                SqliteCommand command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE games SET owned_on_steam = $owned, tried = $tried, compatibility = $compatibility, rating = $rating,
                                     notes = $notes, to_publish = $publish
                    WHERE id = $id";
                command.Parameters.AddWithValue("$owned", game.OwnedOnSteam ? 1 : 0);
                command.Parameters.AddWithValue("$tried", game.Tried ? 1 : 0);
                command.Parameters.AddWithValue("$compatibility", (int)game.Compatibility);
                command.Parameters.AddWithValue("$rating", (object?)game.Rating ?? DBNull.Value);
                command.Parameters.AddWithValue("$notes", string.IsNullOrWhiteSpace(game.Notes) ? DBNull.Value : game.Notes);
                command.Parameters.AddWithValue("$publish", game.ToPublish ? 1 : 0);
                command.Parameters.AddWithValue("$id", game.Id);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Recomputes "installed" for every game with the given test (steam number, program),
        /// in one transaction. Returns how many games are installed.
        /// </summary>
        public int UpdateInstalled(Func<long?, string?, bool> isInstalled)
        {
            int installed = 0;
            using (SqliteConnection connection = Connect())
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                List<(long Id, bool Installed)> changes = new List<(long, bool)>();
                SqliteCommand read = connection.CreateCommand();
                read.Transaction = transaction;
                read.CommandText = "SELECT id, steam_app_id, launch_target, installed FROM games";
                using (SqliteDataReader reader = read.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        long? steam = reader.IsDBNull(1) ? null : reader.GetInt64(1);
                        string? target = reader.IsDBNull(2) ? null : reader.GetString(2);
                        bool now = isInstalled(steam, target);
                        if (now)
                        {
                            installed++;
                        }
                        if (now != (reader.GetInt64(3) == 1))
                        {
                            changes.Add((reader.GetInt64(0), now));
                        }
                    }
                }
                foreach ((long id, bool now) in changes)
                {
                    SqliteCommand update = connection.CreateCommand();
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE games SET installed = $installed WHERE id = $id";
                    update.Parameters.AddWithValue("$installed", now ? 1 : 0);
                    update.Parameters.AddWithValue("$id", id);
                    update.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            return installed;
        }

        /// <summary>
        /// After a Steam synchronisation: every game with a Steam number gets "owned on Steam"
        /// yes or no, according to the list. Games without a Steam number keep what was set by
        /// hand. Returns how many games are owned, and the Steam numbers of the catalogue.
        /// </summary>
        public (int Owned, HashSet<long> CatalogueIds) UpdateOwned(HashSet<long> ownedIds)
        {
            int owned = 0;
            HashSet<long> catalogueIds = new HashSet<long>();
            using (SqliteConnection connection = Connect())
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                List<(long Id, bool Owned)> games = new List<(long, bool)>();
                SqliteCommand read = connection.CreateCommand();
                read.Transaction = transaction;
                read.CommandText = "SELECT id, steam_app_id FROM games WHERE steam_app_id IS NOT NULL";
                using (SqliteDataReader reader = read.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        long steamId = reader.GetInt64(1);
                        catalogueIds.Add(steamId);
                        bool isOwned = ownedIds.Contains(steamId);
                        if (isOwned)
                        {
                            owned++;
                        }
                        games.Add((reader.GetInt64(0), isOwned));
                    }
                }
                foreach ((long id, bool isOwned) in games)
                {
                    SqliteCommand update = connection.CreateCommand();
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE games SET owned_on_steam = $owned WHERE id = $id";
                    update.Parameters.AddWithValue("$owned", isOwned ? 1 : 0);
                    update.Parameters.AddWithValue("$id", id);
                    update.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            return (owned, catalogueIds);
        }

        /// <summary>Saves where a game is on this computer: installed, and the program chosen to start it (or null).</summary>
        public void SaveInstallation(Game game)
        {
            using (SqliteConnection connection = Connect())
            {
                SqliteCommand command = connection.CreateCommand();
                command.CommandText = "UPDATE games SET installed = $installed, launch_target = $launch WHERE id = $id";
                command.Parameters.AddWithValue("$installed", game.Installed ? 1 : 0);
                command.Parameters.AddWithValue("$launch", (object?)game.LaunchTarget ?? DBNull.Value);
                command.Parameters.AddWithValue("$id", game.Id);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Finds a game: by its Steam number when it has one, otherwise by its name (case
        /// ignored). Returns it with its categories and tags, or null.
        /// </summary>
        public Game? FindGame(long? steamAppId, string name)
        {
            using (SqliteConnection connection = Connect())
            {
                SqliteCommand command = connection.CreateCommand();
                if (steamAppId.HasValue)
                {
                    command.CommandText = "SELECT id FROM games WHERE steam_app_id = $steam LIMIT 1";
                    command.Parameters.AddWithValue("$steam", steamAppId.Value);
                    if (command.ExecuteScalar() is long byId)
                    {
                        return GetGame(byId);
                    }
                }
                command = connection.CreateCommand();
                command.CommandText = "SELECT id FROM games WHERE lower(name) = lower($name) LIMIT 1";
                command.Parameters.AddWithValue("$name", name.Trim());
                return command.ExecuteScalar() is long byName ? GetGame(byName) : null;
            }
        }

        /// <summary>Saves every field of a game (after an import), with its categories and tags.</summary>
        public void UpdateFromImport(Game game)
        {
            using (SqliteConnection connection = Connect())
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                SqliteCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    UPDATE games SET name = $name, steam_app_id = $steam, image_url = $image,
                        description = $description, release_date = $release, developer = $developer,
                        genres = $genres, price = $price, french = $french, owned_on_steam = $owned,
                        tried = $tried, compatibility = $compatibility, rating = $rating, notes = $notes,
                        to_publish = $publish
                    WHERE id = $id";
                command.Parameters.AddWithValue("$name", game.Name);
                command.Parameters.AddWithValue("$steam", (object?)game.SteamAppId ?? DBNull.Value);
                command.Parameters.AddWithValue("$image", (object?)game.ImageUrl ?? DBNull.Value);
                command.Parameters.AddWithValue("$description", (object?)game.Description ?? DBNull.Value);
                command.Parameters.AddWithValue("$release", (object?)game.ReleaseDate ?? DBNull.Value);
                command.Parameters.AddWithValue("$developer", (object?)game.Developer ?? DBNull.Value);
                command.Parameters.AddWithValue("$genres", (object?)game.Genres ?? DBNull.Value);
                command.Parameters.AddWithValue("$price", (object?)game.Price ?? DBNull.Value);
                command.Parameters.AddWithValue("$french", game.French.HasValue ? (object)(game.French.Value ? 1 : 0) : DBNull.Value);
                command.Parameters.AddWithValue("$owned", game.OwnedOnSteam ? 1 : 0);
                command.Parameters.AddWithValue("$tried", game.Tried ? 1 : 0);
                command.Parameters.AddWithValue("$compatibility", (int)game.Compatibility);
                command.Parameters.AddWithValue("$rating", (object?)game.Rating ?? DBNull.Value);
                command.Parameters.AddWithValue("$notes", string.IsNullOrWhiteSpace(game.Notes) ? DBNull.Value : game.Notes);
                command.Parameters.AddWithValue("$publish", game.ToPublish ? 1 : 0);
                command.Parameters.AddWithValue("$id", game.Id);
                command.ExecuteNonQuery();

                SqliteCommand clear = connection.CreateCommand();
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM game_categories WHERE game_id = $id; DELETE FROM game_tags WHERE game_id = $id;";
                clear.Parameters.AddWithValue("$id", game.Id);
                clear.ExecuteNonQuery();
                InsertNames(connection, transaction, game);
                transaction.Commit();
            }
        }

        /// <summary>
        /// Removes the fictitious games (SampleGames.cs) if they are still there: same name and
        /// no Steam number. Returns how many were removed.
        /// </summary>
        public int DeleteSampleGames()
        {
            int removed = 0;
            using (SqliteConnection connection = Connect())
            {
                foreach (Game sample in SampleGames.Create())
                {
                    SqliteCommand command = connection.CreateCommand();
                    command.CommandText = "DELETE FROM games WHERE name = $name AND steam_app_id IS NULL";
                    command.Parameters.AddWithValue("$name", sample.Name);
                    removed += command.ExecuteNonQuery();
                }
            }
            if (removed > 0)
            {
                App.Log("Database: " + removed + " fictitious game(s) removed");
            }
            return removed;
        }

        /// <summary>The games matching a filter (without their categories and tags, not needed in lists).</summary>
        public List<Game> GetGames(GameFilter filter)
        {
            List<Game> games = new List<Game>();
            using (SqliteConnection connection = Connect())
            {
                SqliteCommand command = connection.CreateCommand();
                StringBuilder sql = new StringBuilder("SELECT * FROM games g WHERE 1 = 1");
                if (filter.Category != null)
                {
                    sql.Append(" AND EXISTS (SELECT 1 FROM game_categories c WHERE c.game_id = g.id AND c.name = $category)");
                    command.Parameters.AddWithValue("$category", filter.Category);
                }
                if (filter.Tag != null)
                {
                    sql.Append(" AND EXISTS (SELECT 1 FROM game_tags t WHERE t.game_id = g.id AND t.name = $tag)");
                    command.Parameters.AddWithValue("$tag", filter.Tag);
                }
                if (filter.Developer != null)
                {
                    sql.Append(" AND g.developer = $developer");
                    command.Parameters.AddWithValue("$developer", filter.Developer);
                }
                if (filter.Installed.HasValue)
                {
                    sql.Append(" AND g.installed = $installed");
                    command.Parameters.AddWithValue("$installed", filter.Installed.Value ? 1 : 0);
                }
                if (filter.Tried.HasValue)
                {
                    sql.Append(" AND g.tried = $tried");
                    command.Parameters.AddWithValue("$tried", filter.Tried.Value ? 1 : 0);
                }
                if (filter.OwnedOnSteam.HasValue)
                {
                    sql.Append(" AND g.owned_on_steam = $owned");
                    command.Parameters.AddWithValue("$owned", filter.OwnedOnSteam.Value ? 1 : 0);
                }
                if (filter.ExactCompatibility.HasValue)
                {
                    sql.Append(" AND g.compatibility = $compatibility");
                    command.Parameters.AddWithValue("$compatibility", (int)filter.ExactCompatibility.Value);
                }
                if (filter.MinRating.HasValue)
                {
                    sql.Append(" AND g.rating >= $rating");
                    command.Parameters.AddWithValue("$rating", filter.MinRating.Value);
                }
                command.CommandText = sql.ToString();

                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        games.Add(ReadGame(reader));
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                string wanted = Simplify(filter.Search);
                games = games.Where(g => Simplify(g.Name).Contains(wanted)).ToList();
            }

            return filter.NewestFirst
                ? games.OrderByDescending(g => g.AddedAt).ThenBy(g => g.Name, NameOrder).ToList()
                : games.OrderBy(g => g.Name, NameOrder).ToList();
        }

        /// <summary>One game with its categories and tags, or null if it no longer exists.</summary>
        public Game? GetGame(long id)
        {
            using (SqliteConnection connection = Connect())
            {
                SqliteCommand command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM games WHERE id = $id";
                command.Parameters.AddWithValue("$id", id);
                Game? game = null;
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        game = ReadGame(reader);
                    }
                }
                if (game == null)
                {
                    return null;
                }
                game.Categories = ReadNames(connection, "SELECT name FROM game_categories WHERE game_id = $id ORDER BY name", id);
                game.Tags = ReadNames(connection, "SELECT name FROM game_tags WHERE game_id = $id ORDER BY position", id);
                return game;
            }
        }

        /// <summary>
        /// The categories, with their number of games, in alphabetical order. Each one comes
        /// with the image of one of its games for its card: the best rated, then the most
        /// recently added.
        /// </summary>
        public List<NamedCount> GetCategories() =>
            ReadCounts(@"
                SELECT c.name, COUNT(*),
                       (SELECT g.image_url FROM games g
                        JOIN game_categories c2 ON c2.game_id = g.id
                        WHERE c2.name = c.name AND g.image_url IS NOT NULL AND g.image_url <> ''
                        ORDER BY g.rating IS NULL, g.rating DESC, g.added_at DESC
                        LIMIT 1)
                FROM game_categories c GROUP BY c.name")
                .OrderBy(c => c.Name, NameOrder).ToList();

        /// <summary>The Steam tags, the most used first.</summary>
        public List<NamedCount> GetTags() =>
            ReadCounts("SELECT name, COUNT(*) FROM game_tags GROUP BY name")
                .OrderByDescending(c => c.Count).ThenBy(c => c.Name, NameOrder).ToList();

        /// <summary>The developers, in alphabetical order.</summary>
        public List<NamedCount> GetDevelopers() =>
            ReadCounts("SELECT developer, COUNT(*) FROM games WHERE developer IS NOT NULL AND developer <> '' GROUP BY developer")
                .OrderBy(c => c.Name, NameOrder).ToList();

        // ----- Helpers -----

        /// <summary>A text without case, accents or extra spaces, to compare names: "Échecs " → "echecs".</summary>
        public static string Simplify(string text)
        {
            string decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder simple = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    simple.Append(c);
                }
            }
            return simple.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>Writes a game's categories and tags (tags in Steam's order).</summary>
        private static void InsertNames(SqliteConnection connection, SqliteTransaction transaction, Game game)
        {
            foreach (string category in game.Categories.Distinct())
            {
                SqliteCommand insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO game_categories (game_id, name) VALUES ($id, $name)";
                insert.Parameters.AddWithValue("$id", game.Id);
                insert.Parameters.AddWithValue("$name", category);
                insert.ExecuteNonQuery();
            }
            int position = 0;
            foreach (string tag in game.Tags.Distinct())
            {
                SqliteCommand insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO game_tags (game_id, name, position) VALUES ($id, $name, $position)";
                insert.Parameters.AddWithValue("$id", game.Id);
                insert.Parameters.AddWithValue("$name", tag);
                insert.Parameters.AddWithValue("$position", position++);
                insert.ExecuteNonQuery();
            }
        }

        private SqliteConnection Connect()
        {
            SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private static void Execute(SqliteConnection connection, string sql)
        {
            SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private static object? Scalar(SqliteConnection connection, string sql)
        {
            SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteScalar();
        }

        private List<NamedCount> ReadCounts(string sql)
        {
            List<NamedCount> result = new List<NamedCount>();
            using (SqliteConnection connection = Connect())
            {
                SqliteCommand command = connection.CreateCommand();
                command.CommandText = sql;
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // A third column, when there is one, is an image address.
                        string? image = reader.FieldCount > 2 && !reader.IsDBNull(2) ? reader.GetString(2) : null;
                        result.Add(new NamedCount(reader.GetString(0), reader.GetInt32(1), image));
                    }
                }
            }
            return result;
        }

        private static List<string> ReadNames(SqliteConnection connection, string sql, long id)
        {
            List<string> names = new List<string>();
            SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$id", id);
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    names.Add(reader.GetString(0));
                }
            }
            return names;
        }

        /// <summary>Turns the current row of a "SELECT * FROM games" into a Game.</summary>
        private static Game ReadGame(SqliteDataReader r)
        {
            return new Game
            {
                Id = r.GetInt64(r.GetOrdinal("id")),
                Name = r.GetString(r.GetOrdinal("name")),
                SteamAppId = NullableLong(r, "steam_app_id"),
                ImageUrl = NullableString(r, "image_url"),
                Description = NullableString(r, "description"),
                ReleaseDate = NullableString(r, "release_date"),
                Developer = NullableString(r, "developer"),
                Genres = NullableString(r, "genres"),
                Price = NullableString(r, "price"),
                French = NullableLong(r, "french") is long french ? french == 1 : (bool?)null,
                OwnedOnSteam = r.GetInt64(r.GetOrdinal("owned_on_steam")) == 1,
                Tried = r.GetInt64(r.GetOrdinal("tried")) == 1,
                Compatibility = (Compatibility)r.GetInt32(r.GetOrdinal("compatibility")),
                Rating = (int?)NullableLong(r, "rating"),
                Notes = NullableString(r, "notes"),
                ToPublish = r.GetInt64(r.GetOrdinal("to_publish")) == 1,
                Installed = r.GetInt64(r.GetOrdinal("installed")) == 1,
                LaunchTarget = NullableString(r, "launch_target"),
                AddedAt = DateTime.Parse(r.GetString(r.GetOrdinal("added_at")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            };
        }

        private static string? NullableString(SqliteDataReader r, string column)
        {
            int i = r.GetOrdinal(column);
            return r.IsDBNull(i) ? null : r.GetString(i);
        }

        private static long? NullableLong(SqliteDataReader r, string column)
        {
            int i = r.GetOrdinal(column);
            return r.IsDBNull(i) ? null : r.GetInt64(i);
        }
    }
}
