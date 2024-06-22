namespace DistanceScraper
{
	public class Leaderboard
	{
		public uint ID { get; set; }
		public string LevelName { get; set; }
		public string LeaderboardName { get; set; }
		public bool IsOfficial { get; set; }
		public uint? SteamLeaderboardID { get; set; }
		public LevelType LevelType { get; set; }
		public string LevelSet { get; set; }

		public Leaderboard() {}

		public Leaderboard(string levelName, LevelType levelType, string levelSet = null, bool isOfficial = false)
		{
			LevelName = levelName;
			LevelType = levelType;
			LevelSet = levelSet;
			IsOfficial = isOfficial;
		}
	}
}
