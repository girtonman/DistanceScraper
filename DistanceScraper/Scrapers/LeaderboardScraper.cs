﻿using DistanceScraper.DALs;
using SteamKit2;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DistanceScraper
{
	public class LeaderboardScraper : BaseScraper
	{
		private LeaderboardDAL LeaderboardDAL { get; set; }
		private LeaderboardEntryDAL LeaderboardEntryDAL { get; set; }
		private PlayerDAL PlayerDAL { get; set; }
		private LeaderboardEntryHistoryDAL LeaderboardEntryHistoryDAL { get; set; }

		public LeaderboardScraper(uint AppID) : base(AppID)
		{
			LeaderboardDAL = new LeaderboardDAL();
			LeaderboardEntryDAL = new LeaderboardEntryDAL();
			LeaderboardEntryHistoryDAL = new LeaderboardEntryHistoryDAL();
			PlayerDAL = new PlayerDAL();
		}

		// Scrapes leaderboards to get their leaderboard IDs and store them in the database
		public async Task ScrapeLeaderboards()
		{
			var dal = new LeaderboardDAL();
			var existingLeaderboards = await dal.GetAllLeaderboards();
			var leaderboardsToUpdate = existingLeaderboards.Where(x => !x.SteamLeaderboardID.HasValue);
			foreach(var leaderboardToUpdate in leaderboardsToUpdate)
			{
				var leaderboard = await Handlers.UserStats.FindLeaderboard(AppID, leaderboardToUpdate.LeaderboardName);
				if(leaderboard.Result == EResult.OK && leaderboard.ID != 0)
				{
					Utils.WriteLine($"Saving steam leaderboard ID for {leaderboardToUpdate.LevelName} ({leaderboardToUpdate.LeaderboardName})");
					await dal.UpdateSteamLeaderboardID(leaderboardToUpdate.ID, (uint) leaderboard.ID);
				}
				else
				{
					Utils.WriteLine($"Failed to get the steam leaderboard ID for {leaderboardToUpdate.LevelName} ({leaderboardToUpdate.LeaderboardName})");
				}
			}
		}

		// Scrapes the leaderboard entries for the official maps
		public async Task ScrapeOfficialLeaderboardEntries()
		{
			var leaderboards = await LeaderboardDAL.GetOfficialSprintLeaderboards();
			await ScrapeLeaderboardEntries(leaderboards);
		}

		public async Task ScrapeUnofficialLeaderboardEntries()
		{
			var leaderboards = await LeaderboardDAL.GetUnofficialSprintLeaderboards();
			await ScrapeLeaderboardEntries(leaderboards);
		}

		// Scrapes leaderboard entries and updates the relevant rows in the database
		public async Task ScrapeLeaderboardEntries(List<Leaderboard> leaderboards)
		{
			foreach (var leaderboard in leaderboards)
			{
				//Utils.WriteLine($"===================Processing {leaderboard.LevelName}===================");

				if (leaderboard.SteamLeaderboardID == null)
				{
					continue;
				}
				// Retrieve leaderboards from steam and from the DB
				var job = await Handlers.UserStats.GetLeaderboardEntries(AppID, (int) leaderboard.SteamLeaderboardID, 0, 99999, ELeaderboardDataRequest.Global);
				if(job.Result != EResult.OK)
				{
					Utils.WriteLine($"Failed to retrieve entries for {leaderboard.LevelName}");
					continue;
				}
				var entries = job.Entries;
				var existingEntries = await LeaderboardEntryDAL.GetLeaderboardEntries(leaderboard.ID);

				// Determine inserts and updates
				var newEntries = entries.Where(x => !existingEntries.ContainsKey(x.SteamID.ConvertToUInt64())).ToList();
				var entryKeys = existingEntries.ToDictionary(x => x.Value.LeaderboardID + "-" + x.Value.Milliseconds + "-" + x.Value.SteamID, x => x.Value);
				var entriesToUpdate = entries.Where(x => existingEntries.ContainsKey(x.SteamID.ConvertToUInt64()) && !entryKeys.ContainsKey(leaderboard.ID + "-" + x.Score + "-" + x.SteamID.ConvertToUInt64())).ToList();

				// Update the database based on the scraped data
				await PlayerDAL.AddPlayersFromEntries(newEntries, Handlers, this);
				await LeaderboardEntryDAL.AddLeaderboardEntries(leaderboard, newEntries, Handlers, this);
				await LeaderboardEntryHistoryDAL.AddLeaderboardEntryHistory(leaderboard, existingEntries, entriesToUpdate, Handlers, this);
				await LeaderboardEntryDAL.UpdateLeaderboardEntries(leaderboard, existingEntries, entriesToUpdate);
			}
		}
	}
}
