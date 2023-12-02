using MySqlConnector;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DistanceScraper.DALs
{
	public class PlayerDAL
	{
		private MySqlConnection Connection { get; set; }

		public PlayerDAL()
		{
			Connection = new MySqlConnection(Settings.ConnectionString);
		}

		public async Task<Player> GetPlayer(ulong steamID)
		{
			Connection.Open();
			var sql = $"SELECT ID, SteamID, Name FROM Players WHERE SteamID = {steamID}";
			var command = new MySqlCommand(sql, Connection);
			var reader = await command.ExecuteReaderAsync();

			Player player = null;
			while (reader.Read())
			{
				player = new Player()
				{
					ID = reader.GetUInt32(0),
					SteamID = reader.GetUInt64(1),
					Name = reader.GetString(2),
				};
			}
			reader.Close();
			Connection.Close();

			return player;
		}
		
		public async Task AddPlayersFromEntries(List<SteamUserStats.LeaderboardEntriesCallback.LeaderboardEntry> newEntries, Handlers handlers, BaseScraper scraper)
		{
			foreach (var entry in newEntries)
			{
				var existingUser = await GetPlayer(entry.SteamID.ConvertToUInt64());
				if (existingUser == null)
				{
					await scraper.RequestUserInfo(entry.SteamID);
					var name = handlers.Friends.GetFriendPersonaName(entry.SteamID);
					Connection.Open();

					var sql = "INSERT INTO Players(SteamID, Name, FirstSeenTimeUTC) VALUES";
					sql += $"({entry.SteamID.ConvertToUInt64()},@userName,{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()})";
					Utils.WriteLine($"Adding {entry.SteamID.ConvertToUInt64()}: {name} to the players table");

					var command = new MySqlCommand(sql, Connection);
					command.Parameters.AddWithValue("@userName", name);
					await command.ExecuteNonQueryAsync();

					Connection.Close();
				}
			}
		}
	}
}
