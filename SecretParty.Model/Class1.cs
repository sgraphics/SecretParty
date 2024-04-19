using System.Runtime.Serialization;
using System.Text.Json;
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

	public class User : ITableEntity
	{
		public string PartitionKey { get; set; }
		public string RowKey { get; set; }
		public DateTimeOffset? Timestamp { get; set; }
		public ETag ETag { get; set; }
		
		public string Gender { get; set; }
	}

	public class Participant : ITableEntity
	{
		public string PartitionKey { get; set; }

		public string RowKey { get; set; } = Guid.NewGuid().ToString();

		public DateTimeOffset? Timestamp { get; set; }

		public ETag ETag { get; set; }

		public string? Photo { get; set; }

		public string? PhotoThumb { get; set; }

		public string? HairStyle { get; set; }

		public string? Ethnicity { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; }

		[JsonPropertyName("gender")]
		public string Gender { get; set; }

		[JsonPropertyName("age")]
		public string Age { get; set; }

		[JsonPropertyName("photoPrompt")]
		public string? Description { get; set; }

		[JsonPropertyName("chattingStyle")]
		public string? ChattingStyle { get; set; }

		public string? User { get; set; }
		public string? ActiveChatId { get; set; }
		public int Score { get; set; }
	};

	public class Party : ITableEntity
	{
		public string PartitionKey { get; set; } = string.Empty;

		public string RowKey { get; set; } = Guid.NewGuid().ToString();

		public DateTimeOffset? Timestamp { get; set; }

		public ETag ETag { get; set; }

		[JsonPropertyName("participants")]
		[IgnoreDataMember]
		public IList<Participant>? Participants { get; set; } = new List<Participant>();
		
		[JsonIgnore]
		public string ParticipantsJson
		{
			get => JsonSerializer.Serialize(Participants);
			set => Participants = JsonSerializer.Deserialize<IList<Participant>?>(value);
		}

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
		[IgnoreDataMember]
		public int ParticipantCount { get; set; }
		[IgnoreDataMember]
		public List<string> SomePhotos { get; set; } = new();
	};

	public class ChatData : ITableEntity
	{
		public string PartitionKey { get; set; } = string.Empty;

		public string RowKey { get; set; } = Guid.NewGuid().ToString();

		public DateTimeOffset? Timestamp { get; set; }

		public ETag ETag { get; set; }

		public string Participant1Id { get; set; }

		public string GetOtherPronoun()
		{
			return Participant?.Gender == "M" ? "she" : "he";
		}
		public string Participant1Name { get; set; }
		public string Participant1Age { get; set; }
		public string? Participant2Id { get; set; }
		public string? Participant2Name { get; set; }
		public string? Participant2Age { get; set; }
		[IgnoreDataMember]
		public Party Party { get; set; }
		[IgnoreDataMember]
		public Participant Participant { get; set; }

		[IgnoreDataMember]
		[JsonIgnore]
		public Participant? OtherParticipant => string.IsNullOrWhiteSpace(Participant2Id)
			? null
			: new()
			{
				Name = Participant1Id == Participant.RowKey ? Participant2Name : Participant1Name,
				RowKey = Participant1Id == Participant.RowKey ? Participant2Id : Participant1Id,
				Age = Participant1Id == Participant.RowKey ? Participant2Age : Participant1Age,
				PhotoThumb = Participant1Id == Participant.RowKey ? Participant2PhotoThumb : Participant1PhotoThumb,
				Photo = Participant1Id == Participant.RowKey ? Participant2Photo : Participant1Photo,
			};

		[IgnoreDataMember]
		public List<ChatMessageData> History { get; set; } = new();

		[JsonIgnore]
		public string HistoryJson
		{
			get => JsonSerializer.Serialize(History);
			set => History = JsonSerializer.Deserialize<List<ChatMessageData>>(value);
		}

		public string? Participant1PhotoThumb { get; set; }
		public string? Participant2PhotoThumb { get; set; }
		public string? Participant1Photo { get; set; }
		public string? Participant2Photo { get; set; }
	}

	public class ChatMessageData
	{
		public string ParticipantId { get; set; }
		public DateTimeOffset Timestamp { get; set; }
		public string? Message { get; set; }
		public bool? IsBot { get; set; }
	}
}