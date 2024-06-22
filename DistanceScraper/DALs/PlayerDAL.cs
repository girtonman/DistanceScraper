using DistanceTracker.DALs;
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
					Name = reader.GetString(2),
				};
			}
			reader.Close();
			Connection.Close();

			return players;
		}

		public async Task AddPlayersFromEntries(List<SteamUserStats.LeaderboardEntriesCallback.LeaderboardEntry> newEntries, Handlers handlers, BaseScraper scraper, int workerNumber)
		{
			if (newEntries.Count == 0)
			{
				return;
			}

			// Get players from the DB and determine which SteamIDs will need to be added
			var existingLookupBatches = newEntries.Chunk(100);
			var playersToAdd = new List<ulong>();
			foreach (var batch in existingLookupBatches)
			{
				var existingUsers = await GetPlayers(batch.Select(x => (ulong)x.SteamID).ToList());
				foreach (var entry in batch)
				{
					if (!existingUsers.ContainsKey(entry.SteamID))
					{
						playersToAdd.Add(entry.SteamID);
					}
				}
			}

			// Skip the rest of this if there are no new players
			if (playersToAdd.Count == 0)
			{
				return;
			}

			// Get steam names and cache them
			var newPlayerBatches = playersToAdd.Chunk(100);
			var steamDAL = new SteamDAL();
			foreach (var batch in newPlayerBatches)
			{
				await steamDAL.GetPlayerSummaries(batch.ToList(), workerNumber);
			}

			// Add new players to the database
			// Build the string that will become the query
			var Connection = new MySqlConnection(Settings.ConnectionString);
			Connection.Open();
			var sqlSB = new StringBuilder("INSERT INTO Players(SteamID, Name, FirstSeenTimeUTC) VALUES");
			var i = 0;
			foreach (var newPlayer in playersToAdd)
			{
				sqlSB.Append($"({newPlayer},@userName{i},{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}),");
				i++;
			}
			sqlSB.Remove(sqlSB.Length - 1, 1); // Remove last comma
			sqlSB.Append(" ON DUPLICATE KEY UPDATE ID = ID");

			// Create the sql query
			var command = new MySqlCommand(sqlSB.ToString(), Connection);

			// Add parameter values for player names
			i = 0;
			foreach (var newPlayer in playersToAdd)
			{
				Caches.PlayerCache.TryGetValue(newPlayer, out var player);
				player ??= PlayerSummary.UnknownPlayer;
				if (Settings.Verbose)
				{
					Utils.WriteLine($"Worker #{workerNumber + 1}", $"Adding {newPlayer}: {player.Name} to the players table");
				}
				command.Parameters.AddWithValue($"@userName{i}", player.Name);
				i++;
			}

			await command.ExecuteNonQueryAsync();
			Connection.Close();
			Utils.WriteLine($"Worker #{workerNumber + 1}", $"Added {playersToAdd.Count} new players to the database");
		}
	}
}
