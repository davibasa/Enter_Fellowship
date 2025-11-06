using Enter_Extractor_Api.Models;
using System.Text.Json;
using System.Text;
using OpenAI;
using OpenAI.Chat;
namespace Enter_Extractor_Api.Services
{
    public interface ILLMService
    {
        Task<(Dictionary<string, object?> result, int tokensUsed)> ExtractAsync(string text, Dictionary<string, string> schema);
    }
    public class OpenAIService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly OpenAIClient _openAIClient;

        public OpenAIService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<OpenAIService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key not configured");
            }
            _openAIClient = new OpenAIClient(apiKey);
        }


        public async Task<(Dictionary<string, object?> result, int tokensUsed)> ExtractAsync(
            string text,
            Dictionary<string, string> schema)
        {
            try
            {
                var prompt = BuildPrompt(text, schema);

                var chatClient = _openAIClient.GetChatClient("gpt-5-mini"); // Note: "gpt-5-mini" não existe

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(
                        "You are a precise data extraction assistant. Extract structured data from document text. " +
                        "Return ONLY valid JSON with the requested fields. Use null for missing fields. " +
                        "Be accurate - even 1 character wrong makes the field incorrect."
                    ),
                    new UserChatMessage(prompt)
                };

                var options = new ChatCompletionOptions
                {
                    ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
                };

                _logger.LogInformation("Sending request to OpenAI API");

                var completion = await chatClient.CompleteChatAsync(messages, options);

                var resultText = completion.Value.Content[0].Text;
                var tokensUsed = completion.Value.Usage.TotalTokenCount;

                _logger.LogInformation("Received response from OpenAI, tokens used: {TokensUsed}", tokensUsed);

                // Parse the JSON response
                var extractedData = JsonSerializer.Deserialize<Dictionary<string, object?>>(resultText);

                if (extractedData == null)
                {
                    throw new InvalidOperationException("Failed to parse OpenAI response as JSON");
                }

                // Ensure all schema fields are present (even if null)
                var result = new Dictionary<string, object?>();
                foreach (var field in schema.Keys)
                {
                    result[field] = extractedData.ContainsKey(field) ? extractedData[field] : null;
                }

                return (result, tokensUsed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API");
                throw;
            }
        }

        private string BuildPrompt(string text, Dictionary<string, string> schema)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Extract the following information from this document text:");
            sb.AppendLine();
            sb.AppendLine("DOCUMENT TEXT:");
            sb.AppendLine("```");

            // Truncate text if too long (keep first and last parts)
            if (text.Length > 3000)
            {
                var firstPart = text.Substring(0, 1500);
                var lastPart = text.Substring(text.Length - 1500);
                sb.AppendLine(firstPart);
                sb.AppendLine("... [text truncated] ...");
                sb.AppendLine(lastPart);
            }
            else
            {
                sb.AppendLine(text);
            }

            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("FIELDS TO EXTRACT:");

            foreach (var kvp in schema)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }

            sb.AppendLine();
            sb.AppendLine("Return a JSON object with these exact field names. Use null for fields not found in the document.");
            sb.AppendLine("Be precise with formatting, dates, numbers, and text. Case-insensitive but maintain original formatting.");

            return sb.ToString();
        }

    }
}
