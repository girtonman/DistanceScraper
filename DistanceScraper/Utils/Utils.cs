﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DistanceTracker.DALs;
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

		public static void WriteLine(string source, string message)
		{
			var elapsed = Timer.Elapsed;
			Console.WriteLine($"[{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}][{source}]: {message}");
		}

		public static void ClearCurrentConsoleLine()
		{
			Console.SetCursorPosition(0, Console.CursorTop - 1);
			var currentLineCursor = Console.CursorTop;
			Console.SetCursorPosition(0, Console.CursorTop);
			Console.Write(new string(' ', Console.WindowWidth));
			Console.SetCursorPosition(0, currentLineCursor);
		}

		public static async Task LogNewLeaderboardEntry(Leaderboard leaderboard, List<SteamUserStats.LeaderboardEntriesCallback.LeaderboardEntry> newEntries, string source)
		{
			// TODO: Pull from database instead of doing API calls
			// // Look for steamids that need caching
			// var steamIDs = new List<ulong>();
			// foreach (var newEntry in newEntries)
			// {
			// 	Caches.PlayerCache.TryGetValue(newEntry.SteamID, out var player);
			// 	if (player == null)
			// 	{
			// 		steamIDs.Add(newEntry.SteamID);
			// 	}
			// }

			// // Cache in batches
			// var steamIDBatches = steamIDs.Chunk(100);
			// foreach (var steamIDBatch in steamIDBatches)
			// {
			// 	await SteamAPIDAL.GetPlayerSummaries(steamIDBatch.ToList(), source);
			// }

			// Log information about the new time and who set it
			foreach (var newEntry in newEntries)
			{
				Caches.PlayerCache.TryGetValue(newEntry.SteamID, out var player);
				var identity = (player == null || string.IsNullOrEmpty(player.Name)) ? newEntry.SteamID.ToString() : player.Name;
				WriteLine(source, $"New time: {identity} set their first time on {leaderboard.LevelName}: {newEntry.Score / 1000.0:0.000}s with a rank of {newEntry.GlobalRank}!");
			}
		}
	}
}
