using Azure;
using Azure.AI.OpenAI;

namespace SecretParty.Client
{
	public class AiProxy
	{
		private readonly IConfiguration _configuration;
		public AiProxy(IConfiguration configuration)
		{
			_configuration = configuration;
		}

		public async Task<string> Think(IList<AiMessageRecord> messages)
		{
			var api = Think(messages, out var completionRequest);
			var result = await api.GetChatCompletionsStreamingAsync(completionRequest);
			var output = result.ToString();
			return output;
		}

		public async Task<IAsyncEnumerable<string>> ThinkStream(IList<AiMessageRecord> messages)
		{
			var api = Think(messages, out var completionRequest);
			var result = await api.GetChatCompletionsStreamingAsync(completionRequest);
			return result.Select(x => x.ContentUpdate);
		}

		private OpenAIClient Think(IList<AiMessageRecord> messages, out ChatCompletionsOptions completionRequest)
		{
			var apiKey = _configuration["OpenAiKey"];
			
			var api = new OpenAIClient(
				new Uri("https://translate-better.openai.azure.com/"),
				new AzureKeyCredential(apiKey));


			completionRequest = new ChatCompletionsOptions(
				"translate-better-4k", messages
				.Select<AiMessageRecord, ChatRequestMessage>(x =>
				{
					switch (x.Role)
					{
						case AiMessageRole.System:
							return new ChatRequestSystemMessage(x.Content);
						case AiMessageRole.User:
							return new ChatRequestUserMessage(x.Content);
						case AiMessageRole.Partygoer:
							return new ChatRequestAssistantMessage(x.Content);
					}

					throw new NotImplementedException();
				})
				.ToList())
			{
				Temperature = 0.7f,
				MaxTokens = 2000,
				FrequencyPenalty = 0.1f,
				PresencePenalty = 0,
			};
			return api;
		}
	}

	public record AiMessageRecord(AiMessageRole Role, string Content)
	{
	}

	public enum AiMessageRole
	{
		System,
		Partygoer,
		User
	}
}
