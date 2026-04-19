using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace SongFinder2_back.Services
{
    public class GroqService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GroqService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Groq:ApiKey"] ?? throw new ArgumentNullException("Groq:ApiKey");
            
            _httpClient.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<List<string>> FilterTracksAsync(Dictionary<string, string> tracksDict, string mood, string genre)
        {
            if (tracksDict == null || tracksDict.Count == 0) return new List<string>();

            var tracksListBuilder = new StringBuilder();
            foreach (var kvp in tracksDict)
            {
                tracksListBuilder.AppendLine($"ID: {kvp.Key} | Track: {kvp.Value}");
            }

            var systemPrompt = $@"You are an expert music curator and DJ.
The user wants tracks that STRICTLY match the mood: '{mood}' and genre: '{genre}'.
I will provide a list of tracks with their IDs.
Your task:
1. Analyze each track. 
2. Determine if it truly matches the requested mood and genre.
3. Be highly critical. Reject tracks that don't fit the mood, even if they fit the genre.
Return ONLY a valid JSON object with a single property 'accepted_ids' which contains an array of string IDs of the accepted tracks.
Example: {{ ""accepted_ids"": [""id1"", ""id2""] }}";

            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = tracksListBuilder.ToString() }
                },
                temperature = 0.1,
                response_format = new { type = "json_object" }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("chat/completions", jsonContent);
                if (response.IsSuccessStatusCode)
                {
                    var responseStr = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseStr);
                    var rawMessage = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

                    if (!string.IsNullOrWhiteSpace(rawMessage))
                    {
                        using var contentDoc = JsonDocument.Parse(rawMessage);
                        if (contentDoc.RootElement.TryGetProperty("accepted_ids", out var idsElement))
                        {
                            var ids = new List<string>();
                            foreach (var id in idsElement.EnumerateArray())
                            {
                                var val = id.GetString();
                                if (!string.IsNullOrWhiteSpace(val)) ids.Add(val);
                            }
                            return ids;
                        }
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Groq API Error: {error}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calling Groq API: {ex.Message}");
            }

            // Fallback: If AI checking completely fails (e.g., rate limit, network error), 
            // return all tracks so the app doesn't break.
            return tracksDict.Keys.ToList(); 
        }
    }
}
