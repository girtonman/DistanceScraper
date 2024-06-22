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

				// Seed the official leaderboards from constants in this program
				new Thread(() => TableSeeder.SeedOfficialLeaderboards()).Start();

				// Start a worker to asynchronously grab information on new levels
				new Thread(() => WorkshopScraperThread()).Start();

				// Create several workers so that scanning several thousand leaderboards for their entries doesn't take forever
				for (var i = 0; i < Settings.Workers; i++)
				{
					var workerNumber = i+0;
					new Thread(() => WorkerThread(workerNumber)).Start();
				}

				// Don't exit the app
				Console.ReadLine();
			}
			catch (Exception e)
			{
				Utils.WriteLine($"big uh oh: {e.Message}", "");
			}
		}

		public static async void WorkshopScraperThread()
		{
			while (true)
			{
				try
				{
					if (Settings.Verbose)
					{
						Utils.WriteLine("Workshop", "Scraping Steam Workshop for new levels");
					}
					await scraper.ScrapeWorkshopInfo();

					if (Settings.Verbose)
					{
						Utils.WriteLine("Workshop", "Scraping Leaderboards");
					}
					await scraper.ScrapeLeaderboards();
				}
				catch(Exception e)
				{
					Utils.WriteLine("Workshop", $"uh oh: {e.Message}");
					Utils.WriteLine("Workshop", $"Workshop scraper sleeping for 5 minutes before trying again.");
				}
				Thread.Sleep(TimeSpan.FromMinutes(5));
			}
		}

		public static async void WorkerThread(int workerNumber)
		{
			while (true)
			{
				try
				{
					if (Settings.Verbose)
					{
						Utils.WriteLine($"Worker #{workerNumber+1}", "Scraping Official Leaderboards");
					}
					await scraper.ScrapeOfficialLeaderboardEntries(workerNumber);

					if (Settings.Verbose)
					{
						Utils.WriteLine($"Worker #{workerNumber+1}", "Scraping Unofficial Leaderboards");
					}
					await scraper.ScrapeUnofficialLeaderboardEntries(workerNumber);
				}
				catch (Exception e)
				{
					Utils.WriteLine($"Worker #{workerNumber+1}", $"uh oh: {e.Message}");
					Utils.WriteLine($"Worker #{workerNumber+1}", $"Worker sleeping for 5 minutes before trying again.");
					Thread.Sleep(TimeSpan.FromMinutes(5));
				}
			}
		}
	}
}
