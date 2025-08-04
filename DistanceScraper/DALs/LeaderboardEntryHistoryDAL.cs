using MySqlConnector;
using SteamKit2;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DistanceScraper.DALs
{
	public class LeaderboardEntryHistoryDAL
	{
		public LeaderboardEntryHistoryDAL() { }

		public async Task AddLeaderboardEntryHistory(Leaderboard leaderboard, Dictionary<ulong, LeaderboardEntry> existingEntries, List<SteamUserStats.LeaderboardEntriesCallback.LeaderboardEntry> updatedEntries, string source)
		{
			if (updatedEntries.Count == 0)
			{
				return;
			}
			
			var Connection = new MySqlConnection(Settings.ConnectionString);
			Connection.Open();
			var historyInsertsSB = new StringBuilder("INSERT INTO LeaderboardEntryHistory (LeaderboardID, SteamID, FirstSeenTimeUTC, OldMilliseconds, NewMilliseconds, OldRank, NewRank, UpdatedTimeUTC) VALUES");
			foreach (var updatedEntry in updatedEntries)
			{
				// Get existing entry and the rank they held
				var existingEntry = existingEntries[updatedEntry.SteamID.ConvertToUInt64()];
				var order = leaderboard.LevelType == LevelType.Stunt ? "DESC" : "ASC";
				//TODO only query this once (mostly useful during initial scraping)
				var sql = $"SELECT `Rank` FROM (SELECT SteamID, RANK() OVER (ORDER BY Milliseconds {order}) `Rank` FROM LeaderboardEntries WHERE LeaderboardID = {existingEntry.LeaderboardID}) rankings WHERE SteamID = {existingEntry.SteamID};";
				var command = new MySqlCommand(sql, Connection);
				var reader = await command.ExecuteReaderAsync();

				reader.Read();
				var rank = reader.GetInt32(0);
				reader.Close();

				var improvement = leaderboard.LevelType == LevelType.Stunt ? ((ulong)updatedEntry.Score - existingEntry.Milliseconds) : (existingEntry.Milliseconds - (ulong)updatedEntry.Score);
				var rankImprovement = rank - updatedEntry.GlobalRank;

				Caches.PlayerCache.TryGetValue(updatedEntry.SteamID, out var player);
				player ??= (await SteamAPIDAL.GetPlayerSummaries(new List<ulong> { updatedEntry.SteamID }, source)).FirstOrDefault();
				player ??= PlayerSummary.UnknownPlayer; // Sometimes players just don't exist on steam anymore. This is more likely to happen when filling in a fresh database

				var improvementString = leaderboard.LevelType == LevelType.Stunt ? $"{improvement} eV" : $"{improvement / 1000.0:0.000}s";
				Utils.WriteLine(source, $"Updated {(leaderboard.LevelType == LevelType.Stunt ? "score" : "time")}: {player.Name} improved on {leaderboard.LevelName}. Improved by {improvementString} and {rankImprovement} ranks ({rank} to {updatedEntry.GlobalRank})!");

				historyInsertsSB.Append($"({existingEntry.LeaderboardID},{existingEntry.SteamID},{existingEntry.UpdatedTimeUTC},{existingEntry.Milliseconds},{updatedEntry.Score},{rank},{updatedEntry.GlobalRank},{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}),");
			}
			historyInsertsSB.Remove(historyInsertsSB.Length - 1, 1); // Remove last comma
			historyInsertsSB.Append("ON DUPLICATE KEY UPDATE ID = ID");

			var historyInsertsSQL = historyInsertsSB.ToString();
			var historyInsertsCommand = new MySqlCommand(historyInsertsSQL, Connection);
			await historyInsertsCommand.ExecuteNonQueryAsync();
			Connection.Close();

			Utils.WriteLine(source, $"({leaderboard.ID}){leaderboard.LevelName}: Saved leaderboard entry history for {updatedEntries.Count} improvements");
		}
	}
}
