using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Risk_assessor.Services
{
    public class OpenAIAssistantService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _assistantId;

        public OpenAIAssistantService(IConfiguration configuration)
        {
            _httpClient = new HttpClient();
            _apiKey = configuration["OpenAI:ApiKey"] ?? throw new ArgumentNullException("API Key missing");
            _assistantId = configuration["OpenAI:AssistantId"] ?? throw new ArgumentNullException("Assistant ID missing");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v1");
        }

        public async Task<string> GetAssistantResponseAsync(string userInput)
        {
            // Step 1: Create Thread
            string threadResponse;
            try
            {
                threadResponse = await PostAsync("https://api.openai.com/v1/threads");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to create OpenAI thread.", ex);
            }

            var threadJson = JsonDocument.Parse(threadResponse);
            var threadId = threadJson.RootElement.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            if (string.IsNullOrEmpty(threadId))
                throw new InvalidOperationException("Thread ID not found in OpenAI response.");

            // Step 2: Add user message
            var messageBody = new { role = "user", content = userInput };
            await PostAsync($"https://api.openai.com/v1/threads/{threadId}/messages", messageBody);

            // Step 3: Run the Assistant
            var runBody = new { assistant_id = _assistantId };
            var runResponse = await PostAsync($"https://api.openai.com/v1/threads/{threadId}/runs", runBody);
            var runJson = JsonDocument.Parse(runResponse);
            var runId = runJson.RootElement.TryGetProperty("id", out var runIdElement) ? runIdElement.GetString() : null;
            if (string.IsNullOrEmpty(runId))
                throw new InvalidOperationException("Run ID not found in OpenAI response.");

            // Step 4: Poll for completion
            string? status;
            do
            {
                await Task.Delay(1000);
                var statusResponse = await GetAsync($"https://api.openai.com/v1/threads/{threadId}/runs/{runId}");
                var jsonDoc = JsonDocument.Parse(statusResponse);
                status = jsonDoc.RootElement.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;
                if (status == null)
                    throw new InvalidOperationException("Status not found in OpenAI run response.");
            } while (status == "in_progress" || status == "queued");

            // Step 5: Get response message
            var messagesResponse = await GetAsync($"https://api.openai.com/v1/threads/{threadId}/messages");
            var messagesJson = JsonDocument.Parse(messagesResponse);
            var firstMessage = messagesJson.RootElement.GetProperty("data").EnumerateArray().FirstOrDefault();
            string? content = null;
            if (firstMessage.ValueKind != JsonValueKind.Undefined &&
                firstMessage.TryGetProperty("content", out var contentArray) &&
                contentArray.GetArrayLength() > 0 &&
                contentArray[0].TryGetProperty("text", out var textObj) &&
                textObj.TryGetProperty("value", out var valueObj))
            {
                content = valueObj.GetString();
            }

            return content ?? string.Empty;
        }

        //private async Task<string> PostAsync(string url, object? body = null)
        //{
        //    var request = new HttpRequestMessage(HttpMethod.Post, url);
        //    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        //    request.Headers.Add("OpenAI-Beta", "assistants=v1");
        //    var r = Newtonsoft.Json.JsonConvert.SerializeObject(request);
        //    body = new { };
        //    if (body != null)
        //    {
        //        var json = JsonSerializer.Serialize(body);
        //        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        //    }

        //    var response = await _httpClient.SendAsync(request);
        //    if (!response.IsSuccessStatusCode)
        //    {
        //        var errorContent = await response.Content.ReadAsStringAsync();
        //        var requestBody = body != null ? JsonSerializer.Serialize(body) : "<no body>";
        //        // Log or throw with details for debugging
        //        throw new HttpRequestException($"Request to {url} failed with status {response.StatusCode}.\nRequest Body: {requestBody}\nResponse: {errorContent}");
        //    }
        //    return await response.Content.ReadAsStringAsync();
        //}

        private async Task<string> PostAsync(string url, object? body = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Headers.Add("OpenAI-Beta", "assistants=v2"); // Updated to v2

            if (body != null)
            {
                var json = JsonSerializer.Serialize(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var requestBody = body != null ? JsonSerializer.Serialize(body) : "<no body>";
                throw new HttpRequestException($"Request to {url} failed with status {response.StatusCode}.\nRequest Body: {requestBody}\nResponse: {errorContent}");
            }

            return await response.Content.ReadAsStringAsync();
        }


        //private async Task<string> GetAsync(string url)
        //{
        //    var response = await _httpClient.GetAsync(url);
        //    var r = Newtonsoft.Json.JsonConvert.SerializeObject(response);
        //    response.EnsureSuccessStatusCode();
        //    return await response.Content.ReadAsStringAsync();
        //}

        private async Task<string> GetAsync(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Headers.Add("OpenAI-Beta", "assistants=v2");

            var response = await _httpClient.SendAsync(request);

            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Request to {url} failed with status {response.StatusCode}.\nResponse: {content}");
            }

            return content;
        }

    }
}
