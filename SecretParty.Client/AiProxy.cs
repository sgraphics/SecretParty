using System.Text;
using Azure;
using Azure.AI.OpenAI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
			var api = Think2(messages, out var completionRequest);
			var result = await api.GetChatCompletionsStreamingAsync(completionRequest);
			var output = result.ToString();
			return output;
		}

		public async Task<string> CreateImage(string prompt)
		{
			var url = "https://api.clarifai.com/v2/users/openai/apps/dall-e/models/dall-e-3/versions/e38680e630bf4ee8a91454e83454fefc/outputs";
			var apiKey = _configuration["Clarifai"]; // Replace with your actual API key

			using var client = new HttpClient();

			client.DefaultRequestHeaders.Add("Authorization", $"Key {apiKey}");
			client.DefaultRequestHeaders.Add("Content-Type", "application/json");

			var json = @"{
                'inputs': [
                    {
                        'data': {
                            'text': {
                                'raw': '" + prompt + @"'
                            }
                        }
                    }
                ],
                'model': {
                    'model_version': {
                        'output_info': {
                            'params': {
                                'size':'1024x1024',
                                'quality':'hd',
                                'api_key':'" + apiKey + @"'"
			           + "} } } } }"; // Add any additional required parameters here

			var content = new StringContent(json, Encoding.UTF8, "application/json");
			var response = await client.PostAsync(url, content);

			if (response.IsSuccessStatusCode)
			{
				var responseContent = await response.Content.ReadAsStringAsync();
				Console.WriteLine(responseContent);

				return responseContent;
			}
			else
			{
				Console.WriteLine($"Error: {response.StatusCode}");
				throw new Exception(await response.Content.ReadAsStringAsync());
			}
		}

		public async Task<string> ThinkStream(IList<AiMessageRecord> messages)
		{
			var PAT = _configuration["ClarifaiPAT"];
			const string USER_ID = "openai";
			const string APP_ID = "chat-completion";
			const string MODEL_ID = "GPT-4";
			const string MODEL_VERSION_ID = "5d7a50b44aec4a01a9c492c5a5fcf387";
			var stream = new StringBuilder();
			foreach (var aiMessageRecord in messages.Where(x => x.Role != AiMessageRole.System))
			{
				stream.AppendLine($"{aiMessageRecord.Role.ToString()}:{aiMessageRecord.Content}");
			}

			stream.AppendLine($"{AiMessageRole.Assistant.ToString()}:");

			var RAW_TEXT = stream.ToString();

			// Create the JSON payload
			var payload = new
			{
				user_app_id = new { user_id = USER_ID, app_id = APP_ID },
				inputs = new[]
				{
					new
					{
						data = new
						{
							text = new { raw = RAW_TEXT }
						}
					}
				},
				model = new {
					model_version = new {
						output_info = new {
							@params= new {
								temperature = 0.7f,
								max_tokens = 2048,
								system_prompt = messages.First(x => x.Role == AiMessageRole.System).Content,
								frequency_penalty = 0.1f,
								presence_penalty = 0
							}
						}
					}
				}
			};

			var json = JsonConvert.SerializeObject(payload);

			using var client = new HttpClient();

			// Set the request headers
			client.DefaultRequestHeaders.Add("Accept", "application/json");
			client.DefaultRequestHeaders.Add("Authorization", $"Key {PAT}");

			// Create the content for the POST request
			var content = new StringContent(json, Encoding.UTF8, "application/json");

			// Make the POST request
			var response = await client.PostAsync($"https://api.clarifai.com/v2/models/{MODEL_ID}/versions/{MODEL_VERSION_ID}/outputs", content);

			var responseTExt = await response.Content.ReadAsStringAsync();
			if (response.IsSuccessStatusCode)
			{
				var responseContent = responseTExt;
				dynamic result = JsonConvert.DeserializeObject(responseContent);

				// Check if the status code is 10000 (success)
				if (result.status.code != 10000)
				{
					Console.WriteLine(result.status);
					throw new Exception("Invalid response");
				}
				else
				{
					var thinkStream = result.outputs[0].data.text.raw as JToken;
					Console.WriteLine(thinkStream?.Value<string>());
					return thinkStream?.Value<string>() ?? string.Empty;
				}
			}
			else
			{
				Console.WriteLine($"Error: {response.StatusCode}");
				throw new Exception(responseTExt);
			}
		}

		public async Task<IAsyncEnumerable<string>> ThinkStream2(IList<AiMessageRecord> messages)
		{
			var api = Think2(messages, out var completionRequest);
			var result = await api.GetChatCompletionsStreamingAsync(completionRequest);
			return result.Select(x => x.ContentUpdate);
		}

		private OpenAIClient Think2(IList<AiMessageRecord> messages, out ChatCompletionsOptions completionRequest)
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
							case AiMessageRole.Assistant:
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
		Assistant,
		User
	}
}
