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
			var status = new ScraperDAL();
			try
			{
				new Thread(async () => await status.Start("Scraper")).Start();
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
				new Thread(async () => await status.Finish("Scraper", "Scraper died unexpectedly")).Start();
				Utils.WriteLine($"big uh oh: {e.Message}", "");
			}
		}

		public static async void WorkshopScraperThread()
		{
			var source = "Workshop";
			var status = new ScraperDAL();
			while (true)
			{
				await status.Start(source);
				try
				{
					await status.Pulse(source, "Scanning Steam Workshop for new levels");
					await scraper.ScrapeWorkshopInfo();

					await status.Pulse(source, "Retrieving basic level information");
					await scraper.ScrapeLeaderboards();

					await status.Finish(source, "Workshop scanning finished. Sleeping for 5 minutes");
				}
				catch(Exception e)
				{
					Utils.WriteLine(source, $"uh oh: {e.Message}");
					await status.Pulse(source, "Unexpected error occurred. Sleeping for 5 minutes before trying again");
				}
				Thread.Sleep(TimeSpan.FromMinutes(5));
			}
		}

		public static async void PlayerSummaryThread()
		{
			var source = "Players";
			var status = new ScraperDAL();
			var players = new PlayerScraper(source);
			while (true)
			{
				await status.Start(source);
				try
				{
					await status.Pulse(source, "Populating the Players table with additional player information");
					await players.ScrapePlayerSummaries();

					await status.Finish(source, "Player scanning finished. Sleeping for 10 seconds");
				}
				catch(Exception e)
				{
					Utils.WriteLine(source, $"uh oh: {e.Message}");
					await status.Pulse(source, "Player scraper sleeping for 2 minutes before trying again");
					Thread.Sleep(TimeSpan.FromMinutes(2));
				}
				Thread.Sleep(TimeSpan.FromSeconds(10));
			}
		}

		public static async void OfficialLeaderboardThread()
		{
			var source = "Officials";
			var status = new ScraperDAL();
			while (true)
			{
				await status.Start(source);
				try
				{
					await status.Pulse(source, "Retrieving leaderboard entries for official leaderboards");
					await scraper.ScrapeOfficialLeaderboardEntries(source);

					await status.Finish(source);
				}
				catch (Exception e)
				{
					Utils.WriteLine(source, $"uh oh: {e.Message}");
					await status.Pulse(source, "Worker sleeping for 60 seconds before trying again");
					Thread.Sleep(TimeSpan.FromSeconds(60));
				}
			}
		}
		
		public static async void UnofficialLeaderboardThread(int workerNumber)
		{
			var source = $"Unofficials #{workerNumber+1}";
			var status = new ScraperDAL();
			while (true)
			{
				await status.Start(source);
				try
				{
					await status.Pulse(source, "Retrieving leaderboard entries for unofficial leaderboards");
					await scraper.ScrapeUnofficialLeaderboardEntries(workerNumber, source);

					await status.Finish(source);
				}
				catch (Exception e)
				{
					Utils.WriteLine(source, $"uh oh: {e.Message}");
					await status.Pulse(source, "Worker sleeping for 60 seconds before trying again.");
					Thread.Sleep(TimeSpan.FromSeconds(60));
				}
			}
		}
	}
}
