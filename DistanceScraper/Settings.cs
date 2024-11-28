namespace DistanceScraper
{
	public class Settings
	{
		public static string ConnectionString { get; } = "";
		public static string Username { get; } = "";
		public static string Password { get; } = "";
		public static string SteaamAPIKey { get; } = "";
		public static bool Verbose { get; } = true;
		public static int Workers { get; } = 1;
		public static int SteamAPIBucketingWindowSeconds { get; } = 10;
		public static int SteamAPIWindowLimit { get; } = 9;
		public static double SteamAPIRequestsPerSecond { get; } = 1;
	}
}
