using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DistanceScraper.DALs
{
	public class LeaderboardDAL
	{
		public LeaderboardDAL()	{ }

		public async Task<List<Leaderboard>> GetAllLeaderboards()
		{
			var leaderboards = new List<Leaderboard>();
			var Connection = new MySqlConnection(Settings.ConnectionString);
			Connection.Open();

			var sql = "SELECT ID, LevelName, LeaderboardName, IsOfficial, SteamLeaderboardID FROM Leaderboards";
			var command = new MySqlCommand(sql, Connection);
			var reader = await command.ExecuteReaderAsync();

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

			Connection.Close();
			return leaderboards;
		}

		public async Task<List<Leaderboard>> GetOfficialLeaderboards()
		{
			var Connection = new MySqlConnection(Settings.ConnectionString);
			Connection.Open();

			var sql = "SELECT ID, LevelName, LeaderboardName, IsOfficial, SteamLeaderboardID FROM Leaderboards WHERE IsOfficial = true ORDER BY ID ASC";
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
			var Connection = new MySqlConnection(Settings.ConnectionString);
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

		public async Task AddOfficialLeaderboards(List<Leaderboard> levels)
		{
			// Short circuit for empty arrays to avoid unnecessary connections
			if (levels.Count == 0)
			{
				return;
			}

			var Connection = new MySqlConnection(Settings.ConnectionString);
			Connection.Open();

			// If speed ever becomes an issue, this can be batched instead of executing once per level. Leaving it unbatched for now for easier log based troubleshooting
			foreach (var level in levels)
			{
				var sql = @$"INSERT INTO Leaderboards (LevelName, ImageURL, LeaderboardName, IsOfficial, LevelType, LevelSet) VALUES
						(@levelName, @imageURL, @leaderboardName, @isOfficial, @levelType, @levelSet) ON DUPLICATE KEY UPDATE ID=ID";
				var command = new MySqlCommand(sql, Connection);
				command.Parameters.AddWithValue("@levelName", level.LevelName);
				command.Parameters.AddWithValue("@imageURL", $"/images/{level.LevelName}.bytes.png");
				command.Parameters.AddWithValue("@leaderboardName", $"{level.LevelName}_{level.LevelType:D}1_stable");
				command.Parameters.AddWithValue("@isOfficial", level.IsOfficial);
				command.Parameters.AddWithValue("@levelType", level.LevelType);
				command.Parameters.AddWithValue("@levelSet", level.LevelSet);

				Utils.WriteLine("DB Seed", $"Adding {level.LevelName} to the leaderboards table");
				await command.ExecuteNonQueryAsync();
			}

			Connection.Close();
		}

		public async Task AddWorkshopLeaderboard(PublishedFileDetail fileDetail, string levelSet = null)
		{
			// Create a list of level types, since each level type will be a different leaderboard
			var levelTypes = new List<LevelType>();
			if (fileDetail.Tags.Contains("Sprint"))
			{
				levelTypes.Add(LevelType.Sprint);
			}
			if (fileDetail.Tags.Contains("Challenge"))
			{
				levelTypes.Add(LevelType.Challenge);
			}
			if (fileDetail.Tags.Contains("Stunt"))
			{
				levelTypes.Add(LevelType.Stunt);
			}

			// Skip levels that don't have a level type we are tracking
			if (levelTypes.Count == 0)
			{
				Utils.WriteLine("Workshop", "Skipping workshop level due to it not having a supported level type");
				return;
			}

			var Connection = new MySqlConnection(Settings.ConnectionString);
			Connection.Open();

			foreach (var levelType in levelTypes)
			{
				var sql = @$"INSERT INTO Leaderboards (LevelName, ImageURL, LeaderboardName, IsOfficial, WorkshopFileID, Tags, LevelType, LevelSet) VALUES
						(@levelName, @imageURL, @leaderboardName, 0, @workshopFileID, @tags, @levelType, @levelSet) ON DUPLICATE KEY UPDATE ID=ID";

				var command = new MySqlCommand(sql, Connection);
				command.Parameters.AddWithValue("@levelName", fileDetail.Title);
				command.Parameters.AddWithValue("@imageURL", fileDetail.PreviewURL);
				command.Parameters.AddWithValue("@leaderboardName", fileDetail.GetLeaderboardName(levelType));
				command.Parameters.AddWithValue("@workshopFileID", fileDetail.PublishedFileID);
				command.Parameters.AddWithValue("@tags", string.Join(",", fileDetail.Tags));
				command.Parameters.AddWithValue("@levelType", levelType);
				command.Parameters.AddWithValue("@levelSet", levelSet);
				await command.ExecuteNonQueryAsync();
			}
			Connection.Close();
		}

		public async Task UpdateSteamLeaderboardID(uint leaderboardID, uint steamLeaderboardID)
		{
			var Connection = new MySqlConnection(Settings.ConnectionString);
			Connection.Open();
			var sql = $"UPDATE Leaderboards SET SteamLeaderboardID = {steamLeaderboardID} WHERE ID = {leaderboardID}";
			var command = new MySqlCommand(sql, Connection);
			await command.ExecuteNonQueryAsync();
			Connection.Close();
		}

		public async Task<uint> GetMostRecentFileID()
		{
			var Connection = new MySqlConnection(Settings.ConnectionString);
			Connection.Open();

			var sql = "SELECT WorkshopFileID FROM Leaderboards WHERE WorkshopFileID IS NOT NULL ORDER BY ID DESC LIMIT 1";
			var command = new MySqlCommand(sql, Connection);
			var reader = await command.ExecuteReaderAsync();
			uint fileID = 0;
			while (reader.Read())
			{
				fileID = reader.GetUInt32(0);
			}
			reader.Close();
			Connection.Close();

			return fileID;
		}

		public async Task<List<string>> GetExistingLeaderboardNames()
		{
			var Connection = new MySqlConnection(Settings.ConnectionString);
			Connection.Open();

			var sql = "SELECT LeaderboardName FROM Leaderboards";
			var command = new MySqlCommand(sql, Connection);
			var reader = await command.ExecuteReaderAsync();

			var leaderboardNames = new List<string>();
			while (reader.Read())
			{
				leaderboardNames.Add(reader.GetString(0));
			}
			reader.Close();
			Connection.Close();

			return leaderboardNames;
		}
	}
}
