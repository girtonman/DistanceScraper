using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DistanceScraper
{
	// Silly little custom json converter because valve thought this was a sane structure for the tags array:
	// Example:
	// "tags":[
	// 	{"tag":"level"},
	// 	{"tag":"Sprint"},
	// 	{"tag":"Nightmare"}
	// ]
	public class TagConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return true;
		}

		public override List<string> ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			// Load JObject from stream
			var json = ((JTokenReader)reader).CurrentToken;

			// Put the tags into a string list instead
			var tags = new List<string>();
			foreach (var element in json)
			{
				tags.Add(element["tag"].ToString());
			}

			reader.Read();
			return tags;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}
	}
}
