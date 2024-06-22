using System.Collections.Generic;

namespace DistanceScraper
{
	public static class Caches
	{
		public static Dictionary<ulong, PlayerSummary> PlayerCache { get; } = new();
	}
}