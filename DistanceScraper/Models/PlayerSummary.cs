using Newtonsoft.Json;

namespace DistanceScraper
{
	public class PlayerSummary
	{
		public ulong SteamID { get; set; }
		public int CommunityVisibilityState { get; set; }
		[JsonProperty("personaname")]
		public string Name { get; set; }
		public string ProfileURI { get; set; }
		public string Avatar { get; set; }
		public string AvatarMedium { get; set; }
		public string AvatarFull { get; set; }
		public string AvatarHash { get; set; }
		public int PersonaState { get; set; }
		public ulong PrimaryClanID { get; set; }
		public long TimeCreated { get; set; }
		public int PersonaStateFlags { get; set; }

		public static PlayerSummary UnknownPlayer { get; } = new PlayerSummary() {Name = "Unknown/Deleted Steam User"};
	}
}
