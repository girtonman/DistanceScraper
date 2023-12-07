using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SteamKit2;

namespace DistanceScraper
{
	public static class Utils
	{
		public static Stopwatch Timer = new Stopwatch();

		public static void Init()
		{
			Timer.Start();
		}

		public static void WriteLine(string message)
		{
			var elapsed = Timer.Elapsed;
			Console.WriteLine($"[{(int) elapsed.TotalMinutes}:{elapsed.Seconds.ToString("00")}]: {message}");
		}

		public static void ClearCurrentConsoleLine()
		{
			Console.SetCursorPosition(0, Console.CursorTop - 1);
			var currentLineCursor = Console.CursorTop;
			Console.SetCursorPosition(0, Console.CursorTop);
			Console.Write(new string(' ', Console.WindowWidth));
			Console.SetCursorPosition(0, currentLineCursor);
		}

		public static async Task LogNewLeaderboardEntry(Leaderboard leaderboard, List<SteamUserStats.LeaderboardEntriesCallback.LeaderboardEntry> newEntries, Handlers handlers, BaseScraper scraper)
		{
			// Look for steamids that need caching
			var steamIDs = new List<SteamID>();
			foreach (var newEntry in newEntries)
			{
				var name = handlers.Friends.GetFriendPersonaName(newEntry.SteamID);
				if (string.IsNullOrEmpty(name))
				{
					steamIDs.Add(newEntry.SteamID);
				}
			}

			// Cache in batches
			var steamIDBatches = steamIDs.Chunk(100);
			foreach (var steamIDBatch in steamIDBatches)
			{
				await scraper.RequestUserInfo(steamIDBatch.ToList());
			}

			// Log information about the new time and who set it
			foreach (var newEntry in newEntries)
			{
				var name = handlers.Friends.GetFriendPersonaName(newEntry.SteamID);
				WriteLine($"New time: {name} set their first time on {leaderboard.LevelName}: {newEntry.Score / 1000.0:0.000}s with a rank of {newEntry.GlobalRank}!");
			}
		}
	}
}
