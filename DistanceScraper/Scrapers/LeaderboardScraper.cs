using DistanceScraper.DALs;
using SteamKit2;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DistanceScraper
{
	public class LeaderboardScraper
	{
		private LeaderboardDAL LeaderboardDAL { get; set; }
		private LeaderboardEntryDAL LeaderboardEntryDAL { get; set; }
		private PlayerDAL PlayerDAL { get; set; }
		private LeaderboardEntryHistoryDAL LeaderboardEntryHistoryDAL { get; set; }
		private ScraperDAL ScraperDAL { get; set; }

		public LeaderboardScraper(uint AppID)
		{
			LeaderboardDAL = new LeaderboardDAL();
			LeaderboardEntryDAL = new LeaderboardEntryDAL();
			LeaderboardEntryHistoryDAL = new LeaderboardEntryHistoryDAL();
			PlayerDAL = new PlayerDAL();
			ScraperDAL = new ScraperDAL();
		}

		public async Task ScrapeWorkshopInfo()
		{
			var mostRecentFileID = await LeaderboardDAL.GetMostRecentFileID();
			var newLevels = await SteamAPIDAL.GetWorkshopLevelList(mostRecentFileID);

			// Add them from oldest to newest in order to make the list retrieval more efficient in subsequent scraper loops
			newLevels.Reverse();

			foreach (var level in newLevels)
			{
				if(level.PublishedFileID == 0)
				{
					Utils.WriteLine("Workshop", "Parse failure detected, skipping.");
					continue;
				}

				Utils.WriteLine("Workshop", $"Adding Level:{level.Title}");
				Utils.WriteLine("Workshop", $"  ID: {level.PublishedFileID}");
				Utils.WriteLine("Workshop", $"  PreviewURL: {level.PreviewURL}");
				Utils.WriteLine("Workshop", $"  Filename: {level.Filename}");
				Utils.WriteLine("Workshop", $"  LeaderboardName: {level.GetLeaderboardName(LevelType.None)}");
				Utils.WriteLine("Workshop", $"  Tags: {string.Join(",", level.Tags)}");
				await LeaderboardDAL.AddWorkshopLeaderboard(level);
			}

			return;
		}

		// Scrapes leaderboards to get their leaderboard IDs and store them in the database
		public async Task ScrapeLeaderboards()
		{
			var dal = new LeaderboardDAL();
			var existingLeaderboards = await dal.GetAllLeaderboards();
			var leaderboardsToUpdate = existingLeaderboards.Where(x => !x.SteamLeaderboardID.HasValue);
			foreach (var leaderboardToUpdate in leaderboardsToUpdate)
			{
				SteamUserStats.FindOrCreateLeaderboardCallback leaderboard;
				try {
					leaderboard = await SteamSDKDAL.UserStats.FindLeaderboard(Constants.AppID, leaderboardToUpdate.LeaderboardName);
				} catch (TaskCanceledException e) {
					await ScraperDAL.CountAPIFailure("Workshop", $"Request to steam timed out (10s). Failed to retrieve leaderboard info for #{leaderboardToUpdate.ID}: {leaderboardToUpdate.LeaderboardName}");
					continue;
				}

				if (leaderboard.Result == EResult.OK && leaderboard.ID != 0)
				{
					await ScraperDAL.Pulse("Workshop", $"Saving steam leaderboard ID for {leaderboardToUpdate.LevelName} ({leaderboardToUpdate.LeaderboardName})");
					await ScraperDAL.CountAPISuccess("Workshop");
					await dal.UpdateSteamLeaderboardID(leaderboardToUpdate.ID, (uint)leaderboard.ID);
				}
				else
				{
					await ScraperDAL.CountAPIFailure("Workshop", $"Failed to get the steam leaderboard ID for {leaderboardToUpdate.LevelName} ({leaderboardToUpdate.LeaderboardName})");
				}
			}
		}

		// Scrapes the leaderboard entries for the official maps
		public async Task ScrapeOfficialLeaderboardEntries(string source)
		{
			var leaderboards = await LeaderboardDAL.GetOfficialLeaderboards();
			await ScrapeLeaderboardEntries(leaderboards, source);
		}

		public async Task ScrapeUnofficialLeaderboardEntries(int workerNumber, string source)
		{
			var leaderboards = await LeaderboardDAL.GetUnofficialLeaderboards();
			var workerLeaderboards = leaderboards.Where(x => x.ID % Settings.Workers == workerNumber).ToList();
			await ScrapeLeaderboardEntries(workerLeaderboards, source);
		}

		// Scrapes leaderboard entries and updates the relevant rows in the database
		public async Task ScrapeLeaderboardEntries(List<Leaderboard> leaderboards, string source)
		{
			foreach (var leaderboard in leaderboards)
			{
				await ScraperDAL.Pulse(source, $"Processing #{leaderboard.ID}: {leaderboard.LevelName}");

				if (leaderboard.SteamLeaderboardID == null)
				{
					continue;
				}

				// Retrieve leaderboards from steam and from the DB
				SteamUserStats.LeaderboardEntriesCallback job;
				try {
					job = await SteamSDKDAL.UserStats.GetLeaderboardEntries(Constants.AppID, (int)leaderboard.SteamLeaderboardID, 0, 99999, ELeaderboardDataRequest.Global);
				} catch (TaskCanceledException e) {
					await ScraperDAL.CountAPIFailure(source, $"Request to steam timed out (10s). Failed to retrieve entries for #{leaderboard.ID}: {leaderboard.LevelName}");
					continue;
				}

				// Error checking and API result counting
				if (job.Result != EResult.OK)
				{
					await ScraperDAL.CountAPIFailure(source, $"Failed to retrieve entries for #{leaderboard.ID}: {leaderboard.LevelName}");
					continue;
				}
				await ScraperDAL.CountAPISuccess(source);

				var entries = job.Entries;
				var existingEntries = await LeaderboardEntryDAL.GetLeaderboardEntries(leaderboard.ID);

				// Determine inserts and updates
				var newEntries = entries.Where(x => !existingEntries.ContainsKey(x.SteamID.ConvertToUInt64())).ToList();
				var entryKeys = existingEntries.ToDictionary(x => x.Value.LeaderboardID + "-" + x.Value.Milliseconds + "-" + x.Value.SteamID, x => x.Value);
				var entriesToUpdate = entries.Where(x => existingEntries.ContainsKey(x.SteamID.ConvertToUInt64()) && !entryKeys.ContainsKey(leaderboard.ID + "-" + x.Score + "-" + x.SteamID.ConvertToUInt64())).ToList();

				// Update the database based on the scraped data
				await PlayerDAL.AddPlayersFromEntries(newEntries, source);
				await LeaderboardEntryDAL.AddLeaderboardEntries(leaderboard, newEntries, source);
				await LeaderboardEntryHistoryDAL.AddLeaderboardEntryHistory(leaderboard, existingEntries, entriesToUpdate, source);
				await LeaderboardEntryDAL.UpdateLeaderboardEntries(leaderboard, existingEntries, entriesToUpdate, source);
			}
		}
	}
}
