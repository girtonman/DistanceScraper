using DistanceScraper.DALs;
using System.Linq;
using System.Threading.Tasks;

namespace DistanceScraper
{
	public class TableSeeder
	{
		public TableSeeder() { }

		public static async void SeedOfficialLeaderboards()
		{
			await SeedSprintLeaderboards();
			await SeedChallengeLeaderboards();
			await SeedStuntLeaderboards();
		}

		private static async Task SeedSprintLeaderboards()
		{
			Utils.WriteLine("DB Seed", $"Checking for changes in official sprint leaderboards list");

			var dal = new LeaderboardDAL();
			var existingLeaderboards = await dal.GetOfficialLeaderboards();
			var newSprintLeaderboards = OfficialLeaderboards.SprintLevels
				.Where(x => !existingLeaderboards.Any(y => y.LevelName == x.LevelName))
				.ToList();

			Utils.WriteLine("DB Seed", $"{newSprintLeaderboards.Count} new official sprint leaderboards found");
			await dal.AddOfficialLeaderboards(newSprintLeaderboards);
		}

		public static async Task SeedChallengeLeaderboards()
		{
			Utils.WriteLine("DB Seed", $"Checking for changes in official challenge leaderboards list");

			var dal = new LeaderboardDAL();
			var existingLeaderboards = await dal.GetOfficialLeaderboards();
			var newSprintLeaderboards = OfficialLeaderboards.ChallengeLevels
				.Where(x => !existingLeaderboards.Any(y => y.LevelName == x.LevelName))
				.ToList();

			Utils.WriteLine("DB Seed", $"{newSprintLeaderboards.Count} new official challenge leaderboards found");
			await dal.AddOfficialLeaderboards(newSprintLeaderboards);
		}

		public static async Task SeedStuntLeaderboards()
		{
			Utils.WriteLine("DB Seed", $"Checking for changes in official stunt leaderboards list");

			var dal = new LeaderboardDAL();
			var existingLeaderboards = await dal.GetOfficialLeaderboards();
			var newStuntLeaderboards = OfficialLeaderboards.StuntLevels
				.Where(x => !existingLeaderboards.Any(y => y.LevelName == x.LevelName))
				.ToList();

			Utils.WriteLine("DB Seed", $"{newStuntLeaderboards.Count} new official stunt leaderboards found");
			await dal.AddOfficialLeaderboards(newStuntLeaderboards);
		}
	}
}
