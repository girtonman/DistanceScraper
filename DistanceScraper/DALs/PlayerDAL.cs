using MySqlConnector;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistanceScraper.DALs
{
	public class PlayerDAL
	{
		public PlayerDAL() { }

		public async Task<Dictionary<ulong, Player>> GetPlayers(List<ulong> steamIDs)
		{
			var Connection = new MySqlConnection(Settings.ConnectionString);
			Connection.Open();
			var sql = $"SELECT ID, SteamID, Name FROM Players WHERE SteamID IN ({string.Join(",", steamIDs)})";
			var command = new MySqlCommand(sql, Connection);
			var reader = await command.ExecuteReaderAsync();

			Dictionary<ulong, Player> players = new Dictionary<ulong, Player>();
			while (reader.Read())
			{
				players[reader.GetUInt64(1)] = new Player()
				{
					ID = reader.GetUInt32(0),
					SteamID = reader.GetUInt64(1),
					Name = reader.IsDBNull(2) ? null : reader.GetString(2),
				};
			}
			reader.Close();
			Connection.Close();

			return players;
		}

		public async Task UpdatePlayers(List<PlayerSummary> players, string source)
		{
			var Connection = new MySqlConnection(Settings.ConnectionString);
			Connection.Open();

			foreach (var player in players)
			{
				var sql = @"
					UPDATE Players SET
					Name = @name,
					SteamAvatar = @avatar
					WHERE SteamID = @steamID
				";

				// Create the sql query
				var command = new MySqlCommand(sql, Connection);
				command.Parameters.AddWithValue("@name", player.Name);
				command.Parameters.AddWithValue("@avatar", player.Avatar);
				command.Parameters.AddWithValue("@steamID", player.SteamID);

				if (Settings.Verbose)
				{
					Utils.WriteLine(source, $"Updating {player.SteamID}:{player.Name}");
				}
				await command.ExecuteNonQueryAsync();
			}

			Connection.Close();
		}

		public async Task<List<ulong>> GetSteamIDsForUpdating()
		{
			var Connection = new MySqlConnection(Settings.ConnectionString);
			Connection.Open();
			var sql = "SELECT SteamID FROM Players WHERE Name IS NULL";
			var command = new MySqlCommand(sql, Connection);
			var reader = await command.ExecuteReaderAsync();

			List<ulong> steamIDs = new();
			while (reader.Read())
			{
				steamIDs.Add(reader.GetUInt64(0));
			}
			Connection.Close();

			return steamIDs;
		}

		public async Task AddPlayersFromEntries(List<SteamUserStats.LeaderboardEntriesCallback.LeaderboardEntry> newEntries, string source)
		{
			if (newEntries.Count == 0)
			{
				return;
			}

			// Get players from the DB and determine which SteamIDs will need to be added
			var playersToAdd = new List<ulong>();
			var existingUsers = await GetPlayers(newEntries.Select(x => (ulong)x.SteamID).ToList());
			foreach (var entry in newEntries)
			{
				if (!existingUsers.ContainsKey(entry.SteamID))
				{
					playersToAdd.Add(entry.SteamID);
				}
			}

			// Skip the rest of this if there are no new players
			if (playersToAdd.Count == 0)
			{
				return;
			}

			// Add new players to the database
			// Build the string that will become the query
			var Connection = new MySqlConnection(Settings.ConnectionString);
			Connection.Open();
			var sqlSB = new StringBuilder("INSERT INTO Players(SteamID, FirstSeenTimeUTC) VALUES");
			foreach (var newPlayer in playersToAdd)
			{
				if (Settings.Verbose)
				{
					Utils.WriteLine(source, $"Adding {newPlayer} to the players table");
				}
				sqlSB.Append($"({newPlayer},{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}),");
			}
			sqlSB.Remove(sqlSB.Length - 1, 1); // Remove last comma
			sqlSB.Append(" ON DUPLICATE KEY UPDATE ID = ID");

			// Create the sql query
			var command = new MySqlCommand(sqlSB.ToString(), Connection);

			await command.ExecuteNonQueryAsync();
			Connection.Close();
			Utils.WriteLine(source, $"Added {playersToAdd.Count} new players to the database");
		}

		public async Task FillMissingPlayerInfo(string source)
		{
			var steamIDsToUpdate = await GetSteamIDsForUpdating();
			if(steamIDsToUpdate.Count == 0)
			{
				return;
			}

			// Update each batch to minimize potential API call waste
			var updateBatches = steamIDsToUpdate.Chunk(100);
			foreach (var batch in updateBatches)
			{
				// Fill the cache
				await SteamAPIDAL.GetPlayerSummaries(batch.ToList(), source);

				// Get the players from the cache
				var players = new List<PlayerSummary>();
				foreach (var steamID in batch)
				{
					Caches.PlayerCache.TryGetValue(steamID, out var player);
					player ??= PlayerSummary.UnknownPlayer;
					player.SteamID = player.SteamID == 0 ? steamID : player.SteamID;

					players.Add(player);
				}

				// Update this batch
				await UpdatePlayers(players, source);
			}

			Utils.WriteLine(source, $"Updated {steamIDsToUpdate.Count} players");
		}
	}
}
