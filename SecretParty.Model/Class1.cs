using System.Text.Json.Serialization;
using Azure;
using Azure.Data.Tables;

namespace SecretParty.Model
{
	public class Photo
	{
		public string Url { get; set; }
		public string ThumbUrl { get; set; }
	}
	public class Participant : ITableEntity
	{
		public string PartitionKey { get; set; }

		public string RowKey { get; set; } = Guid.NewGuid().ToString();

		public DateTimeOffset? Timestamp { get; set; }

		public ETag ETag { get; set; }

		public string? Photo { get; set; }

		public string? PhotoThumb { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; init; }

		[JsonPropertyName("gender")]
		public string Gender { get; init; }

		[JsonPropertyName("age")]
		public string Age { get; init; }

		[JsonPropertyName("photoPrompt")]
		public string Description { get; init; }

		[JsonPropertyName("chattingStyle")]
		public string ChattingStyle { get; init; }
	};

	public class Party : ITableEntity
	{
		public string PartitionKey { get; set; } = string.Empty;

		public string RowKey { get; set; } = Guid.NewGuid().ToString();

		public DateTimeOffset? Timestamp { get; set; }

		public ETag ETag { get; set; }

		[JsonPropertyName("participants")]
		public IList<Participant>? Participants { get; set; } = new List<Participant>();

		[property: JsonPropertyName("participantsJson")]
		public string? ParticipantsJson { get; set; }

		[JsonPropertyName("partyName")]
		public string PartyName { get; init; }

		[JsonPropertyName("musicStyle")]
		public string MusicStyle { get; init; }

		[JsonPropertyName("location")]
		public string Location { get; init; }

		[JsonPropertyName("dressCode")]
		public string DressCode { get; init; }

		[JsonPropertyName("type")]
		public string Type { get; init; }

		[JsonPropertyName("primaryColor")]
		public string PrimaryColor { get; init; }

		[JsonPropertyName("description")]
		public string Description { get; init; }

		[JsonPropertyName("flyerPrompt")]
		public string FlyerPrompt { get; init; }

		public string? Photo { get; set; }
		public string? PhotoThumb { get; set; }
		public int ParticipantCount { get; set; }
		public List<string> SomePhotos { get; set; } = new();
	};
}