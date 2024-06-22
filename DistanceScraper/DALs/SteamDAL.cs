using DistanceScraper;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace DistanceTracker.DALs
{
	public class SteamDAL
	{
		public SteamDAL()
		{
			SteamAPIKey = Settings.SteamAPIKey;
		}

		private string SteamAPIKey { get; set; }

		public async Task<PublishedFileDetail> GetWorkshopInfo(uint fileID)
		{
			var client = new HttpClient
			{
				BaseAddress = new Uri("http://api.steampowered.com")
			};
			var content = new FormUrlEncodedContent(new[]
			{
				new KeyValuePair<string, string>("key", SteamAPIKey),
				new KeyValuePair<string, string>("itemcount", "1"),
				new KeyValuePair<string, string>("publishedfileids[0]", $"{fileID}"),
			});
			var response = await client.PostAsync("ISteamRemoteStorage/GetPublishedFileDetails/v1/", content);

			var responseMessage = await response.Content.ReadAsStringAsync();

			var json = JObject.Parse(responseMessage);
			var workshopDetails = json["response"]["publishedfiledetails"].ToObject<List<PublishedFileDetail>>()[0];

			return workshopDetails;
		}

		public async Task<List<PublishedFileDetail>> GetWorkshopLevelList(uint mostRecentFileID = 0)
		{
			// Setup client and reusable part of the uri
			var client = new HttpClient
			{
				BaseAddress = new Uri("https://api.steampowered.com")
			};
			var content = new Dictionary<string, string>
			{
				{"key", SteamAPIKey},
				{"query_type", "1"},
				{"numperpage", "100"},
				{"appid", "233610"},
				{"return_details", "true"},
			};
			var endpoint = "/IPublishedFileService/QueryFiles/v1/?";
			var uri = endpoint + string.Join("&", content.Select(x => x.Key + "=" + x.Value));

			// Loop through results until the end of the list or until we see the most recent fileID that we already know about
			var keepScanning = true;
			var cursor = "*"; // First cursor should be "*", then subsequently the value of "next_cursor" from the response
			var levelList = new List<PublishedFileDetail>();
			while (keepScanning)
			{
				// Query the API for workshop levels
				var response = await client.GetAsync(uri + $"&cursor={HttpUtility.UrlEncode(cursor)}");
				var responseMessage = await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
				{
					throw new Exception($"Aborting workshop level retrieval: {responseMessage}");
				}

				// Attempt to parse the response into a json object
				JObject json = null;
				json = JObject.Parse(responseMessage);

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
				foreach(var jToken in json["response"]["publishedfiledetails"])
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
	}
}
