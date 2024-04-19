using System.Diagnostics;
using System.Reactive.Subjects;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecretParty.Client;
using SecretParty.Model;

namespace SecretParty
{
	public class ChatService
	{
		private readonly IConfiguration _configuration;
		private readonly AiProxy _aiProxy;
		private readonly ILogger<ChatService> _logger;
		private readonly AuthenticationStateProvider _authenticationStateProvider;

		public ChatService(IConfiguration configuration, AiProxy aiProxy, ILogger<ChatService> logger, AuthenticationStateProvider authenticationStateProvider)
		{
			_configuration = configuration;
			_aiProxy = aiProxy;
			_logger = logger;
			_authenticationStateProvider = authenticationStateProvider;
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
			var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
			var user = state.User?.Identity?.Name;
			//var user = _httpContextAccessor.HttpContext?.User.Identity?.Name;

			//_logger.LogInformation("user is " + user);

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
				var previousChats = await chatTable.QueryAsync<ChatData>()
					.Where(x => x.PartitionKey == partyId && (x.Participant1Id == participant.RowKey || x.Participant2Id == participant.RowKey))
					.ToListAsync();

				var previousPartners = previousChats.SelectMany(x => new[] { x.Participant1Id, x.Participant2Id })
					.ToHashSet();

				var participant2 = participants
					.Where(x => string.IsNullOrWhiteSpace(x.ActiveChatId) && x.Gender != participant.Gender && x.User != user)
					.Where(x => !previousPartners.Contains(x.RowKey))
					.MinBy(x => Guid.NewGuid());

				chat = new ChatData
				{
					Participant1Id = participant.RowKey,
					Participant1Name = participant.Name,
					Participant1Age = participant.Age,
					Participant1PhotoThumb = participant.PhotoThumb,
					Participant2Id = participant2?.RowKey,
					Participant2Age = participant2?.Age,
					Participant2Name = participant2?.Name,
					Participant2PhotoThumb = participant2?.PhotoThumb,
					PartitionKey = partyId,
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
			chat.Participant = participant;

			return chat;
		}

		public async Task Say(string newChat, ChatData chat)
		{
			var serviceClient = new TableServiceClient(_configuration["AzureWebJobsStorage"]);
			var participantsTable = serviceClient.GetTableClient("Participant");
			var chatsTable = serviceClient.GetTableClient("Chat");
			await participantsTable.CreateIfNotExistsAsync();
			await chatsTable.CreateIfNotExistsAsync();
			var participants = await participantsTable.QueryAsync<Participant>(x => x.ActiveChatId == chat.RowKey).ToListAsync();

			var participant = chat.Participant;
			var otherParticipant = participants.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.User) || x.User != chat.Participant.User);

			var chatMessageData = new ChatMessageData { Message = newChat, Timestamp = DateTimeOffset.UtcNow, ParticipantId = participant.RowKey };
			chat.History.Add(chatMessageData);

			await chatsTable.UpsertEntityAsync(chat);

			ChatUpdates.OnNext((chat.RowKey, chatMessageData));

			if (string.IsNullOrWhiteSpace(otherParticipant!.User))
			{
				//is BOT
				Task.Run(async () =>
				{
					Stopwatch stopper = new Stopwatch();
					stopper.Start();

					var aiMessageRecords = new List<AiMessageRecord>
					{
						new(AiMessageRole.System, @$"Conversation is between the user ({participant.Name}, {participant.Gender}, {participant.Age}) and a person called {otherParticipant.Name}. {otherParticipant.Name} is a {otherParticipant.Age} year old {otherParticipant.Gender}, who looks like this: {otherParticipant.Description}. {otherParticipant.Name} chatting style is as follows: {otherParticipant.ChattingStyle}. All generated answers must follow this writing style, and overall feel like a real person is writing. IMPORTANT: {otherParticipant.Name} message should be VERY short, brief, MAXIMUM 2 sentences, but can be even shorter like only 3 words. {otherParticipant.Name} aim is to answer questions but also ask questions about the user to learn more and perhaps hook up. Conversation can be flirty. Conversation takes place at a party: {chat.Party}, dress code: {chat.Party.DressCode}, music style {chat.Party.MusicStyle}.")
					};

					AddChatHistory(chat, aiMessageRecords);

					aiMessageRecords.Add(new(AiMessageRole.User, @$"{newChat}"));

					var writeOut = await _aiProxy.ThinkStream(aiMessageRecords);

					var aiChatMessage = new ChatMessageData { Message = writeOut, Timestamp = DateTimeOffset.UtcNow, ParticipantId = otherParticipant.RowKey };

					chat.History.Add(aiChatMessage);

					await chatsTable.UpsertEntityAsync(chat);

					stopper.Stop();

					var seconds = stopper.ElapsedMilliseconds / 1000;

					var shouldTakeSeconds = writeOut.Length / 3;

					if (shouldTakeSeconds > seconds)
					{
						await Task.Delay(TimeSpan.FromSeconds(shouldTakeSeconds - seconds));
					}

					ChatUpdates.OnNext((chat.RowKey, aiChatMessage));
				});
			}
		}

		/// <summary>
		/// chat id
		/// </summary>
		public static readonly Subject<(string, ChatMessageData)>
			ChatUpdates = new Subject<(string, ChatMessageData)>();

		void AddChatHistory(ChatData chat, List<AiMessageRecord> aiMessageRecords)
		{
			foreach (var message in chat.History.OrderBy(x => x.Timestamp))
			{
				aiMessageRecords.Add(new AiMessageRecord(message.ParticipantId == chat.Participant.RowKey ? AiMessageRole.User : AiMessageRole.Assistant, message.Message));
			}
		}

		public async Task<bool> EndChat(ChatData chat, bool isBot)
		{
			var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
			var userId = state.User?.Identity?.Name;

			var serviceClient = new TableServiceClient(_configuration["AzureWebJobsStorage"]);
			var participantsTable = serviceClient.GetTableClient("Participant");
			var chatsTable = serviceClient.GetTableClient("Chat");
			var userTable = serviceClient.GetTableClient("Users");
			var user = (await userTable.GetEntityAsync<TableEntity>(string.Empty, userId)).Value;
			var currentScore = user.GetInt32("Score") ?? 0;

			await participantsTable.CreateIfNotExistsAsync();
			await chatsTable.CreateIfNotExistsAsync();
			var participants = await participantsTable.QueryAsync<Participant>(x => x.ActiveChatId == chat.RowKey).ToListAsync();

			var participant = chat.Participant;
			var otherParticipant = participants.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.User) || x.User != chat.Participant.User);
			bool isScore = false;
			if (string.IsNullOrWhiteSpace(otherParticipant.User))
			{
				//is bot
				if (isBot)
				{
					await userTable.UpsertEntityAsync(new TableEntity(string.Empty, userId)
					{
						{ "Score", ++currentScore },
					});
					isScore = true;
				}
			}
			else
			{
				if (!isBot)
				{
					await userTable.UpsertEntityAsync(new TableEntity(string.Empty, userId)
					{
						{ "Score", ++currentScore },
					});
					isScore = true;
				}
			}
			await ResetActiveChat(participantsTable, participant, otherParticipant);

			ChatUpdates.OnNext((chat.RowKey, new ChatMessageData
			{
				ParticipantId = chat.Participant.RowKey,
				Timestamp = DateTimeOffset.UtcNow,
				IsBot = isBot
			}));

			return isScore;
		}

		private static async Task ResetActiveChat(TableClient participantsTable, Participant participant,
			Participant otherParticipant)
		{
			await participantsTable.UpsertEntityAsync(new TableEntity(participant.PartitionKey, participant.RowKey)
			{
				{ "ActiveChatId", "" },
			});

			await participantsTable.UpsertEntityAsync(new TableEntity(otherParticipant.PartitionKey, otherParticipant.RowKey)
			{
				{ "ActiveChatId", "" },
			});
		}
	}
}
