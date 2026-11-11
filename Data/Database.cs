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
        private const int SchemaVersion = 1;

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
                if (version < SchemaVersion)
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
                        PRAGMA user_version = " + SchemaVersion + ";");
                    App.Log("Database: tables created (version " + SchemaVersion + ")");
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
                                       genres, price, french, tried, compatibility, rating, notes, to_publish,
                                       installed, launch_target, added_at)
                    VALUES ($name, $steam, $image, $description, $release, $developer,
                            $genres, $price, $french, $tried, $compatibility, $rating, $notes, $publish,
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
                transaction.Commit();
            }
        }

        /// <summary>Saves what I change on a game's page: tried, compatibility, rating, notes, to publish.</summary>
        public void SaveUserFields(Game game)
        {
            using (SqliteConnection connection = Connect())
            {
                SqliteCommand command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE games SET tried = $tried, compatibility = $compatibility, rating = $rating,
                                     notes = $notes, to_publish = $publish
                    WHERE id = $id";
                command.Parameters.AddWithValue("$tried", game.Tried ? 1 : 0);
                command.Parameters.AddWithValue("$compatibility", (int)game.Compatibility);
                command.Parameters.AddWithValue("$rating", (object?)game.Rating ?? DBNull.Value);
                command.Parameters.AddWithValue("$notes", string.IsNullOrWhiteSpace(game.Notes) ? DBNull.Value : game.Notes);
                command.Parameters.AddWithValue("$publish", game.ToPublish ? 1 : 0);
                command.Parameters.AddWithValue("$id", game.Id);
                command.ExecuteNonQuery();
            }
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
                if (filter.InstalledOnly)
                {
                    sql.Append(" AND g.installed = 1");
                }
                if (filter.Tried.HasValue)
                {
                    sql.Append(" AND g.tried = $tried");
                    command.Parameters.AddWithValue("$tried", filter.Tried.Value ? 1 : 0);
                }
                if (filter.MinCompatibility.HasValue)
                {
                    sql.Append(" AND g.compatibility >= $compatibility");
                    command.Parameters.AddWithValue("$compatibility", (int)filter.MinCompatibility.Value);
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

        /// <summary>The categories, with their number of games, in alphabetical order.</summary>
        public List<NamedCount> GetCategories() =>
            ReadCounts("SELECT name, COUNT(*) FROM game_categories GROUP BY name")
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
                        result.Add(new NamedCount(reader.GetString(0), reader.GetInt32(1)));
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
