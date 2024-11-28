using DistanceScraper.DALs;
using System.Threading.Tasks;

namespace DistanceScraper
{
	public class PlayerScraper
	{
		private PlayerDAL PlayerDAL { get; set; }
		private string Source { get; set; }

		public PlayerScraper(string source)
		{
			PlayerDAL = new PlayerDAL();
			Source = source;
		}

		public async Task ScrapePlayerSummaries()
		{
			await PlayerDAL.FillMissingPlayerInfo(Source);
		}
	}
}
