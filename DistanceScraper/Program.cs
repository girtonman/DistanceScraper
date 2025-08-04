using System;
using System.Threading;
using DistanceScraper.DALs;

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
				SteamAPIDAL.Init();
				SteamSDKDAL.Init();

				// Seed the official leaderboards from constants in this program
				new Thread(() => TableSeeder.SeedOfficialLeaderboards()).Start();

				// Start a worker to collect information on new levels
				new Thread(() => WorkshopScraperThread()).Start();

				// Start a worker to collect information about players
				new Thread(() => PlayerSummaryThread()).Start();

				// Create several workers so that scanning several thousand leaderboards for their entries doesn't take forever
				new Thread(() => OfficialLeaderboardThread()).Start();
				for (var i = 0; i < Settings.Workers; i++)
				{
					var workerNumber = i+0;
					new Thread(() => UnofficialLeaderboardThread(workerNumber)).Start();
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

		public static async void PlayerSummaryThread()
		{
			var source = "Players";
			var players = new PlayerScraper(source);
			while (true)
			{
				try
				{
					if (Settings.Verbose)
					{
						Utils.WriteLine(source, "Backfilling the Players table with player information");
					}
					await players.ScrapePlayerSummaries();

				}
				catch(Exception e)
				{
					Utils.WriteLine(source, $"uh oh: {e.Message}");
					Utils.WriteLine(source, $"Player scraper sleeping for 2 minutes before trying again.");
					Thread.Sleep(TimeSpan.FromSeconds(120));
				}
				Thread.Sleep(TimeSpan.FromSeconds(10));
			}
		}

		public static async void OfficialLeaderboardThread()
		{
			var source = $"Officials";
			while (true)
			{
				try
				{
					if (Settings.Verbose)
					{
						Utils.WriteLine(source, "Scraping Official Leaderboards");
					}
					await scraper.ScrapeOfficialLeaderboardEntries(source);
				}
				catch (Exception e)
				{
					Utils.WriteLine(source, $"uh oh: {e.Message}");
					Utils.WriteLine(source, $"Worker sleeping for 60 seconds before trying again.");
					Thread.Sleep(TimeSpan.FromSeconds(60));
				}
			}
		}
		
		public static async void UnofficialLeaderboardThread(int workerNumber)
		{
			var source = $"Unofficials #{workerNumber+1}";
			while (true)
			{
				try
				{
					if (Settings.Verbose)
					{
						Utils.WriteLine(source, "Scraping Unofficial Leaderboards");
					}
					await scraper.ScrapeUnofficialLeaderboardEntries(workerNumber, source);
				}
				catch (Exception e)
				{
					Utils.WriteLine(source, $"uh oh: {e.Message}");
					Utils.WriteLine(source, $"Worker sleeping for 60 seconds before trying again.");
					Thread.Sleep(TimeSpan.FromSeconds(60));
				}
			}
		}
	}
}
