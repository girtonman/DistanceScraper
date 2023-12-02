using DistanceScraper.DALs;
using System.Linq;
using System.Threading.Tasks;

namespace DistanceScraper
{
	public class TableSeeder
	{
		public TableSeeder() {}

		public static async Task SeedOfficialSprintLeaderboardsAsync()
		{
			Utils.WriteLine($"Checking for changes in official sprint leaderboards list");

			var dal = new LeaderboardDAL();
			var existingLeaderboards = await dal.GetAllLeaderboards();
			var newLeaderboards = OfficialLeaderboards.SprintLevelNames
				.Where(x => !existingLeaderboards.Any(y => y.LevelName == x))
				.ToList();

			Utils.WriteLine($"{newLeaderboards.Count} new leaderboards found");
			await dal.AddLeaderboards(newLeaderboards);
		}
	}
}
