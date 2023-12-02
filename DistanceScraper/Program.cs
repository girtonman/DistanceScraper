using System;
using System.Threading;

namespace DistanceScraper
{
	public class Program
	{
		public static LeaderboardScraper scraper = new LeaderboardScraper(233610);

		public static void Main(string[] args)
		{
			try
			{
				Utils.Init();
				scraper.Init();
				new Thread(new ThreadStart(MainThread)).Start();
				Console.ReadLine();
			}
			catch (Exception e)
			{
				Utils.WriteLine($"uh oh: {e.Message}");
			}
		}

		public static async void MainThread()
		{
			await TableSeeder.SeedOfficialSprintLeaderboardsAsync();

			while (true)
			{
				try
				{
					while (true)
					{
						await scraper.ScrapeLeaderboards();
						await scraper.ScrapeOfficialLeaderboardEntries();
						await scraper.ScrapeUnofficialLeaderboardEntries();
					}
				}
				catch (Exception e)
				{
					Utils.WriteLine($"uh oh: {e.Message}");
					Utils.WriteLine($"Scraper sleeping for 5 minutes before trying again.");
					Thread.Sleep(TimeSpan.FromMinutes(5));
				}
			}
		}
	}
}
