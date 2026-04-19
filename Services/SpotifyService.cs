using Microsoft.Extensions.Configuration;
using SongFinder2_back.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using System;

namespace SongFinder2_back.Services
{
    public class SpotifyService
    {
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly GroqService _groqService;

        // Static cache to avoid requesting a new token for every user search
        private static string? _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;
        private static readonly object _lock = new object();

        public SpotifyService(IConfiguration configuration, GroqService groqService)
        {
            _clientId = configuration["Spotify:ClientId"] ?? throw new ArgumentNullException("Spotify:ClientId");
            _clientSecret = configuration["Spotify:ClientSecret"] ?? throw new ArgumentNullException("Spotify:ClientSecret");
            _groqService = groqService;
        }

        private async Task<SpotifyClient> GetSpotifyClientAsync()
        {
            lock (_lock)
            {
                if (_cachedToken != null && DateTime.Now < _tokenExpiry)
                {
                    var config = SpotifyClientConfig.CreateDefault()
                        .WithToken(_cachedToken)
                        .WithRetryHandler(new SimpleRetryHandler());
                    return new SpotifyClient(config);
                }
            }

            var configForAuth = SpotifyClientConfig.CreateDefault();
            var authRequest = new ClientCredentialsRequest(_clientId, _clientSecret);
            var oauth = new OAuthClient(configForAuth);
            
            try
            {
                var response = await oauth.RequestToken(authRequest);
                lock (_lock)
                {
                    _cachedToken = response.AccessToken;
                    _tokenExpiry = DateTime.Now.AddSeconds(response.ExpiresIn).AddMinutes(-5);
                }
                var configWithRetry = SpotifyClientConfig.CreateDefault()
                    .WithToken(response.AccessToken)
                    .WithRetryHandler(new SimpleRetryHandler());
                return new SpotifyClient(configWithRetry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get Spotify Token: {ex.Message}");
                throw;
            }
        }

        public async Task<List<TrackResponse>> GetRecommendationsAsync(MusicSearchRequest request)
        {
            var limit = request.Count;
            if (limit <= 0) limit = 10;
            if (limit > 50) limit = 50;

            var spotify = await GetSpotifyClientAsync();
            var rnd = new Random();

            var keywords = MoodMapper.GetSearchKeywords(request.Mood);
            var genre = request.Genre.ToLowerInvariant().Trim();
            var yearFilter = !string.IsNullOrWhiteSpace(request.YearRange) ? $" year:{request.YearRange}" : string.Empty;

            var pool = new List<FullTrack>();

            // --- SEMANTIC SEARCH STRATEGY ---
            // Instead of forcing literal word matches in titles (which returns wrong moods),
            // we use natural language semantic queries (e.g., "sad rock") so Spotify's AI 
            // evaluates the actual vibe/playlists the track belongs to.
            var selectedKeywords = keywords.OrderBy(_ => rnd.Next()).Take(Math.Min(keywords.Count, 3)).ToList();
            
            if (selectedKeywords.Count > 0)
            {
                // Run multiple semantic queries for maximum diversity
                foreach (var kw in selectedKeywords)
                {
                    // Query example: "genre:\"metal\" energetic"
                    var semanticQuery = $"genre:\"{genre}\" {kw}{yearFilter}";
                    var semanticTracks = await FetchTracks(spotify, semanticQuery, Math.Max(20, limit), rnd.Next(0, 10));
                    pool.AddRange(semanticTracks);
                }
            }

            // Fallback stays in the strict genre zone if we didn't get enough tracks
            if (pool.Count < limit)
            {
                var fallbackQuery = $"genre:\"{genre}\"{yearFilter}";
                var fallbackTracks = await FetchTracks(spotify, fallbackQuery, limit * 2, rnd.Next(0, 20));
                pool.AddRange(fallbackTracks);
            }

            // --- FINAL PROCESSING ---
            // Deduplicate, filter by year/popularity, and shuffle
            var uniqueTracks = pool
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Id))
                .GroupBy(t => t.Id) 
                .Select(g => g.First())
                .OrderBy(_ => rnd.Next())
                .Take(Math.Max(30, limit * 2)) // Fetch a solid pool for AI to check
                .ToList();



            // --- AI CURATION (GROQ LLM) ---
            // Send the raw list of tracks to our LLaMA 3 model to be heavily scrutinized
            var dictToFilter = uniqueTracks.ToDictionary(
                t => t.Id, 
                t => $"{t.Artists.FirstOrDefault()?.Name} - {t.Name} (Album: {t.Album?.Name ?? "N/A"})"
            );
            var acceptedIds = await _groqService.FilterTracksAsync(dictToFilter, request.Mood, genre);

            var finalTracks = uniqueTracks
                .Where(t => acceptedIds.Contains(t.Id))
                .Take(limit)
                .ToList();

            // Pad the list with original tracks if AI filtered out too many,
            // to ensure the user is not left with fewer tracks than requested.
            if (finalTracks.Count < limit && uniqueTracks.Count > finalTracks.Count)
            {
                var remainingNeeded = limit - finalTracks.Count;
                var paddingTracks = uniqueTracks
                    .Where(t => !acceptedIds.Contains(t.Id))
                    .Take(remainingNeeded);
                finalTracks.AddRange(paddingTracks);
            }

            // Safety fallback: if AI rejected literally everything or API failed, return the regular results
            if (finalTracks.Count == 0 && uniqueTracks.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("AI rejected all tracks or API failed, using standard fallback.");
                finalTracks = uniqueTracks.Take(limit).ToList();
            }

            return finalTracks.Select(track => new TrackResponse
            {
                TrackName = track.Name,
                Artist = track.Artists.FirstOrDefault()?.Name ?? "Unknown Artist",
                SpotifyUrl = track.ExternalUrls.ContainsKey("spotify") ? track.ExternalUrls["spotify"] : string.Empty,
                ImageUrl = track.Album?.Images?.FirstOrDefault()?.Url
            }).ToList();
        }

        private async Task<List<FullTrack>> FetchTracks(SpotifyClient spotify, string query, int requestedCount, int startOffset = 0)
        {
            var result = new List<FullTrack>();
            try
            {
                // STABILITY: Using sequential loop with safe limit to avoid deadlocks and 400 errors
                int currentOffset = Math.Min(startOffset, 1000 - 20);
                int fetchedSoFar = 0;
                
                while (fetchedSoFar < requestedCount)
                {
                    var searchRequest = new SearchRequest(SearchRequest.Types.Track, query) 
                    { 
                        Limit = null, // IMPORTANT: Avoids library 'Invalid limit' bug
                        Offset = currentOffset 
                    };
                    var searchResponse = await spotify.Search.Item(searchRequest);
                    
                    if (searchResponse.Tracks?.Items == null || searchResponse.Tracks.Items.Count == 0) break;
                    
                    result.AddRange(searchResponse.Tracks.Items);
                    fetchedSoFar += searchResponse.Tracks.Items.Count;
                    currentOffset += searchResponse.Tracks.Items.Count;

                    if (currentOffset > 980) break; 
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in FetchTracks: {ex.Message}");
            }
            return result;
        }
    }
}
