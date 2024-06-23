using DistanceScraper;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Threading;

namespace DistanceTracker.DALs
{
	public static class SteamAPIDAL
	{
		private static string SteamAPIKey { get; set; }
		private static Uri BaseAPIAddress { get; set; }
		private static Task ResetBucketTask;
		private static int BucketCount = 0;

		public static void Init()
		{
			SteamAPIKey = Settings.SteamAPIKey;
			BaseAPIAddress = new Uri("https://api.steampowered.com");
			ResetBucketTask = ResetBucket();
			ResetBucketTask.Start();
		}

		private static async Task ResetBucket()
		{
			await Task.Delay(Settings.SteamAPIBucketingWindowSeconds * 1000);
			Utils.WriteLine("SteamAPI", $"Resetting rate limiting bucket. API calls this window: {BucketCount}");
			Volatile.Write(ref BucketCount, 0);
			ResetBucketTask = ResetBucket();
			ResetBucketTask.Start();
		}

		private HttpClient CreateClient()
		{
			return new HttpClient
			{
				BaseAddress = BaseAPIAddress
			};
		}

		private static async Task RateLimit()
		{
			if (Volatile.Read(ref BucketCount) > Settings.SteamAPIWindowLimit)
			{
				await ResetBucketTask;
			}
			Interlocked.Increment(ref BucketCount);
		}

		private async Task<JObject> RateLimitedPost(HttpClient client, string requestURI, List<KeyValuePair<string, string>> parameters = null)
		{
			await RateLimit();

			FormUrlEncodedContent content = null;
			if (parameters != null)
			{
				parameters.Append(new KeyValuePair<string, string>("key", SteamAPIKey));
				content = new FormUrlEncodedContent(parameters);
			}

			var response = await client.PostAsync(requestURI, content);
			var responseMessage = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode)
			{
				throw new Exception($"Steam API call failed ({response.StatusCode}): {responseMessage}");
			}

			return JObject.Parse(responseMessage);
		}

		private async Task<JObject> RateLimitedGet(HttpClient client, string endpoint, Dictionary<string, object> parameters = null)
		{
			await RateLimit();

			string parametersURI = "";
			if (parameters != null)
			{
				parameters.Append(new KeyValuePair<string, object>("key", SteamAPIKey));
				parametersURI = "?" + string.Join("&", parameters.Select(x => $"{x.Key}={HttpUtility.UrlEncode(x.Value.ToString())}"));
			}

			var response = await client.GetAsync(endpoint + parametersURI);
			var responseMessage = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode)
			{
				throw new Exception($"Steam API call failed ({response.StatusCode}): {responseMessage}");
			}

			return JObject.Parse(responseMessage);
		}

		public async Task<PublishedFileDetail> GetWorkshopInfo(uint fileID)
		{
			var client = CreateClient();
			var parameters = new List<KeyValuePair<string, string>>()
			{
				new("itemcount", "1"),
				new("publishedfileids[0]", $"{fileID}"),
			};

			var json = await RateLimitedPost(client, "ISteamRemoteStorage/GetPublishedFileDetails/v1/", parameters);

			return json["response"]["publishedfiledetails"].ToObject<List<PublishedFileDetail>>()[0];
		}

		public async Task<List<PublishedFileDetail>> GetWorkshopLevelList(uint mostRecentFileID = 0)
		{
			// Setup client and reusable part of the uri
			var client = CreateClient();
			var parameters = new Dictionary<string, object>
			{
				{"query_type", 1},
				{"numperpage", 100},
				{"appid", 233610},
				{"return_details", true}
			};
			var endpoint = "/IPublishedFileService/QueryFiles/v1/";

			// Loop through results until the end of the list or until we see the most recent fileID that we already know about
			var keepScanning = true;
			// First cursor should be "*", then subsequently the value of "next_cursor" from the response
			var cursor = "*";
			var levelList = new List<PublishedFileDetail>();
			while (keepScanning)
			{
				// Update the cursor
				parameters["cursor"] = cursor;
				
				// Query the API for workshop levels
				var json = await RateLimitedGet(client, endpoint, parameters);

				// Get the next cursor
				var nextCursor = json["response"]["next_cursor"].ToString();

				// If the cursor is the same, we are finished
				if (cursor == nextCursor)
				{
					break;
				}
				cursor = nextCursor;

				// Collect level information for this batch
				// Breaking this up because ToObject<List<PublishedFileDetail>> was misbehaving
				var batch = new List<PublishedFileDetail>();
				foreach (var jToken in json["response"]["publishedfiledetails"])
				{
					var level = jToken.ToObject<PublishedFileDetail>();

					if (mostRecentFileID != 0 && mostRecentFileID == level.PublishedFileID)
					{
						keepScanning = false;
						break;
					}
					levelList.Add(level);
				}
			}

			if (levelList.Count > 0)
			{
				Utils.WriteLine("Workshop", $"Found {levelList.Count} new workshop levels");
			}
			return levelList;
		}

		public async Task<List<PlayerSummary>> GetPlayerSummaries(List<ulong> steamIDs, int workerNumber)
		{
			// Setup client and the reusable part of the uri
			var client = CreateClient();
			var parameters = new Dictionary<string, object>
			{
				{"steamids", string.Join(",", steamIDs)},
			};
			var endpoint = "/ISteamUser/GetPlayerSummaries/v2/?";

			// Query the API for player summaries
			var json = await RateLimitedGet(client, endpoint, parameters);

			// Collect player summaries
			// Breaking this up because ToObject<List<PlayerSummary>> was misbehaving
			var players = new List<PlayerSummary>();
			foreach (var jToken in json["response"]["players"])
			{
				var player = jToken.ToObject<PlayerSummary>();
				Caches.PlayerCache[player.SteamID] = player;
				players.Add(player);
			}

			Utils.WriteLine($"Worker #{workerNumber + 1}", $"Found {players.Count} player summaries");
			return players;
		}
	}
}
