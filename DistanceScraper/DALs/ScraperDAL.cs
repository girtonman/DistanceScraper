using MySqlConnector;
using System;
using System.Text;
using System.Threading.Tasks;

namespace DistanceScraper.DALs
{
	public class ScraperDAL
	{
		public ScraperDAL() { }

		public async Task Start(string name, string statusMessage = null)
		{
			statusMessage ??= "Started";
			if (Settings.Verbose)
			{
				Utils.WriteLine(name, statusMessage);
			}

			var connection = new MySqlConnection(Settings.ConnectionString);
			var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			connection.Open();

			var sql = new StringBuilder(@$"
				INSERT INTO Scrapers (Name, StatusMessage, APISuccessCount, APIFailureCount, LastStartUTC, LastHeartbeatUTC) VALUES
				(@name, @statusMessage, 0, 0, {now}, {now})
				ON DUPLICATE KEY UPDATE
				LastAPISuccessCount = APISuccessCount,
				LastAPIFailureCount = APIFailureCount,
				APISuccessCount = 0,
				APIFailureCount = 0,
				LastStartUTC = {now},
				LastHeartbeatUTC = {now},
				StatusMessage = @statusMessage;
			");

			var command = new MySqlCommand(sql.ToString(), connection);
			command.Parameters.AddWithValue("@name", name);
			command.Parameters.AddWithValue("@statusMessage", statusMessage);
			await command.ExecuteNonQueryAsync();

			connection.Close();
		}

		public async Task Finish(string name, string statusMessage = null)
		{
			statusMessage ??= "Finished";
			if (Settings.Verbose)
			{
				Utils.WriteLine(name, statusMessage);
			}
			
			var connection = new MySqlConnection(Settings.ConnectionString);
			var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			connection.Open();

			var sql = new StringBuilder(@$"
				INSERT INTO Scrapers (Name, StatusMessage, LastFinishUTC, LastHeartbeatUTC) VALUES
				(@name, @statusMessage, {now}, {now})
				ON DUPLICATE KEY UPDATE
				LastFinishUTC = {now},
				LastHeartbeatUTC = {now}
			");

			if (statusMessage != null) {
				sql.Append(",\n StatusMessage = @statusMessage");
			}
			sql.Append(';');

			var command = new MySqlCommand(sql.ToString(), connection);
			command.Parameters.AddWithValue("@name", name);
			command.Parameters.AddWithValue("@statusMessage", statusMessage);
			await command.ExecuteNonQueryAsync();

			connection.Close();
		}

		public async Task Pulse(string name, string statusMessage = null)
		{
			statusMessage ??= "Pulsed";
			if (Settings.Verbose)
			{
				Utils.WriteLine(name, statusMessage);
			}

			var connection = new MySqlConnection(Settings.ConnectionString);
			var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			connection.Open();

			var sql = new StringBuilder(@$"
				INSERT INTO Scrapers (Name, StatusMessage, LastHeartbeatUTC) VALUES
				(@name, @statusMessage, {now})
				ON DUPLICATE KEY UPDATE
				LastHeartbeatUTC = {now}
			");

			if (statusMessage != null) {
				sql.Append(",\n StatusMessage = @statusMessage");
			}
			sql.Append(';');

			var command = new MySqlCommand(sql.ToString(), connection);
			command.Parameters.AddWithValue("@name", name);
			command.Parameters.AddWithValue("@statusMessage", statusMessage);
			await command.ExecuteNonQueryAsync();

			connection.Close();
		}

		public async Task CountAPISuccess(string name)
		{
			var connection = new MySqlConnection(Settings.ConnectionString);
			var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			connection.Open();

			var sql = new StringBuilder(@$"
				UPDATE Scrapers SET
				APISuccessCount = APISuccessCount + 1,
				LastHeartbeatUTC = {now}
				WHERE Name = @name;
			");

			var command = new MySqlCommand(sql.ToString(), connection);
			command.Parameters.AddWithValue("@name", name);
			await command.ExecuteNonQueryAsync();

			connection.Close();
		}

		public async Task CountAPIFailure(string name, string statusMessage)
		{
			statusMessage ??= "Steam API call failed";
			Utils.WriteLine(name, statusMessage);

			var connection = new MySqlConnection(Settings.ConnectionString);
			var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			connection.Open();

			var sql = new StringBuilder(@$"
				UPDATE Scrapers SET
				APIFailureCount = APIFailureCount + 1,
				LastHeartbeatUTC = {now}
			");

			if (statusMessage != null) {
				sql.Append(",\n StatusMessage = @statusMessage");
			}
			sql.Append("\n WHERE Name = @name;");

			var command = new MySqlCommand(sql.ToString(), connection);
			command.Parameters.AddWithValue("@name", name);
			command.Parameters.AddWithValue("@statusMessage", statusMessage);
			await command.ExecuteNonQueryAsync();

			connection.Close();
		}
	}
}
