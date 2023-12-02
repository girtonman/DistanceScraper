using MySqlConnector;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DistanceScraper.DALs
{
	public class LeaderboardDAL
	{
		private MySqlConnection Connection { get; set; }

		public LeaderboardDAL()
		{
			Connection = new MySqlConnection(Settings.ConnectionString);
		}

		public async Task<List<Leaderboard>> GetAllLeaderboards()
		{
			var leaderboards = new List<Leaderboard>();
			Connection.Open();

			var sql = "SELECT ID, LevelName, LeaderboardName, IsOfficial, SteamLeaderboardID FROM Leaderboards";
			var command = new MySqlCommand(sql, Connection);
			var reader = await command.ExecuteReaderAsync();

			while (reader.Read())
			{
				leaderboards.Add(new Leaderboard() {
					ID = reader.GetUInt32(0),
					LevelName = reader.GetString(1),
					LeaderboardName = reader.GetString(2),
					IsOfficial = reader.GetBoolean(3),
					SteamLeaderboardID = reader.IsDBNull(4) ? new uint?() : reader.GetUInt32(4),
				});
			}

			Connection.Close();
			return leaderboards;
		}

		public async Task<List<Leaderboard>> GetOfficialSprintLeaderboards()
		{
			Connection.Open();

			var sql = "SELECT ID, LevelName, LeaderboardName, IsOfficial, SteamLeaderboardID FROM Leaderboards WHERE IsOfficial = true";
			var command = new MySqlCommand(sql, Connection);
			var reader = await command.ExecuteReaderAsync();

			var leaderboards = new List<Leaderboard>();
			while (reader.Read())
			{
				leaderboards.Add(new Leaderboard()
				{
					ID = reader.GetUInt32(0),
					LevelName = reader.GetString(1),
					LeaderboardName = reader.GetString(2),
					IsOfficial = reader.GetBoolean(3),
					SteamLeaderboardID = reader.IsDBNull(4) ? new uint?() : reader.GetUInt32(4),
				});
			}
			reader.Close();
			Connection.Close();

			return leaderboards;
		}

		public async Task<List<Leaderboard>> GetUnofficialSprintLeaderboards()
		{
			Connection.Open();

			var sql = "SELECT ID, LevelName, LeaderboardName, IsOfficial, SteamLeaderboardID FROM Leaderboards WHERE IsOfficial = false";
			var command = new MySqlCommand(sql, Connection);
			var reader = await command.ExecuteReaderAsync();

			var leaderboards = new List<Leaderboard>();
			while (reader.Read())
			{
				leaderboards.Add(new Leaderboard()
				{
					ID = reader.GetUInt32(0),
					LevelName = reader.GetString(1),
					LeaderboardName = reader.GetString(2),
					IsOfficial = reader.GetBoolean(3),
					SteamLeaderboardID = reader.IsDBNull(4) ? new uint?() : reader.GetUInt32(4),
				});
			}
			reader.Close();
			Connection.Close();

			return leaderboards;
		}

		public async Task AddLeaderboards(List<string> levelNames)
		{
			if (levelNames.Count == 0)
			{
				return;
			}

			Connection.Open();

			var sqlSB = new StringBuilder("INSERT INTO Leaderboards (LevelName, LeaderboardName, IsOfficial) VALUES");
			//TODO: Add support for non-official leaderboard names
			levelNames.ForEach(x => {
				sqlSB.Append($"('{x}','{x}_1_stable',true),");
				Utils.WriteLine($"Adding {x} to the leaderboards table");
			});
			sqlSB.Remove(sqlSB.Length - 1, 1); // Remove last comma
			sqlSB.Append("ON DUPLICATE KEY UPDATE ID = ID");
			var sql = sqlSB.ToString();

			var command = new MySqlCommand(sql, Connection);
			await command.ExecuteNonQueryAsync();

			Connection.Close();
		}

		public async Task UpdateSteamLeaderboardID(uint leaderboardID, uint steamLeaderboardID)
		{
			Connection.Open();
			var sql = $"UPDATE Leaderboards SET SteamLeaderboardID = {steamLeaderboardID} WHERE ID = {leaderboardID}";
			var command = new MySqlCommand(sql, Connection);
			await command.ExecuteNonQueryAsync();
			Connection.Close();
		}
	}
}
