﻿namespace DistanceScraper
{
	public class Leaderboard
	{
		public uint ID { get; set; }
		public string LevelName { get; set; }
		public string LeaderboardName { get; set; }
		public bool IsOfficial { get; set; }
		public uint? SteamLeaderboardID { get; set; }
	}
}
