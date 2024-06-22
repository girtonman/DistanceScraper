using System.Collections.Generic;
using Newtonsoft.Json;

namespace DistanceScraper
{
	public class PublishedFileDetail
	{
		public uint PublishedFileID { get; set; }
		public int Result { get; set; }
		public long Creator { get; set; }
		[JsonProperty("creator_app_id")]
		public int CreatorAppID { get; set; }
		[JsonProperty("consumer_app_id")]
		public int ConsumerAppID { get; set; }
		public string Filename { get; set; }
		[JsonProperty("file_size")]
		public long FileSize { get; set; }
		[JsonProperty("file_url")]
		public string FileURL { get; set; }
		public long hcontent_file { get; set; }
		public long hcontent_preview { get; set; }
		[JsonProperty("preview_url")]
		public string PreviewURL { get; set; }
		public string Title { get; set; }
		public string Description { get; set; }
		[JsonProperty("time_created")]
		public long TimeCreated { get; set; }
		[JsonProperty("time_updated")]
		public long TimeUpdated { get; set; }
		public bool Visibility { get; set; }
		public bool Banned { get; set; }
		[JsonProperty("ban_reason")]
		public string BanReason { get; set; }
		public int Subscriptions { get; set; }
		public int Favorited { get; set; }
		[JsonProperty("lifetime-subscriptions")]
		public int LifetimeSubscriptions { get; set; }
		[JsonProperty("lifetime_favorited")]
		public int LifetimeFavorited { get; set; }
		public int Views { get; set; }
		[JsonConverter(typeof(TagConverter))]
		public List<string> Tags { get; set; }

		// Helper method for creating leaderboard names for steam api lookups
		public string GetLeaderboardName(LevelType levelType)
		{
			return $"{Filename.Replace(".bytes", "")}_{levelType:D}_{Creator}_stable";
		}
	}
}
