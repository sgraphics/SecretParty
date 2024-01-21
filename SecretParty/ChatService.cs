using Azure.Core;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using SecretParty.Model;

namespace SecretParty
{
	public class ChatService
	{
		private readonly IConfiguration _configuration;
		private readonly IHttpContextAccessor _httpContextAccessor;

		public ChatService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
		{
			_configuration = configuration;
			_httpContextAccessor = httpContextAccessor;
		}

		[HttpGet]
		[Route("startChat")]
		public async Task<ChatData> StartChat(string partyId)
		{
			var serviceClient = new TableServiceClient(_configuration["AzureWebJobsStorage"]);
			var partiesTable = serviceClient.GetTableClient("Party");
			await partiesTable.CreateIfNotExistsAsync();
			var participantsTable = serviceClient.GetTableClient("Participant");
			await participantsTable.CreateIfNotExistsAsync();
			var chatTable = serviceClient.GetTableClient("Chat");
			await chatTable.CreateIfNotExistsAsync();
			var party = await partiesTable.QueryAsync<Party>(x => x.RowKey == partyId).FirstOrDefaultAsync();
			var user = _httpContextAccessor.HttpContext?.User.Identity?.Name;

			var participants =
				await participantsTable.QueryAsync<Participant>(x => x.PartitionKey == party.RowKey).ToListAsync();
			var participant = participants.OrderByDescending(x => x.Timestamp).First(x => x.User == user);
			//var chats = chatTable.QueryAsync<ChatData>(x => x.PartitionKey == partyId);

			ChatData chat;
			if (!string.IsNullOrWhiteSpace(participant.ActiveChatId))
			{
				//get active
				chat = chatTable.GetEntity<ChatData>(partyId, participant.ActiveChatId)?.Value;
			}
			else
			{
				var participant2 = participants
					.Where(x => string.IsNullOrWhiteSpace(x.ActiveChatId) && x.Gender != participant.Gender)
					.MinBy(x => Guid.NewGuid());
				chat = new ChatData
				{
					Participant1Id = participant.RowKey,
					Participant1Name = participant.Name,
					Participant1Age = participant.Age,
					Participant2Id = participant2?.RowKey,
					Participant2Age = participant2?.Age,
					Participant2Name = participant2?.Name,
					PartitionKey = partyId
				};
				if (participant2 != null)
				{
					await participantsTable.UpsertEntityAsync(new TableEntity(participant.PartitionKey, participant2.RowKey)
					{
						{ "ActiveChatId", chat.RowKey }
					});
					await chatTable.UpsertEntityAsync(chat);
					await participantsTable.UpsertEntityAsync(new TableEntity(participant.PartitionKey, participant.RowKey)
					{
						{ "ActiveChatId", chat.RowKey }
					});
				}
			}

			chat.Party = party;

			return chat;
		}
	}
}
