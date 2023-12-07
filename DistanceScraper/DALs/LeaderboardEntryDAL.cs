using MySqlConnector;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DistanceScraper.DALs
{
	public class LeaderboardEntryDAL
	{
		private MySqlConnection Connection { get; set; }

		public LeaderboardEntryDAL()
		{
			Connection = new MySqlConnection(Settings.ConnectionString);
		}

		public async Task<Dictionary<ulong, LeaderboardEntry>> GetLeaderboardEntries(uint leaderboardID)
		{
			Connection.Open();
			var sql = $"SELECT ID, LeaderboardID, Milliseconds, SteamID, FirstSeenTimeUTC FROM LeaderboardEntries WHERE LeaderBoardID = {leaderboardID}";
			var command = new MySqlCommand(sql, Connection);
			var reader = await command.ExecuteReaderAsync();

			var leaderboardEntries = new Dictionary<ulong, LeaderboardEntry>();
			while (reader.Read())
			{
				var steamID = reader.GetUInt64(3);
				leaderboardEntries.Add(steamID, new LeaderboardEntry()
				{
					ID = reader.GetUInt32(0),
					LeaderboardID = reader.GetUInt32(1),
					Milliseconds = reader.GetUInt64(2),
					SteamID = steamID,
					FirstSeenTimeUTC = reader.GetUInt64(4),
				});
			}
			reader.Close();
			Connection.Close();

			return leaderboardEntries;
		}

		public async Task AddLeaderboardEntries(Leaderboard leaderboard, List<SteamUserStats.LeaderboardEntriesCallback.LeaderboardEntry> newEntries, Handlers handlers, BaseScraper scraper)
		{
			if (newEntries.Count == 0)
			{
				return;
			}

			// Fill the user cache if necessary
			var steamIDs = new List<SteamID>();
			foreach (var newEntry in newEntries)
			{
				var name = handlers.Friends.GetFriendPersonaName(newEntry.SteamID);
				if (string.IsNullOrEmpty(name))
				{
					steamIDs.Add(newEntry.SteamID);
				}
			}
			await scraper.RequestUserInfo(steamIDs);

			foreach (var newEntry in newEntries)
			{
				var name = handlers.Friends.GetFriendPersonaName(newEntry.SteamID);
				Utils.WriteLine($"New time: {name} set their first time on {leaderboard.LevelName}: {newEntry.Score / 1000.0:0.000}s with a rank of {newEntry.GlobalRank}!");
			}

			var sqlSB = new StringBuilder("INSERT INTO LeaderboardEntries (LeaderboardID, Milliseconds, SteamID, FirstSeenTimeUTC, UpdatedTimeUTC) VALUES");
			newEntries.ForEach(x => sqlSB.Append($"({leaderboard.ID},{x.Score},{x.SteamID.ConvertToUInt64()},{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}),"));
			sqlSB.Remove(sqlSB.Length - 1, 1); // Remove last comma
			sqlSB.Append("ON DUPLICATE KEY UPDATE ID = ID");
			var sql = sqlSB.ToString();

			Connection.Open();
			var command = new MySqlCommand(sql, Connection);
			await command.ExecuteNonQueryAsync();
			Connection.Close();
			Utils.WriteLine($"({leaderboard.ID}){leaderboard.LevelName}: Added {newEntries.Count} new leaderboard entries to the database");
		}

		public async Task UpdateLeaderboardEntries(Leaderboard leaderboard, Dictionary<ulong, LeaderboardEntry> existingEntries, List<SteamUserStats.LeaderboardEntriesCallback.LeaderboardEntry> updatedEntries)
		{
			if (existingEntries.Count == 0 || updatedEntries.Count == 0)
			{
				return;
			}

			foreach (var updatedEntry in updatedEntries)
			{
				Connection.Open();
				var existingEntry = existingEntries[updatedEntry.SteamID.ConvertToUInt64()];
				var sql = $"UPDATE LeaderboardEntries SET Milliseconds = {updatedEntry.Score}, UpdatedTimeUTC = {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()} WHERE ID = {existingEntry.ID}";
				var command = new MySqlCommand(sql, Connection);
				await command.ExecuteNonQueryAsync();
				Connection.Close();
			}

			Utils.WriteLine($"({leaderboard.ID}){leaderboard.LevelName}: Updated {updatedEntries.Count} leaderboard entry records");
		}
	}
}
