using MySqlConnector;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DistanceScraper.DALs
{
	public class LeaderboardEntryHistoryDAL
	{
		private MySqlConnection Connection { get; set; }

		public LeaderboardEntryHistoryDAL()
		{
			Connection = new MySqlConnection(Settings.ConnectionString);
		}

		public async Task AddLeaderboardEntryHistory(Leaderboard leaderboard, Dictionary<ulong, LeaderboardEntry> existingEntries, List<SteamUserStats.LeaderboardEntriesCallback.LeaderboardEntry> updatedEntries, Handlers handlers, BaseScraper scraper)
		{
			if (updatedEntries.Count == 0)
			{
				return;
			}

			Connection.Open();
			var historyInsertsSB = new StringBuilder("INSERT INTO LeaderboardEntryHistory (LeaderboardID, SteamID, FirstSeenTimeUTC, OldMilliseconds, NewMilliseconds, OldRank, NewRank, UpdatedTimeUTC) VALUES");
			foreach (var updatedEntry in updatedEntries)
			{
				// Get existing entry and the rank they held
				var existingEntry = existingEntries[updatedEntry.SteamID.ConvertToUInt64()];
				var sql = $"SELECT `Rank` FROM (SELECT SteamID, RANK() OVER (ORDER BY Milliseconds ASC) `Rank` FROM LeaderboardEntries WHERE LeaderboardID = {existingEntry.LeaderboardID}) rankings WHERE SteamID = {existingEntry.SteamID};";
				var command = new MySqlCommand(sql, Connection);
				var reader = await command.ExecuteReaderAsync();

				reader.Read();
				var rank = reader.GetInt32(0);
				reader.Close();

				var timeImprovement = existingEntry.Milliseconds - (ulong) updatedEntry.Score;
				var rankImprovement = rank - updatedEntry.GlobalRank;
				var name = handlers.Friends.GetFriendPersonaName(updatedEntry.SteamID);
				if (string.IsNullOrEmpty(name))
				{
					await scraper.RequestUserInfo(updatedEntry.SteamID);
					name = handlers.Friends.GetFriendPersonaName(updatedEntry.SteamID);
				}
				Utils.WriteLine($"Updated time: {name} improved on {leaderboard.LevelName}. Improved by {timeImprovement / 1000.0:0.000}s and {rankImprovement} ranks ({rank} to {updatedEntry.GlobalRank})!");

				historyInsertsSB.Append($"({existingEntry.LeaderboardID},{existingEntry.SteamID},{existingEntry.UpdatedTimeUTC},{existingEntry.Milliseconds},{updatedEntry.Score},{rank},{updatedEntry.GlobalRank},{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}),");
			}
			historyInsertsSB.Remove(historyInsertsSB.Length - 1, 1); // Remove last comma
			historyInsertsSB.Append("ON DUPLICATE KEY UPDATE ID = ID");

			var historyInsertsSQL = historyInsertsSB.ToString();
			var historyInsertsCommand = new MySqlCommand(historyInsertsSQL, Connection);
			await historyInsertsCommand.ExecuteNonQueryAsync();
			Connection.Close();

			Utils.WriteLine($"({leaderboard.ID}){leaderboard.LevelName}: Saved leaderboard entry history for {updatedEntries.Count} improvements");
		}
	}
}
