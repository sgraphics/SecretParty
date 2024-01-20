using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using SecretParty.Client;
using SecretParty.Model;

namespace SecretParty.Controllers
{

	[Route("api/[controller]")]
	[ApiController]
	public class SecretPartyController(IUserService userService, IConfiguration configuration, AiProxy aiProxy) : ControllerBase
	{
		[HttpGet]
		[Route("authenticateToken")]
		public async Task<ActionResult> AuthenticateToken(string token)
		{
			await userService.FinishLogin(token);
			return Redirect("/chat");
		}

		[HttpGet]
		[Route("signOut")]
		public async Task<ActionResult> SignOut()
		{
			await userService.SignOut();
			return Redirect("/");
		}

		[HttpGet]
		[Route("getTodaysParties")]
		public async Task<ActionResult> GetParties()
		{
			var partition = DateTimeOffset.UtcNow.ToString("yy-MM-dd");
			var serviceClient = new TableServiceClient(configuration["AzureWebJobsStorage"]);
			var partiesTable = serviceClient.GetTableClient("Party");
			await partiesTable.CreateIfNotExistsAsync();
			var parties = await partiesTable.QueryAsync<Party>(x => x.PartitionKey == partition).ToListAsync();
			foreach (var party in parties)
			{
				party.ParticipantsJson = null;
				party.Participants = null;
			}
			return new JsonResult(parties);
		}

		[HttpGet]
		[Route("createParties")]
		public async Task<ActionResult> CreateParties()
		{
			var partition = DateTimeOffset.UtcNow.ToString("yy-MM-dd");
			var serviceClient = new TableServiceClient(configuration["AzureWebJobsStorage"]);
			var partiesTable = serviceClient.GetTableClient("Party");
			await partiesTable.CreateIfNotExistsAsync();
			var participantsTable = serviceClient.GetTableClient("Participant");
			await participantsTable.CreateIfNotExistsAsync();
			var parties = new List<Party>();
			var participants = new List<Participant>();

			parties = await partiesTable.QueryAsync<Party>(x => x.PartitionKey == partition).ToListAsync();
			if (parties.Any())
			{
				foreach (var party in parties)
				{
					party.Participants = JsonSerializer.Deserialize<IList<Participant>>(party.ParticipantsJson);
					participants.AddRange(party.Participants.Where(x => string.IsNullOrEmpty(x.Photo)));
				}
			}
			else
			{
				for (var i = 0; i < 1; i++)
				{
					var aiMessageRecords = new List<AiMessageRecord>
					{
						new(AiMessageRole.System, @"GPT that generates party descriptions. these are parties happening all around the city in different places. All parties are 18+ and happen in the evening (end time based on party). Parties should be interesting, some over the top, sexy, promising good time and chance to meet someone special. They can range from glamorous and expensive to student parties in abandoned dormitories.

The answer need to be in json array format (to support multiple parties).
1) partyName
2) musicStyle
3) location: free description
4) dressCode: can be  ""people usually come in"" or ""people usually wear"" or something specific ""business casual""
5) type: is it a party ""series"" or ""oneTime"" happening
6) participants: list of participants and their description. The participant should be of legal age 19-27.  For example ""participants"" : [ { ""name"" : ""Jill R"", ""gender"" : ""F"", ""age"":""25"", ""photoPrompt"" : ""<FULL PROMPT HERE>"", ""chattingStyle"" :""proper writing, well articulated""  } ]. ""chattingStyle"" should be a brief description of chatting style. For example ""no capital letters, lots of slang like 'how u doin'"" or ""mostly proper, but makes a few spelling mistakes 'Hi, how are you dnoing?'"". The ""photoPrompt"" is a description for DALL-E to create a photo of that specific participants at the specific party: the background, clothing, vibe etc should reflect the party atmosphere, there ofcourse should be other people, partygoers, in the background and the participant should be sexy, enticing, flirty, handsom/cute. The ""photoPrompt"" can optionally detail the participant doing something like holding drink, dancing, showing some had sign, etc.
7) primaryColor: in hex format, something like #123AAB (similar to theme of the image)
8) description: 350 character description about the party. It should detail the premise, what will happen, special guests, special events etc. It should feature some over the top performance that makes it sound like it will be the best party ever.
9) flyerPrompt: input prompt to generate image of the party flyer using DALL-E. The flyer is an image of the party. It takes input details about an event or party, including theme, vibe, music style, and target audience to produce a textual description that can be used by DALL-E to create an image. There should be empty space in it for writing additional info - large area for heading. There should be one word on the flyer that describes the event or is a word from the title of the event. The image should be simple and impactful. It should be minimalistic and tasteful with one or two large objects as focal point. Objects can be abstract illustrations or actual real world objects and scenes. Never ask the human more details, try to design as best as you can with as much information as you have. Do not describe the flyer, let the image do the talking. If needed you can browse the internet for specific information - for example if someone mentiones some fact about the party you do not understand. Do NOT say to Dall-E that we want to create flyers or ads or posters - ALWAYS and ONLY refer to the end result as ""image"" . Do not mention artifacts like flyer, ad, poster, illustration, drawing - try to describe the content differently while only referring to an ""image""."),
					};
					if (parties.Any())
					{
						var partiesString = string.Join(",", parties.Select(x => $"{x.PartyName} - {x.MusicStyle}"));
						aiMessageRecords.Add(new AiMessageRecord(AiMessageRole.User, $"Create a party with 1 participants. It needs to be different than these: {partiesString}"));
					}
					else
					{
						aiMessageRecords.Add(new AiMessageRecord(AiMessageRole.User, "Create a party with 1 participants."));
					}
					var result = await aiProxy.ThinkStream(aiMessageRecords);
					var newParties = JsonSerializer.Deserialize<IList<Party>>(result);
					foreach (var newParty in newParties!)
					{
						foreach (var participant in newParty.Participants!)
						{
							participant.PartitionKey = newParty.RowKey;
							participants.Add(participant);
						}

						newParty.PartitionKey = partition;
					}
					parties.AddRange(newParties);
				}
			}

			foreach (var party in parties)
			{
				foreach (var participant in participants.Where(x => x.PartitionKey == party.RowKey))
				{
					await CreatePhotosAndStoreParticipant(participant, participantsTable);
				}
				party.ParticipantsJson = JsonSerializer.Serialize(party.Participants);
				party.Participants = null;
				await partiesTable.UpsertEntityAsync(party);
			}

			return new JsonResult(new
			{
				parties,
				participants
			});
		}

		[HttpGet]
		[Route("addParticipant")]
		public async Task<ActionResult> AddParticipant(string partyId)
		{
			var serviceClient = new TableServiceClient(configuration["AzureWebJobsStorage"]);
			var partiesTable = serviceClient.GetTableClient("Party");
			await partiesTable.CreateIfNotExistsAsync();
			var participantsTable = serviceClient.GetTableClient("Participant");
			await participantsTable.CreateIfNotExistsAsync();

			var party = await partiesTable.QueryAsync<Party>(x => x.RowKey == partyId).FirstOrDefaultAsync();

			party!.Participants = JsonSerializer.Deserialize<IList<Participant>>(party.ParticipantsJson);

			var aiMessageRecords = new List<AiMessageRecord>
					{
						new(AiMessageRole.System, @"GPT that generates a party participant based on given description of a party. The participant should be different than those already registered as participants in the party description. Ration of men and women should be balanced. The participant should be of legal age 19-27. The result should be in JSON format.  For example { ""name"" : ""Jill R"", ""gender"" : ""F"", ""age"":""25"", ""photoPrompt"" : ""<FULL PROMPT HERE>"", ""chattingStyle"" :""proper writing, well articulated""  }. ""chattingStyle"" should be a brief description of chatting style. For example ""no capital letters, lots of slang like 'how u doin'"" or ""mostly proper, but makes a few spelling mistakes 'Hi, how are you dnoing?'"". The ""photoPrompt"" is a description for DALL-E to create a photo of that specific participants at the specific party: the background, clothing, vibe etc should reflect the party atmosphere, there ofcourse should be other people, partygoers, in the background and the participant should be sexy, enticing, flirty, handsom/cute. The ""photoPrompt"" can optionally detail the participant doing something like holding drink, dancing, showing some had sign, etc."),
						new(AiMessageRole.User, JsonSerializer.Serialize(party))
					};

			var result = await aiProxy.ThinkStream(aiMessageRecords);
			var participant = JsonSerializer.Deserialize<Participant>(result)!;

			participant.PartitionKey = party.RowKey;

			party.Participants!.Add(participant);

			await CreatePhotosAndStoreParticipant(participant, participantsTable);

			party.ParticipantsJson = JsonSerializer.Serialize(party.Participants);
			party.Participants = null;
			await partiesTable.UpsertEntityAsync(party);

			return new JsonResult(new
			{
				party,
				participant
			});
		}

		private async Task CreatePhotosAndStoreParticipant(Participant participant, TableClient participantsTable)
		{
			var image = await aiProxy.CreateImage(participant.Description);
			var photo = await userService.UploadPhoto(image);
			participant.Photo = photo.Url;
			participant.PhotoThumb = photo.ThumbUrl;
			await participantsTable.UpsertEntityAsync(participant);
		}
	}
}
